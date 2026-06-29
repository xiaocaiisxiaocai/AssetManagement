using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.TestMaterials;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.TestMaterials;

public class TestProjectServiceNoTrackingTests : MySqlFixtureBase
{
    [Fact]
    public async Task Update_option_persists_when_global_no_tracking_is_enabled()
    {
        var service = CreateService();
        var option = new TestProjectOption
        {
            Kind = TestProjectService.OptionKindProjectType,
            Code = "old",
            Label = "旧类型",
            Sort = 1,
            IsActive = true
        };
        _db.TestProjectOptions.Add(option);
        await _db.SaveChangesAsync();

        await service.UpdateOptionAsync(option.Id, new SaveTestProjectOptionRequest
        {
            Kind = TestProjectService.OptionKindProjectType,
            Code = "new",
            Label = "新类型",
            Sort = 2,
            IsActive = false
        });

        await using var verifyDb = CreateNoTrackingContext();
        var saved = await verifyDb.TestProjectOptions.AsNoTracking().SingleAsync(x => x.Id == option.Id);
        saved.Code.Should().Be("new");
        saved.Label.Should().Be("新类型");
        saved.Sort.Should().Be(2);
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_option_removes_row_when_global_no_tracking_is_enabled()
    {
        var service = CreateService();
        var option = new TestProjectOption
        {
            Kind = TestProjectService.OptionKindProgress,
            Code = "todo",
            Label = "待办",
            Sort = 1,
            IsActive = true
        };
        _db.TestProjectOptions.Add(option);
        await _db.SaveChangesAsync();

        await service.DeleteOptionAsync(option.Id);

        await using var verifyDb = CreateNoTrackingContext();
        var exists = await verifyDb.TestProjectOptions.AsNoTracking().AnyAsync(x => x.Id == option.Id);
        exists.Should().BeFalse();
    }

    private TestProjectService CreateService() => new(CreateNoTrackingContext());
}
