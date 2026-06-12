namespace AssetManagement.Application.Common;

public class ApiResult<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string message = "ok") => new()
    {
        Code = 0,
        Message = message,
        Data = data
    };

    public static ApiResult<T> Fail(int code, string message) => new()
    {
        Code = code,
        Message = message
    };
}

public static class ApiResult
{
    public static ApiResult<object?> Ok(string message = "ok") => new()
    {
        Code = 0,
        Message = message
    };
}
