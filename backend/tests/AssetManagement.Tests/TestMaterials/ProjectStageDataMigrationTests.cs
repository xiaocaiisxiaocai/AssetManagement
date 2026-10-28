using AssetManagement.Infrastructure.Migrations;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;

namespace AssetManagement.Tests.TestMaterials;

public class ProjectStageDataMigrationTests
{
    [Fact]
    public void Incomplete_landing_data_migration_rewrites_blocking_projects_and_does_not_restore()
    {
        var migration = new RevertIncompleteLandingProjects();
        var up = Sql(migration, "Up");

        up.Should().Contain("UPDATE `test_projects` AS p");
        up.Should().Contain("p.`ProgressCode` = 'testing'");
        up.Should().Contain("p.`RowVersion` = p.`RowVersion` + 1");
        up.Should().Contain("p.`IsDeleted` = 0");
        up.Should().Contain("p.`ClosedDate` IS NULL");
        up.Should().Contain("LOWER(p.`ProgressCode`) = 'landing'");
        up.Should().Contain("`project_test_stages` AS s");
        up.Should().Contain("s.`IsDeleted` = 0");
        up.Should().Contain("s.`EndDate` IS NULL");
        up.Should().Contain("TRIM(REPLACE(REPLACE(REPLACE(s.`TestContent`");
        up.Should().Contain("TRIM(REPLACE(REPLACE(REPLACE(s.`TestResult`");
        up.Should().NotContain("CREATE TABLE");
        up.Should().NotContain("ALTER TABLE");
        Operations(migration, "Down").Should().BeEmpty();

        using var db = CreateContext();
        db.Database.GetMigrations().Should().Contain("20261027130000_RevertIncompleteLandingProjects");
    }

    private static string Sql(Migration migration, string methodName) =>
        string.Join('\n', Operations(migration, methodName).OfType<SqlOperation>().Select(operation => operation.Sql));

    private static List<MigrationOperation> Operations(Migration migration, string methodName)
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        return builder.Operations;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "Server=localhost;Database=project_stage_migration_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(5, 7)))
            .Options;
        return new AppDbContext(options);
    }
}
