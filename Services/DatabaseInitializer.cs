using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zhuanzhu.WebApi.Services
{
    /// <summary>
    /// 启动时确保数据库与表存在，避免首次部署还要手工建库。
    /// 关闭方式：appsettings.json 里 Database:AutoInitialize = false。
    /// </summary>
    public class DatabaseInitializer : IHostedService
    {
        private static readonly Regex SafeDatabaseName =
            new Regex(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        private const string UsersTableSql = @"
IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        OpenId        NVARCHAR(64)  NOT NULL,
        UnionId       NVARCHAR(64)  NULL,
        SessionKey    NVARCHAR(256) NULL,
        NickName      NVARCHAR(64)  NOT NULL CONSTRAINT DF_AppUsers_NickName DEFAULT (N'微信用户'),
        CreateTime    DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUsers_CreateTime DEFAULT (SYSDATETIME()),
        LastLoginTime DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUsers_LastLoginTime DEFAULT (SYSDATETIME()),
        LastLoginIp   NVARCHAR(64)  NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_AppUsers_OpenId' AND object_id = OBJECT_ID(N'dbo.AppUsers'))
BEGIN
    CREATE UNIQUE INDEX UX_AppUsers_OpenId ON dbo.AppUsers(OpenId);
END;
";

        private readonly Db _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(Db db, IConfiguration configuration, ILogger<DatabaseInitializer> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var autoInitialize = _configuration.GetValue<bool?>("Database:AutoInitialize") ?? true;
            if (!autoInitialize)
            {
                _logger.LogInformation("已关闭数据库自动初始化，跳过建库建表");
                return;
            }

            try
            {
                await EnsureDatabaseAsync(cancellationToken);
                _logger.LogInformation("数据库结构与表检查完成");
            }
            catch (Exception ex)
            {
                // 不阻断进程启动，方便先起站点再修配置；接口调用时会返回明确错误
                _logger.LogError(ex, "数据库初始化失败，请检查连接串，或手动执行 database/init_database.sql");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new SqlConnectionStringBuilder(_db.ConnectionString);
            var database = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(database) || !SafeDatabaseName.IsMatch(database))
            {
                throw new InvalidOperationException("连接串中的数据库名缺失或包含非法字符（仅允许字母、数字、下划线）。");
            }

            var masterBuilder = new SqlConnectionStringBuilder(_db.ConnectionString)
            {
                InitialCatalog = "master"
            };

            using (var master = new SqlConnection(masterBuilder.ConnectionString))
            {
                await master.OpenAsync(cancellationToken);

                using (var existsCommand = master.CreateCommand())
                {
                    existsCommand.CommandText = "SELECT DB_ID(@name)";
                    existsCommand.Parameters.AddWithValue("@name", database);
                    var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
                    if (exists == null || exists == DBNull.Value)
                    {
                        using (var createCommand = master.CreateCommand())
                        {
                            createCommand.CommandText = "CREATE DATABASE [" + database + "]";
                            await createCommand.ExecuteNonQueryAsync(cancellationToken);
                            _logger.LogInformation("已创建数据库 {Database}", database);
                        }
                    }
                }
            }

            using (var connection = _db.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UsersTableSql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
    }
}
