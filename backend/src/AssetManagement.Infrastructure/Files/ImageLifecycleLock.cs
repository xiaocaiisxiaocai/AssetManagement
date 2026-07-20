namespace AssetManagement.Infrastructure.Files;

/// <summary>
/// 本地附件存储的生命周期互斥锁。实体建立图片引用和孤儿文件清理必须共用此锁，
/// 以保证“验证文件存在 + 保存引用”与“确认无引用 + 删除文件”不会交错。
/// </summary>
internal static class ImageLifecycleLock
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        return new Releaser();
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                Gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
