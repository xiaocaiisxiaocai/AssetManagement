namespace AssetManagement.Application.Common;

/// <summary>
/// 分页参数与偏移量的统一边界处理。
/// </summary>
public static class Pagination
{
    public static (int Page, int PageSize) Normalize(
        int page,
        int pageSize,
        int maxPageSize = AppConstants.MaxPageSize)
        => (Math.Max(page, 1), Math.Clamp(pageSize, 1, maxPageSize));

    /// <summary>
    /// 返回可安全传给 EF Core Skip 的偏移量；目标页超出结果集时返回 null。
    /// 先用 long 计算，避免 (page - 1) * pageSize 的 int 溢出回绕到前面的数据。
    /// </summary>
    public static int? GetOffset(int page, int pageSize, int total)
    {
        var offset = ((long)page - 1L) * pageSize;
        return offset >= total ? null : (int)offset;
    }
}
