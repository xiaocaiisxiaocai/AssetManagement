# M1 认证与 RBAC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development 或 superpowers:executing-plans。Steps 用 `- [ ]` 跟踪。**前置：M0 完成。**

**Goal:** 实现 JWT 登录、用户/角色/权限/菜单（RBAC），输出 vben 前端所需的认证/动态菜单/权限契约，接入审计切面，使 vben 用真实账号登录并按权限渲染菜单。

**Architecture:** Domain 定义 RBAC 实体；Infrastructure 负责持久化、JWT 签发与审计拦截器；Application 编排登录与权限解析；Api 暴露 vben 契约接口与 RBAC 管理接口。权限码 `模块:动作`，`[HasPermission]` 策略校验。

**Tech Stack:** EF Core, `Microsoft.AspNetCore.Authentication.JwtBearer`, `BCrypt.Net-Next`（密码哈希）。

---

## File Structure

- `Domain/Entities/`：`User.cs, Role.cs, Permission.cs, Menu.cs, UserRole.cs, RolePermission.cs, RoleMenu.cs`
- `Domain/Entities/AuditLog.cs`
- `Infrastructure/Persistence/Configurations/*Configuration.cs`（每实体 EF 配置）
- `Infrastructure/Persistence/Seed/DbSeeder.cs`（种子）
- `Infrastructure/Auth/JwtTokenService.cs` + `IJwtTokenService`（接口置于 Application）
- `Infrastructure/Auth/PermissionAuthorizationHandler.cs` + `HasPermissionAttribute`
- `Infrastructure/Audit/AuditActionFilter.cs`
- `Application/Auth/`：`AuthService.cs`、`LoginRequest/LoginResponse/UserInfoDto/RouteDto`
- `Api/Controllers/`：`AuthController.cs, MenuController.cs, UserController.cs, RoleController.cs, PermissionController.cs`
- `tests/.../Auth/`：`AuthServiceTests.cs, LoginApiTests.cs, PermissionPolicyTests.cs`

---

### Task 1: RBAC 实体与 EF 配置

**Files:** `Domain/Entities/*.cs`, `Infrastructure/Persistence/Configurations/*.cs`, `AppDbContext.cs`

- [ ] **Step 1: 定义实体**

```csharp
// User.cs
namespace AssetManagement.Domain.Entities;
public class User
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int? DepartmentId { get; set; }      // M2 关联 departments
    public int? SupervisorId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public List<UserRole> UserRoles { get; set; } = new();
}
```
```csharp
public class Role { public int Id {get;set;} public string Code{get;set;}="" ; public string Name{get;set;}=""; public bool IsActive{get;set;}=true; public List<RolePermission> RolePermissions{get;set;}=new(); public List<RoleMenu> RoleMenus{get;set;}=new(); }
public class Permission { public int Id{get;set;} public string Code{get;set;}=""; public string Name{get;set;}=""; public string? Module{get;set;} }
public class Menu { public int Id{get;set;} public int? ParentId{get;set;} public string Name{get;set;}=""; public string Title{get;set;}=""; public string? Path{get;set;} public string? Component{get;set;} public string? Icon{get;set;} public int Sort{get;set;} public string Type{get;set;}="menu"; /* menu|button */ public string? PermissionCode{get;set;} }
public class UserRole { public int UserId{get;set;} public User User{get;set;}=null!; public int RoleId{get;set;} public Role Role{get;set;}=null!; }
public class RolePermission { public int RoleId{get;set;} public Role Role{get;set;}=null!; public int PermissionId{get;set;} public Permission Permission{get;set;}=null!; }
public class RoleMenu { public int RoleId{get;set;} public Role Role{get;set;}=null!; public int MenuId{get;set;} public Menu Menu{get;set;}=null!; }
```
> 中间表带导航属性，后续可 `Users.Include(u=>u.UserRoles).ThenInclude(ur=>ur.Role).ThenInclude(r=>r.RolePermissions).ThenInclude(rp=>rp.Permission)` 一次取出用户全部权限码。EF 配置中复合主键 `HasKey(x=>new{x.UserId,x.RoleId})` 并配置两端关系。
```csharp
// AuditLog.cs
public class AuditLog { public int Id{get;set;} public int? UserId{get;set;} public string ActionType{get;set;}=""; public string? TargetType{get;set;} public string? TargetId{get;set;} public string Summary{get;set;}=""; public string? Detail{get;set;} public string? Ip{get;set;} public DateTime OccurredAt{get;set;} }
```

- [ ] **Step 2: EF 配置 + DbSet**

