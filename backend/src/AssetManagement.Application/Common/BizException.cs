namespace AssetManagement.Application.Common;

public class BizException : Exception
{
    public int Code { get; }
    public int? HttpStatusCode { get; }

    public BizException(int code, string message, int? httpStatusCode = null) : base(message)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }
}
