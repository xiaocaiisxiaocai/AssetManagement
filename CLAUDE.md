# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 仓库定位

部门资产管理系统的全栈实现仓库,包含四个并存的部分:

- `backend/` — **正式后端**:ASP.NET Core 8 + EF Core + SQLite,DDD 四层架构,JWT + 权限码鉴权,可配置审批工作流引擎。当前活跃开发对象。
- `web/` — **正式前端**:基于 vue-vben-admin 5.x 的 monorepo(pnpm + turbo)。实际开发的应用是 `apps/web-ele`(Vue 3 + Element Plus);`web-antd`、`web-naive` 为上游模板自带,**不使用**。
- `docs/` — 需求/设计/实施规划文档(`.md` 与 `.pdf` 并存,**修改以 `.md` 为准**)。审批与多部门设计见 `docs/审批工作流设计.md`、`docs/多部门预留设计.md`;路线图见 `docs/全栈实施规划.md` 与 `docs/plans/`。
- `prototype/` — 早期纯静态 HTML 原型(零依赖),仅作参考,新功能不在此实现。
- `deploy/` — 内网部署说明、生产配置样例、SQLite 备份脚本。

## 常用命令

后端(在仓库根目录执行):

```powershell
dotnet build .\backend\AssetManagement.sln
dotnet run --project backend\src\AssetManagement.Api      # 启动 API,默认 http://localhost:5000
dotnet test .\backend\tests\AssetManagement.Tests --no-build
dotnet test .\backend\tests\AssetManagement.Tests --filter "FullyQualifiedName~ApprovalApiTests"   # 单个测试类
dotnet test .\backend\tests\AssetManagement.Tests --filter "Name=Health_returns_ok"                 # 单个测试方法
```

EF Core 迁移(`dotnet-ef` 已固定 8.0.28,通过 `backend/dotnet-tools.json` 管理):

```powershell
dotnet ef migrations add <Name> --project backend\src\AssetManagement.Infrastructure --startup-project backend\src\AssetManagement.Api
# 无需手动 update:Program.cs 启动时自动 db.Database.Migrate() + DbSeeder.Seed()
```

前端(在 `web/` 目录执行,**必须用 pnpm**):

```powershell
pnpm install
pnpm -F @vben/web-ele dev                       # 开发服务器,端口 5777
pnpm -F @vben/web-ele run typecheck             # 类型检查
pnpm --filter @vben/web-ele... run build        # 构建(带 ... 以构建依赖包)
pnpm check                                       # monorepo 全局检查(圆形依赖/类型/拼写/dep 版本)
```

健康检查:`GET http://localhost:5000/api/health`。默认账号 `1001 / 123456`。

## 前后端集成约定(关键)

- **代理**:前端 `apps/web-ele/vite.config.mts` 将 `/api` 与 `/signalr-hubs` 代理到 `http://localhost:5000`。本地开发须先起后端再起前端。
- **统一响应体**:后端所有接口返回 `ApiResult<T>`(`AssetManagement.Application/Common/ApiResult.cs`),形如 `{ code, message, data }`,`code == 0` 为成功。前端响应拦截器(`apps/web-ele/src/api/request.ts`)按 HTTP 状态码解构。
- **鉴权**:登录返回 JWT,前端存入 access store,请求拦截器加 `Authorization: Bearer <token>` 头。401 触发登出。
- **动态路由/菜单**:菜单与权限由后端下发,**不在前端硬编码**。登录后 `GET /api/auth/user-info` 取用户信息、`GET /api/menus` 取菜单树;菜单的 `Component` 字段(如 `/admin/users/index`)映射到 `apps/web-ele/src/views` 下的页面。新增受权限控制的页面需同时在后端 `DbSeeder` 注册 Menu + Permission。
- **错误**:业务异常抛 `BizException(code, message)`,由 `ExceptionMiddleware` 统一转 `ApiResult`。

## 后端架构

DDD 四层,依赖方向 Api → Infrastructure → Application → Domain:

- **Domain**(`AssetManagement.Domain`):实体(`Entities/`)、领域服务(`Services/`,如资产编号 `AssetNoGenerator`、类别编码 `CategoryCodeService`)、**纯函数审批引擎**(`Workflow/`)。
- **Application**(`AssetManagement.Application`):服务接口(`I*Service`)、DTO、`Common/`(`ApiResult`、`BizException`、`PagedResult`)。仅定义契约与数据形状,不含实现。
- **Infrastructure**(`AssetManagement.Infrastructure`):服务实现、`Persistence/`(`AppDbContext` + `Configurations/` 每实体一个 `IEntityTypeConfiguration` + `Seed/DbSeeder`)、`Migrations/`、`Auth/`(JWT、权限策略)、`Audit/`(操作审计过滤器)。
- **Api**(`AssetManagement.Api`):瘦控制器,每个 action 一行调用对应 `I*Service`;`Program.cs` 注册所有 DI、JWT、Swagger、自定义权限策略。

新增一个后端功能模块的路径:Domain 加实体/领域逻辑 → Application 定义 `IXxxService` + DTO → Infrastructure 实现并加 `EntityTypeConfiguration` → `Program.cs` 注册 DI → Api 加瘦控制器 → 加迁移 → 在 `DbSeeder` 注册菜单/权限。

### 鉴权模型(RBAC + 权限码)

