using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Zhuanzhu.WebApi.Models;
using Zhuanzhu.WebApi.Services;

namespace Zhuanzhu.WebApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly WechatAuthService _wechat;
        private readonly UserRepository _users;
        private readonly TokenService _tokens;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            WechatAuthService wechat,
            UserRepository users,
            TokenService tokens,
            ILogger<AuthController> logger)
        {
            _wechat = wechat;
            _users = users;
            _tokens = tokens;
            _logger = logger;
        }

        /// <summary>小程序 wx.login 拿到 code 后调用，换取本站 token。</summary>
        [HttpPost("wechat/login")]
        [AllowAnonymous]
        public async Task<ApiResult> WechatLogin(
            [FromBody] WechatLoginRequest request,
            CancellationToken cancellationToken)
        {
            var code = request == null ? null : (request.Code ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                return ApiResult.Fail(1001, "缺少登录凭证 code");
            }
            if (code.Length > 512)
            {
                return ApiResult.Fail(1001, "登录凭证格式不正确");
            }

            var session = await _wechat.Code2SessionAsync(code, cancellationToken);
            if (!session.Ok)
            {
                return ApiResult.Fail(session.ErrorCode, session.ErrorMessage);
            }

            var ip = HttpContext.Connection.RemoteIpAddress == null
                ? null
                : HttpContext.Connection.RemoteIpAddress.ToString();

            AppUserRow user;
            try
            {
                user = await _users.GetOrCreateByOpenIdAsync(
                    session.OpenId, session.UnionId, session.SessionKey, ip, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信登录写入用户失败");
                return ApiResult.Fail(1005, "登录服务异常，请稍后再试");
            }

            var issued = _tokens.Issue(user.Id);
            _logger.LogInformation("微信登录成功，userId={UserId}", user.Id);

            return ApiResult.Ok(new WechatLoginResponse
            {
                Token = issued.Token,
                ExpiresIn = issued.ExpiresIn,
                UserId = user.Id,
                NickName = string.IsNullOrWhiteSpace(user.NickName) ? "微信用户" : user.NickName
            });
        }

        /// <summary>校验 token 并返回当前用户，供小程序启动时静默检查登录态。</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ApiResult> Me(CancellationToken cancellationToken)
        {
            var raw = User.FindFirst(TokenService.UserIdClaim);
            long userId;
            if (raw == null || !long.TryParse(raw.Value, out userId))
            {
                return ApiResult.Fail(2001, "登录已失效，请重新登录");
            }

            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return ApiResult.Fail(2002, "账户不存在，请重新登录");
            }

            return ApiResult.Ok(new MeResponse
            {
                UserId = user.Id,
                NickName = string.IsNullOrWhiteSpace(user.NickName) ? "微信用户" : user.NickName
            });
        }
    }
}
