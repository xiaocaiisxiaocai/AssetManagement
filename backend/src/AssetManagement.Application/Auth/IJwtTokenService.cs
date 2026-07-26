namespace AssetManagement.Application.Auth;

public interface IJwtTokenService
{
    /// <summary>
    /// 签发 token。<paramref name="sessionStartedAtUnix"/> 为空时视为全新登录会话，
    /// 记录为本次签发时间；滑动续期时应传入原始登录时间，使 token 的绝对生命周期
    /// 可被追踪，不会因持续续期而无限延长有效期。
    /// </summary>
    string Create(
        int userId,
        string employeeNo,
        IEnumerable<string> permissionCodes,
        IEnumerable<string> roles,
        int? departmentId = null,
        int tokenVersion = 0,
        long? sessionStartedAtUnix = null);
}