每实体一个 `IEntityTypeConfiguration`（`ToTable("users")` 等，复合主键 `UserRole(UserId,RoleId)`，唯一索引 `User.EmployeeNo`、`Role.Code`、`Permission.Code`）。在 `AppDbContext` 加 `DbSet<User> Users` 等。

- [ ] **Step 3: 迁移并建表**

```bash
dotnet ef migrations add Rbac -p backend/src/AssetManagement.Infrastructure -s backend/src/AssetManagement.Api
dotnet build backend/AssetManagement.sln
```
Expected: 编译通过，迁移含 7 张表 + audit_logs。

- [ ] **Step 4: Commit** `feat: add RBAC entities and EF configurations`

---

### Task 2: 种子数据

**Files:** `Infrastructure/Persistence/Seed/DbSeeder.cs`, `Program.cs`

- [ ] **Step 1: 实现种子（幂等）**

插入：权限码（`asset:view/create/edit/delete`、`approval:handle`、`admin:user`、`admin:role`、`workflow:design` 等）、菜单树（资产/审批/报表/系统管理对应 prototype 18+ 页面）、角色（`admin` 系统管理员、`warehouse` 仓库管理员、`supervisor` 部门主管、`employee` 普通员工、`dept_admin` 部门管理员）、用户 `admin/1001`（密码 `BCrypt.HashPassword("123456")`）、给 admin 角色全部权限+菜单。仅当表空时插入。

- [ ] **Step 2: 启动时调用**

`Program.cs` 迁移后：`DbSeeder.Seed(db);`

- [ ] **Step 3: 验证**

`dotnet run` 后用 DB 工具或下一个登录测试确认 `users` 有 admin。

- [ ] **Step 4: Commit** `feat: seed RBAC roles/permissions/menus and admin user`

---

### Task 3: JWT 服务与登录（TDD）

**Files:** `Application/Auth/IJwtTokenService.cs`, `Infrastructure/Auth/JwtTokenService.cs`, `Application/Auth/AuthService.cs`, DTOs, `Api/Controllers/AuthController.cs`, `appsettings.json`

- [ ] **Step 1: 安装 JWT/BCrypt 包**

```bash
dotnet add backend/src/AssetManagement.Api package Microsoft.AspNetCore.Authentication.JwtBearer -v 8.*
dotnet add backend/src/AssetManagement.Infrastructure package BCrypt.Net-Next
```

- [ ] **Step 2: 写 AuthService 登录失败测试**

```csharp
[Fact]
public async Task Login_with_wrong_password_throws_biz()
{
    var svc = BuildAuthService(seedUser: ("1001","123456"));
    var act = () => svc.LoginAsync(new LoginRequest{ EmployeeNo="1001", Password="bad" });
    await act.Should().ThrowAsync<BizException>();
}

[Fact]
public async Task Login_ok_returns_token()
{
    var svc = BuildAuthService(seedUser: ("1001","123456"));
    var res = await svc.LoginAsync(new LoginRequest{ EmployeeNo="1001", Password="123456" });
    res.Token.Should().NotBeNullOrEmpty();
}
```
（`BuildAuthService` 用临时 SQLite/`UseInMemoryDatabase` + 真实 BCrypt + 测试 JwtTokenService。）

- [ ] **Step 3: 跑测试确认失败** `dotnet test --filter AuthServiceTests` → FAIL。

- [ ] **Step 4: 实现 DTO/服务/接口**

```csharp
public record LoginRequest { public string EmployeeNo {get;init;}=""; public string Password {get;init;}=""; }
public record LoginResponse { public string Token {get;init;}=""; }

public interface IJwtTokenService { string Create(int userId, string employeeNo, IEnumerable<string> permissionCodes); }
```
`JwtTokenService.Create` 用 `JwtSecurityTokenHandler`，claims 含 `sub`、`employeeNo`、自定义 `perm`（逗号拼接或多条），密钥/有效期读 `appsettings`（`Jwt:Key/Issuer/ExpireMinutes`）。
`AuthService.LoginAsync`：查用户→`BCrypt.Verify`→失败抛 `BizException(4011,"工号或密码错误")`→查该用户角色的权限码集合→`_jwt.Create(...)`。

- [ ] **Step 5: AuthController.Login**

```csharp
[HttpPost("login")]
[AllowAnonymous]
public async Task<ApiResult<LoginResponse>> Login(LoginRequest req)
    => ApiResult<LoginResponse>.Ok(await _auth.LoginAsync(req));
```

- [ ] **Step 6: 配置 JWT 鉴权（Program.cs）**

