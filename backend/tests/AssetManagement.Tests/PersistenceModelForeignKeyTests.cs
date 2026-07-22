using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests;

public class PersistenceModelForeignKeyTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PersistenceModelForeignKeyTests(TestWebAppFactory factory) => _factory = factory;

    public static TheoryData<Type, string, Type> RequiredForeignKeys => new()
    {
        { typeof(Asset), nameof(Asset.CategoryId), typeof(AssetCategory) },
        { typeof(Asset), nameof(Asset.DepartmentId), typeof(Department) },
        { typeof(Asset), nameof(Asset.CustodianId), typeof(User) },
        { typeof(TestMaterial), nameof(TestMaterial.DepartmentId), typeof(Department) },
        { typeof(TestMaterial), nameof(TestMaterial.CustodianId), typeof(User) },
        { typeof(TestProject), nameof(TestProject.OwnerId), typeof(User) },
        { typeof(TestProjectFollowup), nameof(TestProjectFollowup.FilledById), typeof(User) },
        { typeof(ApprovalFlow), nameof(ApprovalFlow.WorkflowId), typeof(AssetManagement.Domain.Entities.Workflow) },
        { typeof(ApprovalFlow), nameof(ApprovalFlow.AssetId), typeof(Asset) },
        { typeof(ApprovalFlow), nameof(ApprovalFlow.ApplicantId), typeof(User) },
        { typeof(ApprovalFlow), nameof(ApprovalFlow.TransfereeId), typeof(User) },
        { typeof(FlowRecord), nameof(FlowRecord.FlowId), typeof(ApprovalFlow) },
        { typeof(MaterialFlow), nameof(MaterialFlow.WorkflowId), typeof(AssetManagement.Domain.Entities.Workflow) },
        { typeof(MaterialFlow), nameof(MaterialFlow.MaterialId), typeof(TestMaterial) },
        { typeof(MaterialFlow), nameof(MaterialFlow.ApplicantId), typeof(User) },
        { typeof(MaterialFlow), nameof(MaterialFlow.TransfereeId), typeof(User) },
        { typeof(MaterialFlowRecord), nameof(MaterialFlowRecord.FlowId), typeof(MaterialFlow) },
        { typeof(User), nameof(User.DepartmentId), typeof(Department) },
        { typeof(User), nameof(User.SupervisorId), typeof(User) },
        { typeof(Department), nameof(Department.ParentId), typeof(Department) },
        { typeof(Department), nameof(Department.ManagerId), typeof(User) },
        { typeof(AssetCategory), nameof(AssetCategory.ParentId), typeof(AssetCategory) },
        { typeof(Menu), nameof(Menu.ParentId), typeof(Menu) },
        { typeof(AuditLog), nameof(AuditLog.UserId), typeof(User) },
        { typeof(Notification), nameof(Notification.UserId), typeof(User) },
    };

    [Theory]
    [MemberData(nameof(RequiredForeignKeys))]
    public void Model_has_restrict_foreign_key(Type dependentType, string propertyName, Type principalType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entityType = db.Model.FindEntityType(dependentType)!;

        var foreignKey = entityType.GetForeignKeys().SingleOrDefault(x =>
            x.Properties.Count == 1
            && x.Properties[0].Name == propertyName
            && x.PrincipalEntityType.ClrType == principalType);

        foreignKey.Should().NotBeNull($"{dependentType.Name}.{propertyName} 应由数据库外键保护");
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void Notification_flow_id_remains_polymorphic_without_foreign_key()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entityType = db.Model.FindEntityType(typeof(Notification))!;

        entityType.GetForeignKeys().SelectMany(x => x.Properties).Select(x => x.Name)
            .Should().NotContain(nameof(Notification.FlowId));
    }
}
