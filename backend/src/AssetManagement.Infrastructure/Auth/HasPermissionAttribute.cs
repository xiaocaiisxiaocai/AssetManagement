using Microsoft.AspNetCore.Authorization;

namespace AssetManagement.Infrastructure.Auth;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"perm:{permission}";
    }
}

