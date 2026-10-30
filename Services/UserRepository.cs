using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Zhuanzhu.WebApi.Services
{
    public class AppUserRow
    {
        public long Id { get; set; }

        public string NickName { get; set; }
    }

    public class UserRepository
    {
        private readonly Db _db;

        public UserRepository(Db db)
        {
            _db = db;
        }

        public async Task<AppUserRow> GetByIdAsync(long id, CancellationToken cancellationToken)
        {
            using (var connection = _db.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using (var command = new SqlCommand("SELECT Id, NickName FROM dbo.AppUsers WHERE Id=@id", connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        if (!await reader.ReadAsync(cancellationToken))
                        {
                            return null;
                        }
                        return new AppUserRow
                        {
                            Id = reader.GetInt64(0),
                            NickName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                        };
                    }
                }
            }
        }

        /// <summary>按 openid 取用户；首次登录自动建档。</summary>
        public async Task<AppUserRow> GetOrCreateByOpenIdAsync(
            string openId,
            string unionId,
            string sessionKey,
            string loginIp,
            CancellationToken cancellationToken)
        {
            using (var connection = _db.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);

                var existing = await FindByOpenIdAsync(connection, openId, cancellationToken);
                if (existing != null)
                {
                    await TouchLoginAsync(connection, existing.Id, unionId, sessionKey, loginIp, cancellationToken);
                    return existing;
                }

                try
                {
                    return await InsertAsync(connection, openId, unionId, sessionKey, loginIp, cancellationToken);
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    // 同一用户并发首次登录：唯一索引冲突后回读已有记录
                    var again = await FindByOpenIdAsync(connection, openId, cancellationToken);
                    if (again == null)
                    {
                        throw;
                    }
                    await TouchLoginAsync(connection, again.Id, unionId, sessionKey, loginIp, cancellationToken);
                    return again;
                }
            }
        }

        private static async Task<AppUserRow> FindByOpenIdAsync(
            SqlConnection connection,
            string openId,
            CancellationToken cancellationToken)
        {
            using (var command = new SqlCommand(
                "SELECT Id, NickName FROM dbo.AppUsers WHERE OpenId=@openId", connection))
            {
                command.Parameters.AddWithValue("@openId", openId);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        return null;
                    }
                    return new AppUserRow
                    {
                        Id = reader.GetInt64(0),
                        NickName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                    };
                }
            }
        }

        private static async Task TouchLoginAsync(
            SqlConnection connection,
            long id,
            string unionId,
            string sessionKey,
            string loginIp,
            CancellationToken cancellationToken)
        {
            const string sql = @"UPDATE dbo.AppUsers
SET SessionKey=@sessionKey,
    UnionId=ISNULL(@unionId, UnionId),
    LastLoginTime=SYSDATETIME(),
    LastLoginIp=@ip
WHERE Id=@id";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sessionKey", (object)sessionKey ?? DBNull.Value);
                command.Parameters.AddWithValue("@unionId", (object)unionId ?? DBNull.Value);
                command.Parameters.AddWithValue("@ip", (object)loginIp ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static async Task<AppUserRow> InsertAsync(
            SqlConnection connection,
            string openId,
            string unionId,
            string sessionKey,
            string loginIp,
            CancellationToken cancellationToken)
        {
            const string sql = @"INSERT INTO dbo.AppUsers (OpenId, UnionId, SessionKey, NickName, CreateTime, LastLoginTime, LastLoginIp)
OUTPUT INSERTED.Id, INSERTED.NickName
VALUES (@openId, @unionId, @sessionKey, N'微信用户', SYSDATETIME(), SYSDATETIME(), @ip)";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@openId", openId);
                command.Parameters.AddWithValue("@unionId", (object)unionId ?? DBNull.Value);
                command.Parameters.AddWithValue("@sessionKey", (object)sessionKey ?? DBNull.Value);
                command.Parameters.AddWithValue("@ip", (object)loginIp ?? DBNull.Value);

                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        throw new InvalidOperationException("写入用户记录失败。");
                    }
                    return new AppUserRow
                    {
                        Id = reader.GetInt64(0),
                        NickName = reader.IsDBNull(1) ? "微信用户" : reader.GetString(1)
                    };
                }
            }
        }
    }
}