`builder.Services.AddAuthentication(JwtBearerDefaults...).AddJwtBearer(...)`（校验 Issuer/Key），`app.UseAuthentication(); app.UseAuthorization();`。

- [ ] **Step 7: 跑测试通过 + 登录 API 集成测试**

```csharp
[Fact] public async Task Login_api_returns_token() {
  var res = await _client.PostAsJsonAsync("/api/auth/login", new { employeeNo="1001", password="123456" });
  var body = await res.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
  body!.Code.Should().Be(0); body.Data!.Token.Should().NotBeEmpty();
}
```
Run: `dotnet test --filter "AuthServiceTests|LoginApiTests"` → PASS。

- [ ] **Step 8: Commit** `feat: add JWT login (auth/login) with bcrypt and tests`

---

### Task 4: 权限策略 [HasPermission]

**Files:** `Infrastructure/Auth/HasPermissionAttribute.cs`, `PermissionAuthorizationHandler.cs`, `Program.cs`, Test `PermissionPolicyTests.cs`

- [ ] **Step 1: 写策略组件（完整代码）**

```csharp
// HasPermissionAttribute.cs
using Microsoft.AspNetCore.Authorization;
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = $"perm:{permission}";
}

// PermissionRequirement.cs
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string p) => Permission = p;
}

// PermissionPolicyProvider.cs —— 把 "perm:xxx" 动态解析为策略
using Microsoft.Extensions.Options;
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> o) => _fallback = new(o);
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("perm:"))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName.Substring("perm:".Length)))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}

// PermissionAuthorizationHandler.cs
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx, PermissionRequirement req)
    {
        var perms = ctx.User.FindAll("perm").Select(c => c.Value);
        if (ctx.User.IsInRole("admin") || perms.Contains(req.Permission))
            ctx.Succeed(req);
        return Task.CompletedTask; // 不 Succeed → 授权失败 → 框架返回 403
    }
}
```
JWT 签发时（`JwtTokenService.Create`）须为每个权限码写一条 `new Claim("perm", code)`，并写 `ClaimTypes.Role` 角色 claim（供 `IsInRole("admin")`）。

