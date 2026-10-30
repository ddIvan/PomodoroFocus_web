namespace Zhuanzhu.WebApi.Options
{
    /// <summary>小程序登录所需配置，AppSecret 只在服务端保存。</summary>
    public class WechatOptions
    {
        public string AppId { get; set; } = string.Empty;

        public string AppSecret { get; set; } = string.Empty;

        public string Code2SessionUrl { get; set; } = "https://api.weixin.qq.com/sns/jscode2session";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
    }
}
