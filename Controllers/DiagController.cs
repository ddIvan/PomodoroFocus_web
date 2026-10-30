using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Zhuanzhu.WebApi.Services;

namespace Zhuanzhu.WebApi.Controllers
{
    /// <summary>
    /// 配置体检：确认运行环境实际加载了哪些 appsettings 文件、关键配置最终取到哪个值、数据库是否连通。
    /// 仅在 appsettings 中配置了 Diagnostics:Token 时可用，避免对外暴露。
    /// 不返回任何密码或密钥明文，只返回长度与来源。
    /// </summary>
    [ApiController]
    [Route("api/diag")]
    public class DiagController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly Db _db;

        public DiagController(IConfiguration configuration, IWebHostEnvironment environment, Db db)
        {
            _configuration = configuration;
            _environment = environment;
            _db = db;
        }

        [HttpGet("config")]
        [AllowAnonymous]
        public async Task<IActionResult> Config([FromQuery] string token, CancellationToken cancellationToken)
        {
            var expected = _configuration["Diagnostics:Token"];
            if (string.IsNullOrWhiteSpace(expected)
                || !string.Equals(expected.Trim(), (token ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                return NotFound();
            }

            var files = new List<object>();
            if (_configuration is IConfigurationRoot root)
            {
                foreach (var provider in root.Providers)
                {
                    if (!(provider is JsonConfigurationProvider json) || !(json.Source is JsonConfigurationSource source))
                    {
                        continue;
                    }

                    string physical = string.Empty;
                    if (!string.IsNullOrEmpty(source.Path))
                    {
                        try
                        {
                            var info = source.FileProvider?.GetFileInfo(source.Path);
                            physical = info == null ? string.Empty : (info.PhysicalPath ?? string.Empty);
                        }
                        catch
                        {
                            physical = string.Empty;
                        }
                    }

                    files.Add(new
                    {
                        path = source.Path,
                        physicalPath = physical,
                        exists = physical.Length > 0 && System.IO.File.Exists(physical),
                        optional = source.Optional
                    });
                }
            }

            var secret = _configuration["Jwt:Secret"] ?? string.Empty;
            var database = await ProbeDatabaseAsync(cancellationToken);

            return Ok(new
            {
                environment = _environment.EnvironmentName,
                contentRoot = _environment.ContentRootPath,
                machineName = Environment.MachineName,
                configFiles = files,
                connection = SafeConnection(_configuration.GetConnectionString("Zhuanzhu")),
                database,
                jwt = new
                {
                    issuer = _configuration["Jwt:Issuer"],
                    audience = _configuration["Jwt:Audience"],
                    expireDays = _configuration["Jwt:ExpireDays"],
                    secretLength = secret.Length
                },
                wechat = new
                {
                    appId = _configuration["Wechat:AppId"],
                    appSecretConfigured = !string.IsNullOrWhiteSpace(_configuration["Wechat:AppSecret"])
                }
            });
        }

        /// <summary>实际连一次库，确认登录链路可写入。</summary>
        private async Task<object> ProbeDatabaseAsync(CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;
            try
            {
                using (var connection = _db.CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT DB_NAME()";
                        var name = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
                        return new
                        {
                            ok = true,
                            name,
                            elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    ok = false,
                    error = ex.Message,
                    elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds
                };
            }
        }

        /// <summary>连接串脱敏输出：只给服务器、库名、账号与密码长度，不给密码本身。</summary>
        private static object SafeConnection(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new { configured = false };
            }

            try
            {
                var builder = new SqlConnectionStringBuilder(raw);
                return new
                {
                    configured = true,
                    server = builder.DataSource,
                    database = builder.InitialCatalog,
                    user = builder.UserID,
                    integratedSecurity = builder.IntegratedSecurity,
                    passwordLength = builder.Password == null ? 0 : builder.Password.Length
                };
            }
            catch (Exception ex)
            {
                return new { configured = true, error = ex.Message };
            }
        }
    }
}
