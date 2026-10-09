using AssetManagement.Infrastructure.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task Lagging_sequence_is_raised_to_the_existing_maximum()
    {
        await using var db = CreateNoTrackingContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO business_sequences (SequenceKey, NextValue) VALUES ({"lagging-regression"}, {2})");

        await using var tx = await db.Database.BeginTransactionAsync();
        var value = await BusinessSequenceGenerator.NextAsync(db, "lagging-regression", 9);
        await tx.CommitAsync();

        value.Should().Be(10);
        var stored = await db.Database.SqlQuery<int>(
            $"SELECT NextValue AS Value FROM business_sequences WHERE SequenceKey = {"lagging-regression"}")
            .SingleAsync();
        stored.Should().Be(11);
    }
}
