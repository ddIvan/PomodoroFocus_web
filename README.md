# 番茄专注管理 · 服务端（Zhuanzhu WebApi）

番茄专注小程序的登录后端。职责只有三件事：

1. 接收小程序 `wx.login()` 拿到的 `code`，调用微信 `jscode2session` 换取 `openid`
2. 把用户写入 `AppUsers` 表（不存在则创建）
3. 签发本站 JWT，供小程序后续请求携带

**服务端不保存任何专注数据**：番茄记录、计划、统计、监控结果全部存在小程序本地存储，服务端不接收也不存储。

- 同系列仓库：[Android 端](https://github.com/ddIvan/PomodoroFocus) · [微信小程序端](https://github.com/ddIvan/PomodoroFocus_webchat)
- 许可证：Apache License 2.0

---

## 一、技术栈

| 项 | 选型 |
| --- | --- |
| 框架 | ASP.NET Core 6.0（`net6.0`，Web SDK） |
| 数据库访问 | `System.Data.SqlClient` 4.8.6，手写 SQL，无 ORM |
| 认证 | `Microsoft.AspNetCore.Authentication.JwtBearer` 6.0.36 |
| 数据库 | SQL Server 2016 及以上 |
| 部署 | IIS（in-process，已附 `web.config`） |

---

## 二、目录结构

```
web/
├── Program.cs                        入口：DI、JWT 配置、401 统一 JSON、路由
├── Controllers/
│   ├── AuthController.cs             微信登录、当前用户
│   └── DiagController.cs             配置体检（仅排障用，默认隐藏）
├── Models/
│   ├── ApiResult.cs                  统一响应 { code, message, data }
│   └── AuthModels.cs                 登录请求/响应模型
├── Options/
│   ├── JwtOptions.cs
│   └── WechatOptions.cs
├── Services/
│   ├── Db.cs                         连接串与连接工厂
│   ├── DatabaseInitializer.cs        启动时自动建库建表（IHostedService）
│   ├── TokenService.cs               JWT 签发
│   ├── UserRepository.cs             用户读写
│   └── WechatAuthService.cs          jscode2session 调用与错误映射
├── database/init_database.sql        手动初始化脚本（幂等）
├── Properties/launchSettings.json    本机调试端口
├── appsettings.json                  基础配置（密钥留空，已入库）
├── appsettings.Production.example.json  生产配置模板，已入库
├── web.config                        IIS 托管配置，已指定 Production 环境
└── Zhuanzhu.WebApi.csproj
```

---

## 三、快速开始

### 1. 准备配置

```powershell
cd web
Copy-Item appsettings.Production.example.json appsettings.Development.json
# 编辑 appsettings.Development.json，填入数据库密码与小程序 AppSecret
```

### 2. 初始化数据库

程序启动时会自动建库建表（`Database:AutoInitialize = true`），也可以手动执行：

```powershell
sqlcmd -S 127.0.0.1,1433 -U sa -P 你的密码 -i database/init_database.sql
```

### 3. 启动

```powershell
dotnet restore
dotnet run
# 默认监听 http://localhost:5131（见 Properties/launchSettings.json）
```

自定义端口：

```powershell
dotnet run --urls "http://0.0.0.0:5080"
```

### 4. 验证

```bash
curl http://localhost:5131/                      # Zhuanzhu WebApi is running.
curl http://localhost:5131/api/auth/me           # 401 + {"code":2001,...}
```

---

## 四、配置说明

配置按环境分层，**密钥文件不入库**：

| 文件 | 是否入库 | 用途 |
| --- | --- | --- |
| `appsettings.json` | ✅ | 基础配置，密钥为占位值（`CHANGE_ME` / 空） |
| `appsettings.Production.example.json` | ✅ | 生产配置模板，供部署时复制 |
| `appsettings.Development.json` | ❌ | 本机开发用的真实密码与 AppSecret |
| `appsettings.Production.json` | ❌ | 生产环境的真实密码与 AppSecret |

| 配置项 | 必填 | 说明 |
| --- | --- | --- |
| `ConnectionStrings:Zhuanzhu` | ✅ | SQL Server 连接串 |
| `Wechat:AppId` | ✅ | 小程序 AppID |
| `Wechat:AppSecret` | ✅ | 小程序 AppSecret，缺失时登录返回 `1004` |
| `Wechat:Code2SessionUrl` | ✅ | 默认 `https://api.weixin.qq.com/sns/jscode2session` |
| `Jwt:Issuer` / `Jwt:Audience` | ✅ | 签发与校验必须一致 |
| `Jwt:Secret` | ✅ | **长度不足 32 字符会直接启动失败** |
| `Jwt:ExpireDays` | — | token 有效期，默认 7 天 |
| `Database:AutoInitialize` | — | 启动时自动建库建表，默认 `true` |
| `Diagnostics:Token` | — | 配置后才开放 `/api/diag/config` |

---

## 五、接口

统一响应结构，`code = 0` 表示成功：

```json
{ "code": 0, "message": "ok", "data": { } }
```

### 1. 微信登录

```
POST /api/auth/wechat/login
Content-Type: application/json
```

请求：

```json
{ "code": "0a3Xk1..." }
```

成功：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "token": "eyJhbGciOi...",
    "expiresIn": 604800,
    "userId": 1,
    "nickName": "微信用户"
  }
}
```

### 2. 当前用户

```
GET /api/auth/me
Authorization: Bearer <token>
```

成功：

```json
{ "code": 0, "message": "ok", "data": { "userId": 1, "nickName": "微信用户" } }
```

未带 token 或 token 失效时返回 **HTTP 401**，响应体仍是统一结构：

```json
{ "code": 2001, "message": "登录已失效，请重新登录", "data": null }
```

### 3. 配置体检（排障用）

```
GET /api/diag/config?token=<Diagnostics:Token>
```

只有 `Diagnostics:Token` 已配置且完全匹配时才返回数据，否则返回 **404**（不暴露接口存在性）。返回内容包含运行环境、实际加载的 appsettings 文件、脱敏后的连接串信息、数据库连通性探测结果、JWT 与微信配置的存在性，**不包含任何密码或密钥明文**。

### 错误码

| code | HTTP | 含义 | 处理建议 |
| --- | --- | --- | --- |
| `0` | 200 | 成功 | — |
| `1001` | 200 | 缺少或格式不正确的 code | 小程序重新 `wx.login()` |
| `1002` | 200 | code 已失效（微信 40029/40163/40226） | 重新 `wx.login()` 后再试 |
| `1003` | 200 | 微信服务异常或超时 | 稍后重试 |
| `1004` | 200 | 服务端未配置 AppSecret | 检查 `Wechat:AppSecret` |
| `1005` | 200 | 用户落库失败 | 查服务端日志与数据库连通性 |
| `2001` | 401 | token 缺失或已过期 | 重新登录 |
| `2002` | 200 | token 有效但用户已不存在 | 重新登录 |

---

## 六、数据库

只需一张表：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | BIGINT IDENTITY | 主键 |
| `OpenId` | NVARCHAR(64) | 微信 openid，唯一索引 `UX_AppUsers_OpenId` |
| `UnionId` | NVARCHAR(64) | 可空 |
| `SessionKey` | NVARCHAR(256) | 可空，预留 |
| `NickName` | NVARCHAR(64) | 默认「微信用户」 |
| `CreateTime` | DATETIME2(0) | 默认 `SYSDATETIME()` |
| `LastLoginTime` | DATETIME2(0) | 每次登录更新 |
| `LastLoginIp` | NVARCHAR(64) | 可空 |

登录时按 `OpenId` 做 upsert：首次创建，之后只更新 `LastLoginTime` 与 `LastLoginIp`。

---

## 七、部署（IIS）

```powershell
dotnet publish -c Release -o ./dist
```

1. 服务器安装 **.NET 6 Hosting Bundle**（不是 SDK 也要装，IIS 集成必需）
2. `dist` 整目录复制到站点目录，例如 `D:\sites\zhuanzhu`
3. 把 `appsettings.Production.example.json` 复制为 `appsettings.Production.json` 并填真实值
4. IIS 新建应用池（.NET CLR 版本选「无托管代码」），站点指向该目录
5. `web.config` 已声明 `ASPNETCORE_ENVIRONMENT=Production`，会加载 `appsettings.Production.json`

端口与证书：小程序要求 https。若前面有反向代理（nginx / IIS ARR），把外部 https 转发到本地 http 端口即可；证书需为公共 CA 签发，域名须与小程序后台配置的 request 合法域名逐字一致。

---

## 八、安全约定

- 真实密码、AppSecret、JWT Secret 只存在于被 `.gitignore` 排除的 `appsettings.Development.json` / `appsettings.Production.json`
- 仓库内的 `appsettings.json` 一律为占位值，clone 后必须自行填写才能运行
- AppSecret 只出现在服务端 → 微信的请求中，不会下发给客户端
- `/api/diag/config` 默认关闭，未配置 Token 时对外表现为 404
- JWT 采用 HMAC-SHA256 对称签名，`Jwt:Secret` 更换后所有已签发 token 立即失效

---

## 九、许可证

本项目基于 [Apache License 2.0](LICENSE) 发布，版权声明见 [NOTICE](NOTICE)。

Copyright 2026 ddIvan
