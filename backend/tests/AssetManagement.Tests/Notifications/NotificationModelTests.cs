using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Migrations;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;

namespace AssetManagement.Tests.Notifications;

public class NotificationModelTests
{
    [Fact]
    public void Notification_indexes_cover_list_and_unread_query_shapes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "Server=localhost;Database=notification_model_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(5, 7)))
            .Options;
        using var db = new AppDbContext(options);

        var indexes = db.Model.FindEntityType(typeof(Notification))!
            .GetIndexes()
            .Select(index => index.Properties.Select(property => property.Name).ToArray())
            .ToList();

        indexes.Should().Contain(properties => properties.SequenceEqual(new[]
        {
            nameof(Notification.UserId),
            nameof(Notification.IsRead)
        }));
        indexes.Should().Contain(properties => properties.SequenceEqual(new[]
        {
            nameof(Notification.UserId),
            nameof(Notification.CreatedAt),
            nameof(Notification.Id)
        }));
        indexes.Should().Contain(properties => properties.SequenceEqual(new[]
        {
            nameof(Notification.UserId),
            nameof(Notification.IsRead),
            nameof(Notification.CreatedAt),
            nameof(Notification.Id)
        }));
        db.Database.GetMigrations()
            .Should().Contain("20260721120000_AddNotificationQueryIndexes");
    }

    [Fact]
    public void Notification_index_migration_never_drops_the_foreign_key_backing_index()
    {
        var migration = new AddNotificationQueryIndexes();
        var up = Operations(migration, "Up");
        up.OfType<CreateIndexOperation>().Should().ContainSingle(operation =>
            operation.Name == "IX_notifications_UserId_CreatedAt_Id");
        up.OfType<CreateIndexOperation>().Should().ContainSingle(operation =>
            operation.Name == "IX_notifications_UserId_IsRead_CreatedAt_Id");
        up.OfType<DropIndexOperation>().Should().BeEmpty(
            "InnoDB 外键可能绑定到旧物理索引，不能尝试删除它");

        var down = Operations(migration, "Down");
        down.OfType<DropIndexOperation>().Should().ContainSingle(operation =>
            operation.Name == "IX_notifications_UserId_CreatedAt_Id");
        down.OfType<DropIndexOperation>().Should().ContainSingle(operation =>
            operation.Name == "IX_notifications_UserId_IsRead_CreatedAt_Id");
        down.OfType<CreateIndexOperation>().Should().BeEmpty(
            "旧索引从未删除，回滚时不应重复创建");

        using var db = CreateContext();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        var upSql = string.Join(Environment.NewLine,
            generator.Generate(up, db.Model).Select(command => command.CommandText));
        upSql.Should().NotContain("DROP INDEX `IX_notifications_UserId_IsRead`");
    }

    private static List<MigrationOperation> Operations(Migration migration, string methodName)
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        return builder.Operations.ToList();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "Server=localhost;Database=notification_model_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(5, 7)))
            .Options;
        return new AppDbContext(options);
    }
}
