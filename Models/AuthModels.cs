namespace Zhuanzhu.WebApi.Models
{
    public class WechatLoginRequest
    {
        /// <summary>wx.login 返回的临时登录凭证，5 分钟内有效且只能用一次。</summary>
        public string Code { get; set; }
    }

    public class WechatLoginResponse
    {
        public string Token { get; set; }

        /// <summary>有效期秒数。</summary>
        public int ExpiresIn { get; set; }

        public long UserId { get; set; }

        public string NickName { get; set; }
    }

    public class MeResponse
    {
        public long UserId { get; set; }

        public string NickName { get; set; }
    }
}
