namespace Zhuanzhu.WebApi.Models
{
    /// <summary>统一响应结构：code=0 表示成功。</summary>
    public class ApiResult
    {
        public int Code { get; set; }

        public string Message { get; set; } = "ok";

        public object Data { get; set; }

        public static ApiResult Ok(object data = null)
        {
            return new ApiResult { Code = 0, Message = "ok", Data = data };
        }

        public static ApiResult Fail(int code, string message)
        {
            return new ApiResult { Code = code, Message = message, Data = null };
        }
    }
}
