namespace Zhuanzhu.WebApi.Options
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = "zhuanzhu-webapi";

        public string Audience { get; set; } = "zhuanzhu-miniprogram";

        /// <summary>HS256 密钥，至少 32 个字符。</summary>
        public string Secret { get; set; } = string.Empty;

        public int ExpireDays { get; set; } = 7;
    }
}