- [ ] **Step 1b: 注册（Program.cs）**

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();
```
未登录访问 `[Authorize]`/`[HasPermission]` 端点 → JWT 中间件返回 **401**；已登录但 handler 未 Succeed → **403**。

- [ ] **Step 2: 集成测试**

无 token 访问受保护端点 → 401；持 `asset:view` token 访问 `asset:view` 端点 → 通过；缺权限 → 403/业务 4031。

- [ ] **Step 3: 跑测试** → PASS。

- [ ] **Step 4: Commit** `feat: add HasPermission authorization policy`

---

### Task 5: vben 契约接口（user-info + 动态菜单）

**Files:** `Api/Controllers/AuthController.cs`, `MenuController.cs`, DTOs

> vben 登录后调用 user-info 取用户与权限码、调用菜单接口生成动态路由。响应结构需匹配 vben `api` 层（以 web 模板实际字段为准，下面给标准形态，对接时按需改键名）。

- [ ] **Step 1: user-info**

```csharp
[HttpGet("user-info")]
public async Task<ApiResult<UserInfoDto>> UserInfo() => ApiResult<UserInfoDto>.Ok(await _auth.GetUserInfoAsync(CurrentUserId));
// UserInfoDto { int Id; string Name; string EmployeeNo; string[] Roles; string[] Permissions; }
```

- [ ] **Step 2: 动态菜单/路由**

```csharp
[HttpGet("/api/menu/routes")]
public async Task<ApiResult<List<RouteDto>>> Routes() => ApiResult<List<RouteDto>>.Ok(await _auth.GetRoutesAsync(CurrentUserId));
// RouteDto { string Name; string Path; string Component; MetaDto Meta; List<RouteDto> Children; }
// MetaDto { string Title; string? Icon; int Order; string[]? Permissions; }
```
`GetRoutesAsync`：取用户角色可见的 `type=menu` 菜单，按 ParentId 组装树 → RouteDto；`type=button` 的 PermissionCode 归并到 Permissions。

- [ ] **Step 3: 集成测试**：admin 登录后 `user-info.Permissions` 非空、`menu/routes` 返回资产/审批/报表/系统管理顶层节点。 → PASS。

- [ ] **Step 4: Commit** `feat: add vben contracts user-info and dynamic routes`

---

### Task 6: 审计切面

**Files:** `Infrastructure/Audit/AuditActionFilter.cs`, `Program.cs`

- [ ] **Step 1: 实现 ActionFilter**

`OnActionExecuted`：仅对写方法（POST/PUT/DELETE）且响应成功时，异步写 `AuditLog`（user、actionType=HTTP 方法、targetType=控制器名、summary=路由、ip）。注册为全局 filter：`builder.Services.AddControllers(o => o.Filters.Add<AuditActionFilter>())`。

- [ ] **Step 2: 集成测试**：调用一个写接口后 `audit_logs` 新增一行。 → PASS。

- [ ] **Step 3: Commit** `feat: add audit action filter logging write operations`

---

### Task 7: RBAC 管理接口（用户/角色/权限/菜单 CRUD）

**Files:** `Api/Controllers/{User,Role,Permission,Menu}Controller.cs`, `Application/.../*Service.cs`, DTOs

> 用户 CRUD 给完整实现作为模板；**角色/权限/菜单按同一模式实现**（实体已定义、接口清单如下）。每个写接口加 `[HasPermission("admin:*")]`。

- [ ] **Step 1: 用户 CRUD（模板）**

接口：`GET /api/users`（分页+筛选 工号/姓名/角色 → `PagedResult<UserDto>`）、`POST /api/users`、`PUT /api/users/{id}`、`POST /api/users/{id}/reset-password`、`POST /api/users/{id}/toggle-status`。`UserService` 用 EF 实现；新增时 `BCrypt.HashPassword(默认=工号后6位)`；分配角色写 `UserRole`。
写 1 个集成测试：创建用户→列表能查到。

- [ ] **Step 2: 角色/权限/菜单 CRUD（明确 DTO 与接口，按用户模式实现）**

DTO（字段明确，避免漏）：
```csharp
public record RoleDto { public int Id; public string Code=""; public string Name=""; public bool IsActive; public int[] PermissionIds=Array.Empty<int>(); public int[] MenuIds=Array.Empty<int>(); }
public record PermissionDto { public int Id; public string Code=""; public string Name=""; public string? Module; }
public record MenuDto { public int Id; public int? ParentId; public string Name=""; public string Title=""; public string? Path; public string? Component; public string? Icon; public int Sort; public string Type="menu"; public string? PermissionCode; }
```
接口：
- 角色：`GET /api/roles`（分页）、`GET /api/roles/{id}`、`POST/PUT/DELETE /api/roles`、`PUT /api/roles/{id}/permissions`（body: int[] permissionIds → 重写 RolePermission）、`PUT /api/roles/{id}/menus`（int[] menuIds）。
- 权限：`GET /api/permissions`（列表/按 Module 分组）、`POST/PUT/DELETE /api/permissions`。
- 菜单：`GET /api/menus`（树，按 ParentId 组装）、`POST/PUT/DELETE /api/menus`。
每个实体写 1 个集成测试覆盖"创建 → 查询命中"；角色额外测"设权限后 user-info 的 Permissions 包含该权限码"。

- [ ] **Step 3: 跑全部测试** `dotnet test backend/...` → 全绿。

- [ ] **Step 4: Commit** `feat: add RBAC management APIs (users/roles/permissions/menus)`

---

### Task 8: vben 前端对接

**Files:** `web/` 前端 `api`、`.env`、登录与权限 store

- [ ] **Step 1: 对齐契约**：核对 vben 登录、user-info、菜单接口的请求/响应字段与后端一致；不一致时改前端 `api` 层适配（或回到 Task 5 调整后端键名，二选一，优先改后端键名匹配 vben 默认）。

- [ ] **Step 2: 联调登录**：`pnpm dev`，用 `1001/123456` 登录 → 进入主框架，左侧菜单按 `menu/routes` 渲染。

- [ ] **Step 3: 验证权限路由**：用仅含部分权限的角色账号登录，确认受限菜单/按钮隐藏。

- [ ] **Step 4: Commit** `feat: wire vben frontend to backend auth/menu/permission`

---

## 验收标准（M1 完成）

- `dotnet test` 全绿；Swagger 中 `auth/login`、`auth/user-info`、`menu/routes`、RBAC CRUD 可用且受 JWT 保护。
- vben 用真实账号登录，菜单与按钮按权限动态渲染；写操作进 `audit_logs`。

## Self-Review

- 覆盖 spec §6 对接契约、§5 RBAC 表、§8 M1 任务。
- 权限码/角色与需求文档 2.1/2.2、多部门预留（dept_admin）一致。
- `ApiResult/PagedResult/BizException` 复用 M0；`Program` partial 复用于集成测试。
- 角色/权限/菜单 CRUD 采用与用户 CRUD 相同模式（接口清单已列全，避免重复贴码）；codex 实现时照用户模板套用。