- 控制器 action 用 `[HasPermission("asset:view")]` 标注(继承 `AuthorizeAttribute`,Policy 名为 `perm:<code>`)。
- 自定义 `PermissionPolicyProvider` + `PermissionAuthorizationHandler` 动态解析策略,无需预注册每个权限。
- 权限码、角色、角色-权限/菜单映射的种子数据集中在 `DbSeeder`;权限矩阵参照需求文档。

### 审批工作流引擎(最复杂的子系统)

- **模板**(`Workflow` 实体)由设计器配置:节点列表(开始/审批/会签/或签/条件分支/抄送/结束)、审批人类型(指定用户/角色/直属上级/部门经理/发起人指定)。节点带 `X/Y` 画布坐标,**仅供前端 LogicFlow 设计器布局,执行引擎忽略**。
- **实例**(`ApprovalFlow` 实体)持有 `List<FlowInstanceNode>` 与 `CurrentNodeIndex`,记录每个节点的实时状态、会签 `SignStates`、加签 `AddedSigners`。
- **引擎**(`Domain/Workflow/WorkflowEngine.cs`)是**静态纯函数**(`Start`/`Approve`/`Reject`/`AddSign`/`Advance`):只推进内存中的流程状态,不碰数据库。
- **编排**(`Infrastructure/Workflow/WorkflowService.cs`)负责加载/持久化实例、调用引擎、并通过 `BizEffectApplier` 在流程通过时落地业务副作用(如资产状态变更)。
- 审批通过即推进到下一节点;会签需全部签署才前进,或签任一签署即可。改动引擎逻辑务必同步 `Workflow/WorkflowEngineTests.cs` 与 `Workflow/ApprovalApiTests.cs`。

## 后端测试

- xUnit + FluentAssertions;集成测试用 `Microsoft.AspNetCore.Mvc.Testing` 的 `TestWebAppFactory`。
- **每个测试类用独立的 in-memory SQLite**(见提交 `b1f3915`,修复共享文件库导致的 flaky),避免跨类数据污染。
- 纯领域逻辑(引擎、编号生成、类别编码)有独立单元测试,无需 Web 工厂。

## 前端约定(apps/web-ele)

- 业务代码在 `apps/web-ele/src` 下:`api/`(按模块分文件,封装 `requestClient` 调用)、`views/`(各模块对应菜单路由)、`store/`(Pinia,登录逻辑在 `store/auth.ts`)。
  - `views/asset/` — 资产管理(库存、出入库、盘点)
  - `views/approval/` — 审批流程(申请/审核)
  - `views/report/` — 报表统计
  - `views/admin/` — 基础数据(部门、用户、角色、权限)
  - `views/system/` — 系统设置(菜单、参数、操作日志)
- 复用上游 `@vben/*`、`@core/*` 包的能力(布局、请求客户端、preferences、stores),不要重复造轮子;改动 `web/packages/` 下的核心包会影响所有 app,需谨慎。
- 提交前跑 `pnpm -F @vben/web-ele run typecheck`;monorepo 根有 `pnpm check`(circular/dep/type/cspell)。

## 常见开发场景速查

| 场景 | 步骤 |
|------|------|
| **添加新的审批流程节点类型** | (1) Domain: `Workflow/NodeType.cs` 加枚举值 (2) Domain: `WorkflowEngine.cs` 加分支逻辑 (3) Infrastructure: `WorkflowService.cs` 实现审批人解析 (4) 测试: `WorkflowEngineTests.cs` 覆盖新逻辑 |
| **新增资产分类** | (1) Domain: `Category` 实体(含编码生成逻辑) (2) Application: `ICategoryService` + DTO (3) Infrastructure: 实现 + EntityTypeConfiguration (4) DbSeeder: 种子数据 (5) Api: 控制器 (6) 迁移 (7) 前端页面 (`views/admin/category`) + 菜单注册 |
| **后端新增权限** | (1) DbSeeder: `Permission` 表加行 + 角色-权限映射 (2) Api: action 标注 `[HasPermission("code")]` (3) 迁移 (4) 前端菜单由后端下发,无需改前端代码 |
| **前端新页面映射后端菜单** | (1) 后端 DbSeeder 注册 Menu(name/path/component) + Permission (2) 前端在 `views/<module>/` 创建页面,Component 路径须与后端 menu.Component 一致 (3) 登录后菜单自动下发,无需硬编码路由 |
| **修改审批工作流逻辑** | (1) 修改 `WorkflowEngine.cs`(纯函数) (2) 同步 `WorkflowEngineTests.cs` + `ApprovalApiTests.cs` (3) 如需新增数据表字段,加 EF 迁移 + WorkflowService 调用 |

## 编码与提交约定

- **路径分隔符**:Windows 环境下文件路径用反斜杠 `\`。
- 后端 C#:`Nullable` + `ImplicitUsings` 开启;控制器保持瘦,逻辑下沉到 service。
- 前端:TypeScript + Vue 3 `<script setup>`;遵循上游 Vben 的 ESLint/Prettier/Stylelint 配置。
- 界面文案、文档、提交说明均用中文。提交遵循 Conventional Commits(如 `feat(web): ...`、`fix: ...`、`test: ...`)。
- **不提交**:SQLite 库文件(`*.db`)、`web/dist/`、`dist.zip`、`bin/`、`obj/`、日志、真实员工数据、生产凭据、内网地址。
- 生产部署必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key` 占位符。
