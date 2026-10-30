using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Zhuanzhu.WebApi.Options;
using Zhuanzhu.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ---------------------------------------------------------------- 配置
var wechatOptions = builder.Configuration.GetSection("Wechat").Get<WechatOptions>() ?? new WechatOptions();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException("配置项 Jwt:Secret 缺失或长度不足 32 个字符，请先在 appsettings.json 中设置。");
}

builder.Services.AddSingleton(wechatOptions);
builder.Services.AddSingleton(jwtOptions);

// ---------------------------------------------------------------- 认证
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 不做入站 claim 映射，统一按 "uid" 取值
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
        };
        options.Events = new JwtBearerEvents
        {
            // 小程序端约定：401 时也返回 JSON，便于前端识别并重新登录
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"code\":2001,\"message\":\"登录已失效，请重新登录\",\"data\":null}");
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------- 业务服务
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHttpClient<WechatAuthService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<DatabaseInitializer>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Text("Zhuanzhu WebApi is running.", "text/plain; charset=utf-8"));

app.Run();
