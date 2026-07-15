using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _svc;

    public NotificationController(INotificationService svc) => _svc = svc;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet]
    public async Task<ApiResult<List<NotificationDto>>> List([FromQuery] bool unreadOnly = false)
        => ApiResult<List<NotificationDto>>.Ok(await _svc.GetMyNotificationsAsync(CurrentUserId, unreadOnly));

    [HttpGet("unread-count")]
    public async Task<ApiResult<int>> UnreadCount()
        => ApiResult<int>.Ok(await _svc.GetUnreadCountAsync(CurrentUserId));

    [HttpPost("{id:int}/read")]
    public async Task<ApiResult<object?>> MarkRead(int id)
    {
        await _svc.MarkReadAsync(id, CurrentUserId);
        return ApiResult.Ok();
    }

    [HttpPost("read-all")]
    public async Task<ApiResult<object?>> MarkAllRead()
    {
        await _svc.MarkAllReadAsync(CurrentUserId);
        return ApiResult.Ok();
    }

    [HttpDelete]
    public async Task<ApiResult<object?>> Clear()
    {
        await _svc.ClearAsync(CurrentUserId);
        return ApiResult.Ok();
    }
}
