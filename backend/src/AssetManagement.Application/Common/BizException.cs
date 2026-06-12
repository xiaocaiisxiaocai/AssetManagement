namespace AssetManagement.Application.Common;

public class BizException : Exception
{
    public int Code { get; }

    public BizException(int code, string message) : base(message)
    {
        Code = code;
    }
}
