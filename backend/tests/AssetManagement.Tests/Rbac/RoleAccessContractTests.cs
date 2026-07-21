using System.Reflection;
using AssetManagement.Api.Controllers;
using AssetManagement.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Tests.Rbac;

public class RoleAccessContractTests
{
    [Fact]
    public void Assignment_catalog_requires_both_delegated_role_permissions_only()
    {
        var action = typeof(RoleController).GetMethod(nameof(RoleController.AccessOptions));

        action.Should().NotBeNull();
        action!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("access-options");
        action.GetCustomAttributes<HasPermissionAttribute>()
            .Select(x => x.Policy)
            .Should().BeEquivalentTo(
                "perm:role:assign-permission",
                "perm:role:assign-menu");
    }
}
