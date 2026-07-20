using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Common;
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

    [Fact]
    public async Task Deleted_project_keeps_followup_history_readable_but_never_writable_or_purgeable()
    {
        var owner = new User { EmployeeNo = "FOLLOW-OWNER", Name = "项目负责人", PasswordHash = "x" };
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();
        var project = new TestProject
        {
            Name = "已删除历史项目",
            Code = "DELETED-HISTORY",
            ProgressCode = TestProjectService.ProgressLanding,
            OwnerId = owner.Id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjects.Add(project);
        await _db.SaveChangesAsync();
        _db.TestProjectFollowups.Add(new TestProjectFollowup
        {
            ProjectId = project.Id,
            DueDate = BusinessClock.Today,
            Content = "历史跟进",
            FilledById = owner.Id,
            FilledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        (await service.ListFollowupsAsync(project.Id)).Should().ContainSingle();
        var dto = (await service.ListAsync("all", owner.Id)).Single(x => x.Id == project.Id);
        dto.CanWriteFollowUp.Should().BeFalse();
        var purge = () => service.PurgeAsync(project.Id);
        await purge.Should().ThrowAsync<BizException>().WithMessage("*跟进历史*");
    }

    [Fact]
    public async Task Monthly_stats_group_followups_by_china_business_date()
    {
        var year = BusinessClock.Now.Year;
        var owner = new User
        {
            EmployeeNo = "TZ-STATS-OWNER",
            Name = "跨时区统计负责人",
            PasswordHash = "x"
        };
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();
        var project = new TestProject
        {
            Name = "跨时区统计项目",
            Code = "TZ-STATS",
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjects.Add(project);
        await _db.SaveChangesAsync();
        _db.TestProjectFollowups.Add(new TestProjectFollowup
        {
            ProjectId = project.Id,
            DueDate = new DateTime(year, 1, 1),
            Content = "中国时间一月一日",
            FilledById = owner.Id,
            FilledAt = BusinessClock.ToUtc(new DateTime(year, 1, 1, 0, 30, 0)),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var stats = await CreateService().GetStatsAsync();

        stats.MonthlyStat.Single(x => x.Month == 1).FollowUpCount.Should().Be(1);
    }

    [Fact]
    public async Task Project_row_version_rejects_a_stale_concurrent_update()
    {
        var project = new TestProject
        {
            Name = "并发项目",
            Code = "CONCURRENT-PROJECT",
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjects.Add(project);
        await _db.SaveChangesAsync();

        await using var firstDb = CreateNoTrackingContext();
        await using var secondDb = CreateNoTrackingContext();
        var first = await firstDb.TestProjects.AsTracking().SingleAsync(x => x.Id == project.Id);
        var second = await secondDb.TestProjects.AsTracking().SingleAsync(x => x.Id == project.Id);

        first.Name = "先提交的名称";
        first.RowVersion++;
        await firstDb.SaveChangesAsync();

        second.Name = "过期提交的名称";
        second.RowVersion++;
        var staleSave = () => secondDb.SaveChangesAsync();
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verifyDb = CreateNoTrackingContext();
        (await verifyDb.TestProjects.SingleAsync(x => x.Id == project.Id)).Name.Should().Be("先提交的名称");
    }

    [Fact]
    public async Task Concurrent_option_create_returns_a_business_conflict_instead_of_database_error()
    {
        await using var firstDb = CreateNoTrackingContext();
        await using var secondDb = CreateNoTrackingContext();
        var first = new TestProjectService(firstDb);
        var second = new TestProjectService(secondDb);
        var request = new SaveTestProjectOptionRequest
        {
            Kind = TestProjectService.OptionKindProjectType,
            Code = "same-code",
            Label = "同编码",
            Sort = 1,
            IsActive = true
        };

        static async Task<Exception?> Capture(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(
            Capture(async () => await first.CreateOptionAsync(request)),
            Capture(async () => await second.CreateOptionAsync(request)));

        results.Count(x => x is null).Should().Be(1);
        results.Single(x => x is not null).Should().BeOfType<BizException>()
            .Which.Code.Should().Be(4094);
    }

    private TestProjectService CreateService() => new(CreateNoTrackingContext());
}
