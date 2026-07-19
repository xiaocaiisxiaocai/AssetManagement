using AssetManagement.Infrastructure.Common;
using FluentAssertions;

namespace AssetManagement.Tests.TestMaterials;

public class BusinessSequenceGeneratorTests : MySqlFixtureBase
{
    [Fact]
    public async Task Parallel_connections_allocate_unique_continuous_numbers()
    {
        const int count = 20;
        var tasks = Enumerable.Range(0, count).Select(async _ =>
        {
            await using var db = CreateNoTrackingContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var value = await BusinessSequenceGenerator.NextAsync(db, "parallel-regression", 0);
            await tx.CommitAsync();
            return value;
        });

        var values = await Task.WhenAll(tasks);

        values.Should().OnlyHaveUniqueItems();
        values.Order().Should().Equal(Enumerable.Range(1, count));
    }
}
