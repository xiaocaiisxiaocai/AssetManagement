using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Rbac;

public class AssetOverviewMenuTests : MySqlFixtureBase
{
    [Fact]
    public void Upgrade_adds_overview_before_list_and_preserves_later_role_customization()
    {
        DbSeeder.Seed(_db);
        var original = _db.Menus.Single(x => x.Name == "AssetOverview");
        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.MenuId == original.Id));
        _db.Menus.Remove(original);
        _db.SaveChanges();

        var list = _db.Menus.Single(x => x.Name == "AssetList");
        var grantedRoles = _db.RoleMenus.Where(x => x.MenuId == list.Id)
            .Where(x => _db.RolePermissions.Any(rp => rp.RoleId == x.RoleId && rp.Permission.Code == "asset:view"))
            .Select(x => x.RoleId).OrderBy(x => x).ToList();
        using (var context = CreateNoTrackingContext()) DbSeeder.SeedAssetOverviewMenu(context);

        _db.ChangeTracker.Clear();
        var menu = _db.Menus.Single(x => x.Name == "AssetOverview");
        menu.ParentId.Should().Be(list.ParentId);
        menu.Sort.Should().BeLessThan(_db.Menus.Single(x => x.Name == "AssetList").Sort);
        menu.Path.Should().Be("/asset/overview");
        menu.Component.Should().Be("/asset/overview/index");
        menu.PermissionCode.Should().Be("asset:view");
        _db.RoleMenus.Where(x => x.MenuId == menu.Id).Select(x => x.RoleId).OrderBy(x => x)
            .Should().Equal(grantedRoles);

        var removedGrant = _db.RoleMenus.First(x => x.MenuId == menu.Id);
        _db.RoleMenus.Remove(removedGrant);
        _db.SaveChanges();
        using (var context = CreateNoTrackingContext()) DbSeeder.SeedAssetOverviewMenu(context);

        _db.Menus.Count(x => x.Name == "AssetOverview").Should().Be(1);
        _db.RoleMenus.Any(x => x.RoleId == removedGrant.RoleId && x.MenuId == menu.Id).Should().BeFalse();
    }

    [Fact]
    public void Roles_without_asset_list_access_do_not_receive_overview_access()
    {
        DbSeeder.Seed(_db);
        var overview = _db.Menus.Single(x => x.Name == "AssetOverview");
        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.MenuId == overview.Id));
        _db.Menus.Remove(overview);
        var role = new Role { Code = "overview-isolated", Name = "无资产菜单角色" };
        _db.Roles.Add(role);
        _db.SaveChanges();
        var permission = _db.Permissions.Single(x => x.Code == "asset:view");
        _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        _db.SaveChanges();

        DbSeeder.SeedAssetOverviewMenu(_db);

        var menu = _db.Menus.Single(x => x.Name == "AssetOverview");
        _db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == menu.Id).Should().BeFalse();
    }
}
