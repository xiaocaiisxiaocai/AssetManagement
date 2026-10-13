namespace AssetManagement.Tests;

/// <summary>
/// 依赖 Windows 文件共享锁语义的测试。Linux 上已打开的文件仍可删除，CI 上跳过而不是误报失败。
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "依赖 Windows 文件锁语义，仅在 Windows 上运行";
        }
    }
}
