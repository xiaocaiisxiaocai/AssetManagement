using System.Text.Json;
using AssetManagement.Api.Middleware;
using AssetManagement.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetManagement.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    [Theory]
    [InlineData(4001, 400)]
    [InlineData(4004, 404)]
    [InlineData(1002, 400)]
    [InlineData(4030, 403)]
    [InlineData(4048, 404)]
    [InlineData(4010, 404)]
    [InlineData(4011, 409)]
    [InlineData(4016, 403)]
    [InlineData(4051, 422)]
    [InlineData(4056, 409)]
    [InlineData(4060, 404)]
    [InlineData(4090, 409)]
    [InlineData(4130, 413)]
    [InlineData(4150, 415)]
    [InlineData(4291, 429)]
    [InlineData(500, 500)]
    public async Task Business_errors_use_meaningful_http_status_without_changing_api_result(
        int businessCode,
        int expectedStatus)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new BizException(businessCode, "业务错误"),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiResult<object?>>(context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().NotBeNull();
        body!.Code.Should().Be(businessCode);
        body.Message.Should().Be("业务错误");
    }

    [Fact]
    public async Task Explicit_http_status_resolves_ambiguous_business_codes()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new BizException(4011, "工号或密码错误", StatusCodes.Status401Unauthorized),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Unhandled_errors_return_http_500_and_hide_exception_detail()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException("sensitive detail"),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("message").GetString().Should().Be("服务器内部错误");
        body.RootElement.ToString().Should().NotContain("sensitive detail");
    }
}
