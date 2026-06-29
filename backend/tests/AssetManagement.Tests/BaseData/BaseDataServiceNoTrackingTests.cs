using AssetManagement.Application.BaseData;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.BaseData;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetManagement.Tests.BaseData;

public class BaseDataServiceNoTrackingTests : MySqlFixtureBase
{
    [Fact]
    public async Task Update_department_persists_when_global_no_tracking_is_enabled()
    {
        var service = CreateService();
        var department = new Department { Code = "D9001", Name = "原部门", IsActive = true };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        await service.UpdateDepartmentAsync(department.Id, new UpdateDepartmentRequest
        {
            Name = "新部门",
            IsActive = false
        });

        await using var verifyDb = CreateNoTrackingContext();
        var saved = await verifyDb.Departments.AsNoTracking().SingleAsync(x => x.Id == department.Id);
        saved.Name.Should().Be("新部门");
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_location_persists_when_global_no_tracking_is_enabled()
    {
        var service = CreateService();
        var location = new Location { Name = "原库位" };
        _db.Locations.Add(location);
        await _db.SaveChangesAsync();

        await service.UpdateLocationAsync(location.Id, new UpdateLocationRequest
        {
            Name = "新库位"
        });

        await using var verifyDb = CreateNoTrackingContext();
        var saved = await verifyDb.Locations.AsNoTracking().SingleAsync(x => x.Id == location.Id);
        saved.Name.Should().Be("新库位");
    }

    [Fact]
    public async Task Save_settings_updates_existing_value_when_global_no_tracking_is_enabled()
    {
        var service = CreateService();
        var setting = new SystemSetting { Key = "base_data_test", Value = "old", Description = "旧说明" };
        _db.SystemSettings.Add(setting);
        await _db.SaveChangesAsync();

        await service.SaveSettingsAsync(new[]
        {
            new SaveSystemSettingRequest
            {
                Key = setting.Key,
                Value = "new",
                Description = "新说明"
            }
        });

        await using var verifyDb = CreateNoTrackingContext();
        var saved = await verifyDb.SystemSettings.AsNoTracking().SingleAsync(x => x.Key == setting.Key);
        saved.Value.Should().Be("new");
        saved.Description.Should().Be("新说明");
    }

    private BaseDataService CreateService()
        => new(CreateNoTrackingContext(), new MemoryCache(new MemoryCacheOptions()));
}
