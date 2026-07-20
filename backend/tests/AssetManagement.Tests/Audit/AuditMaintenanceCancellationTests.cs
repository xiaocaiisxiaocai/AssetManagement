using AssetManagement.Infrastructure.Audit;
using FluentAssertions;

namespace AssetManagement.Tests.Audit;

public class AuditMaintenanceCancellationTests : MySqlFixtureBase
{
    [Fact]
    public async Task Cleanup_honors_already_cancelled_token()
    {
        var service = new AuditMaintenanceService(_db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = () => service.CleanupAsync(7, cancellationToken: cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
