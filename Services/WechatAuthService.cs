using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zhuanzhu.WebApi.Options;

namespace Zhuanzhu.WebApi.Services
{
    public class WechatSessionResult
    {
        public bool Ok { get; set; }

        public int ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public string OpenId { get; set; }

        public string SessionKey { get; set; }

        public string UnionId { get; set; }
    }

    /// <summary>调用微信 jscode2session，换取 openid。AppSecret 只在本机调用中出现。</summary>
    public class WechatAuthService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;
        private readonly WechatOptions _options;
        private readonly ILogger<WechatAuthService> _logger;

        public WechatAuthService(HttpClient http, WechatOptions options, ILogger<WechatAuthService> logger)
        {
            _http = http;
            _options = options;
            _logger = logger;
        }

        public async Task<WechatSessionResult> Code2SessionAsync(string code, CancellationToken cancellationToken)
        {
            if (!_options.IsConfigured)
            {
                return new WechatSessionResult
                {
                    Ok = false,
                    ErrorCode = 1004,
                    ErrorMessage = "服务端未配置 AppSecret，暂时无法完成微信登录"
                };
            }

            var url = string.Format(
                "{0}?appid={1}&secret={2}&js_code={3}&grant_type=authorization_code",
                _options.Code2SessionUrl.TrimEnd('?'),
                Uri.EscapeDataString(_options.AppId),
                Uri.EscapeDataString(_options.AppSecret),
                Uri.EscapeDataString(code));

            try
            {
                using (var response = await _http.GetAsync(url, cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("调用 jscode2session 失败，HTTP {Status}", (int)response.StatusCode);
                        return new WechatSessionResult
                        {
                            Ok = false,
                            ErrorCode = 1003,
                            ErrorMessage = "微信登录服务暂时不可用，请稍后再试"
                        };
                    }

                    var payload = JsonSerializer.Deserialize<Code2SessionPayload>(body, JsonOptions);
                    if (payload == null)
                    {
                        _logger.LogError("jscode2session 返回内容无法解析");
                        return new WechatSessionResult
                        {
                            Ok = false,
                            ErrorCode = 1003,
                            ErrorMessage = "微信登录服务返回异常，请稍后再试"
                        };
                    }

                    if (payload.ErrCode != 0 || string.IsNullOrEmpty(payload.OpenId))
                    {
                        _logger.LogWarning("jscode2session 拒绝请求：errcode={ErrCode}, errmsg={ErrMsg}",
                            payload.ErrCode, payload.ErrMsg);
                        return MapError(payload.ErrCode);
                    }

                    return new WechatSessionResult
                    {
                        Ok = true,
                        OpenId = payload.OpenId,
                        SessionKey = payload.SessionKey,
                        UnionId = payload.UnionId
                    };
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("调用 jscode2session 超时");
                return new WechatSessionResult
                {
                    Ok = false,
                    ErrorCode = 1003,
                    ErrorMessage = "微信登录服务响应超时，请稍后再试"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用 jscode2session 异常");
                return new WechatSessionResult
                {
                    Ok = false,
                    ErrorCode = 1003,
                    ErrorMessage = "微信登录服务暂时不可用，请稍后再试"
                };
            }
        }

        private static WechatSessionResult MapError(int errCode)
        {
            // 40029 code 无效 / 40163 code 已被使用 → 前端重新 wx.login 即可恢复
            if (errCode == 40029 || errCode == 40163 || errCode == 40226)
            {
                return new WechatSessionResult
                {
                    Ok = false,
                    ErrorCode = 1002,
                    ErrorMessage = "登录凭证已失效，请重试"
                };
            }

            return new WechatSessionResult
            {
                Ok = false,
                ErrorCode = 1003,
                ErrorMessage = "微信登录失败，请稍后再试"
            };
        }

        private class Code2SessionPayload
        {
            [JsonPropertyName("openid")]
            public string OpenId { get; set; }

            [JsonPropertyName("session_key")]
            public string SessionKey { get; set; }

            [JsonPropertyName("unionid")]
            public string UnionId { get; set; }

            [JsonPropertyName("errcode")]
            public int ErrCode { get; set; }

            [JsonPropertyName("errmsg")]
            public string ErrMsg { get; set; }
        }
    }
}
