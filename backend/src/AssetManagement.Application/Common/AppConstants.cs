namespace AssetManagement.Application.Common;

/// <summary>
/// 应用程序常量配置
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// JWT 密钥最小长度（字节）
    /// </summary>
    public const int JwtKeyMinLength = 32;

    /// <summary>
    /// 单次资产导入最大行数
    /// </summary>
    public const int MaxImportRows = 1000;

    /// <summary>
    /// 分页查询最大页大小
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// 登录失败最大尝试次数
    /// </summary>
    public const int MaxLoginAttempts = 5;

    /// <summary>
    /// 登录失败锁定时长（分钟）
    /// </summary>
    public const int LoginLockoutMinutes = 10;

    /// <summary>
    /// 部门树缓存时长（分钟）
    /// </summary>
    public const int DepartmentTreeCacheMinutes = 5;

    /// <summary>
    /// 分类树缓存时长（分钟）
    /// </summary>
    public const int CategoryTreeCacheMinutes = 5;

    /// <summary>
    /// 慢查询阈值（毫秒）
    /// </summary>
    public const int SlowQueryThresholdMs = 1000;
}
