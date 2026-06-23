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

- **代理**:前端 `apps/web-ele/vite.config.mts` 将 `/api` 代理到 `http://localhost:5000`。本地开发须先起后端再起前端。
- **统一响应体**:后端所有接口返回 `ApiResult<T>`(`AssetManagement.Application/Common/ApiResult.cs`),形如 `{ code, message, data }`,`code == 0` 为成功。前端响应拦截器(`apps/web-ele/src/api/request.ts`)按 HTTP 状态码解构。
- **鉴权**:登录返回 JWT,前端存入 access store,请求拦截器加 `Authorization: Bearer <token>` 头。401 触发登出。
- **动态路由/菜单**:菜单与权限由后端下发,**不在前端硬编码**。登录后 `GET /api/auth/user-info` 取用户信息、`GET /api/menu/routes`(`MenuController`,对应前端 `api/core/menu.ts` 的 `getAllMenusApi`)取当前用户的动态路由树;菜单的 `Component` 字段(如 `/admin/users/index`)映射到 `apps/web-ele/src/views` 下的页面。**注意区分**:`GET /api/menus`(`RbacMenuController`)是后台菜单 CRUD 管理接口,**不是**动态路由来源。新增受权限控制的页面需同时在后端 `DbSeeder` 注册 Menu + Permission。
- **错误**:业务异常抛 `BizException(code, message)`,由 `ExceptionMiddleware` 统一转 `ApiResult`。

## 后端架构

DDD 四层,依赖方向 Api → Infrastructure → Application → Domain:

- **Domain**(`AssetManagement.Domain`):实体(`Entities/`)、领域服务(`Services/`,如资产编号 `AssetNoGenerator`、类别编码 `CategoryCodeService`)、**纯函数审批引擎**(`Workflow/`)。
- **Application**(`AssetManagement.Application`):服务接口(`I*Service`)、DTO、`Common/`(`ApiResult`、`BizException`、`PagedResult`)。仅定义契约与数据形状,不含实现。服务按**限界上下文**粗粒度划分(**非每实体一个**):`IAssetService`(资产 CRUD + 详情/流转时间线 + Excel 批量导入)、`IAuthService`/`IJwtTokenService`、`IBaseDataService`(部门/分类/位置)、`IRbacService`(用户/角色/权限/菜单)、`IWorkflowService`(工作流 + 审批)、`IReportService`(汇总/借用/逾期报表)、`IFileStorageService`(文件存储,见 `Files/`)、`IAuditQueryService`(审计日志查询,见 `Audit/`)。
- **Infrastructure**(`AssetManagement.Infrastructure`):服务实现、`Persistence/`(`AppDbContext` + `Configurations/` 每实体一个 `IEntityTypeConfiguration` + `Seed/DbSeeder`)、`Migrations/`、`Auth/`(JWT、权限策略)、`Audit/`(操作审计过滤器 + 审计查询)、`Reports/`(报表服务)、`Files/`(文件存储服务 `FileStorageService`,支持资产图片上传;在 `Program.cs` 注册为 **Singleton**,上传根目录由 `Attachment:Path` 配置,默认 `App_Data/uploads`)。
- **Api**(`AssetManagement.Api`):瘦控制器,每个 action 一行调用对应 `I*Service`;`Program.cs` 注册所有 DI、JWT、Swagger、自定义权限策略。

新增一个后端功能模块的路径:Domain 加实体/领域逻辑 → Application 定义 `IXxxService` + DTO → Infrastructure 实现并加 `EntityTypeConfiguration` → `Program.cs` 注册 DI → Api 加瘦控制器 → 加迁移 → 在 `DbSeeder` 注册菜单/权限。

### 鉴权模型(RBAC + 权限码)

- 控制器 action 用 `[HasPermission("asset:view")]` 标注(继承 `AuthorizeAttribute`,Policy 名为 `perm:<code>`)。
- 自定义 `PermissionPolicyProvider` + `PermissionAuthorizationHandler` 动态解析策略,无需预注册每个权限。
- 权限码、角色、角色-权限/菜单映射的种子数据集中在 `DbSeeder`;权限矩阵参照需求文档。

### 多部门数据隔离(已实现)

- **JWT增强**:登录时将用户的 `DepartmentId` 写入 JWT token 的 `departmentId` claim。
- **数据隔离逻辑**(`AssetService.ApplyQuery`):
  - 超级管理员(`admin` 角色):无限制,查看全部资产
  - 部门管理员(`dept_admin` 角色且非 `admin`):自动过滤,只能查看本部门+子部门的资产
  - 普通员工:无限制(共享资产池模式)
- **实现方式**:通过 `IHttpContextAccessor` 获取当前用户的角色和部门信息,在 EF 查询条件中自动附加 `DepartmentId` 过滤。
- 参考设计文档:`docs/多部门预留设计.md`。

### 审批工作流引擎（BPMN 2.0）

**2026-06-22 重大升级**: 从简单线性引擎升级到标准 BPMN 2.0 工作流引擎。

#### 核心架构

- **模板**(`Workflow` 实体)：存储标准 BPMN 2.0 XML（`BpmnXml` 字段），由 bpmn-js 设计器可视化编辑。
- **解析器**(`Domain/Workflow/BpmnParser.cs`)：完整解析 BPMN XML，支持 UserTask、ServiceTask、StartEvent、EndEvent、ExclusiveGateway、ParallelGateway、InclusiveGateway 等标准元素。
- **执行引擎**(`Domain/Workflow/BpmnEngine.cs`)：Token 驱动的纯函数引擎，支持并行流程执行。
- **实例**(`ApprovalFlow` 实体)：实现 `IBpmnFlowInstance` 接口，持有 `CurrentNodeIds`（活跃节点列表）和 `BpmnTokens`（Token 状态字典）。
- **编排**(`Infrastructure/Workflow/WorkflowService.cs`)：加载 BPMN 定义、解析、执行、持久化，并在流程完成时落地业务副作用。

#### 支持的 BPMN 元素

- **UserTask**: 审批节点，使用 `camunda:assignee` 配置审批人（支持 `supervisor`、`deptManager`、用户 ID、角色代码）
- **ExclusiveGateway**: 排他网关，根据条件选择一条分支（条件表达式：`${amount} > 5000`）
- **ParallelGateway**: 并行网关，所有分支同时执行
- **InclusiveGateway**: 包容网关，执行所有满足条件的分支
- **SequenceFlow**: 连线，可配置条件表达式

#### 前端设计器

- **位置**: `web/apps/web-ele/src/views/admin/workflows/`
- **设计器**: bpmn-modeler.vue（bpmn-js，CDN 加载）
- **属性面板**: bpmn-properties.vue（配置审批人、条件表达式）
- **列表页**: index.vue（管理工作流定义）

#### 审批流程

1. 用户发起审批 → 后端调用 `BpmnEngine.Start()` → Token 推进到第一个 UserTask
2. 审批人审批 → 后端调用 `BpmnEngine.Approve()` → Token 根据流程定义推进
3. 遇到网关 → 自动评估条件表达式，选择分支或并行执行
4. 到达 EndEvent → 流程完成，触发业务副作用（如资产状态变更）

#### 关键设计

- **Token 驱动**: 每个活跃节点有一个 Token，支持并行执行
- **接口解耦**: `IBpmnFlowInstance` 避免 Domain 层依赖 Entities
- **标准兼容**: 使用 Camunda 扩展属性存储审批人配置
- **纯函数引擎**: `BpmnEngine` 只操作内存状态，不访问数据库

#### 迁移说明

从旧引擎迁移到 BPMN 的关键变更：
- `Workflow.Nodes` → `Workflow.BpmnXml`
- `ApprovalFlow.CurrentNodeIndex` → `ApprovalFlow.CurrentNodeIds` (支持并行)
- `ApprovalFlow.Nodes` → `ApprovalFlow.BpmnTokens`
- 种子数据已转换为 BPMN XML

参考文档: `docs/BPMN-*.md`（6 份详细文档）

## 后端测试

- xUnit + FluentAssertions;集成测试用 `Microsoft.AspNetCore.Mvc.Testing` 的 `TestWebAppFactory`。
- **每个测试类用独立的 in-memory SQLite**(见提交 `b1f3915`,修复共享文件库导致的 flaky),避免跨类数据污染。
- 纯领域逻辑(引擎、编号生成、类别编码)有独立单元测试,无需 Web 工厂。

## 前端约定(apps/web-ele)

- 业务代码在 `apps/web-ele/src` 下:`api/`(按模块分文件,封装 `requestClient` 调用)、`views/`(各模块对应菜单路由)、`store/`(Pinia,登录逻辑在 `store/auth.ts`)。
  - `views/asset/` — 资产管理(`list` 列表、`hierarchy` 层级视图、`categories` 分类、`locations` 位置)
  - `views/approval/` — 审批流程(`pending` 待我审批、`mine` 我的申请、`confirm-return` 确认入库)
  - `views/report/` — 报表(`summary` 汇总、`borrow` 借用明细、`overdue` 逾期)
  - `views/admin/` — 基础数据与系统管理(`departments` 部门、`users` 用户、`roles` 角色、`settings` 系统参数、`audit` 审计日志、`workflows` 工作流设计器)
- 复用上游 `@vben/*`、`@core/*` 包的能力(布局、请求客户端、preferences、stores),不要重复造轮子;改动 `web/packages/` 下的核心包会影响所有 app,需谨慎。
- 提交前跑 `pnpm -F @vben/web-ele run typecheck`;monorepo 根有 `pnpm check`(circular/dep/type/cspell)。
- **端到端测试**:`web/e2e-comprehensive-test.js`(Playwright,覆盖登录 + 资产/审批/报表/系统管理全部页面)。须先起后端(5000)再起前端(5777),然后 `cd web && node e2e-comprehensive-test.js`(依赖 `web/node_modules/playwright`,用默认账号 `1001/123456` 跑通全流程)。截图产物(`e2e-final-state.png`、`debug-*.png`)已在 `.gitignore` 忽略,不入库。

## 常见开发场景速查

| 场景 | 步骤 |
|------|------|
| **修改 BPMN 工作流定义** | (1) 前端: `views/admin/workflows/bpmn-modeler.vue` 可视化设计器调整流程 (2) 保存后 `Workflow.BpmnXml` 字段自动更新 (3) 测试: `BpmnEngineTests.cs` 覆盖新场景 (4) 如需扩展网关类型,修改 `Domain/Workflow/BpmnParser.cs` + `BpmnEngine.cs` |
| **扩展基础数据(分类/部门/位置)** | (1) Domain: `AssetCategory`/`Department`/`Location` 实体(分类编码生成在 `Services/CategoryCodeService`) (2) Application: 复用 `IBaseDataService`(三类基础数据共用同一粗粒度服务)+ DTO (3) Infrastructure: 实现 + EntityTypeConfiguration (4) DbSeeder: 种子数据 (5) Api: 对应控制器(如 `AssetCategoryController`) (6) 迁移 (7) 前端页面(如 `views/admin/categories`)+ 菜单注册 |
| **后端新增权限** | (1) DbSeeder: `Permission` 表加行 + 角色-权限映射 (2) Api: action 标注 `[HasPermission("code")]` (3) 迁移 (4) 前端菜单由后端下发,无需改前端代码 |
| **前端新页面映射后端菜单** | (1) 后端 DbSeeder 注册 Menu(name/path/component) + Permission (2) 前端在 `views/<module>/` 创建页面,Component 路径须与后端 menu.Component 一致 (3) 登录后菜单自动下发,无需硬编码路由 |
| **修改 BPMN 工作流定义** | (1) 前端: `views/admin/workflows/bpmn-modeler.vue` 可视化设计器调整流程 (2) 保存后 `Workflow.BpmnXml` 字段自动更新 (3) 测试: `BpmnEngineTests.cs` 覆盖新场景 (4) 如需扩展网关类型,修改 `Domain/Workflow/BpmnParser.cs` + `BpmnEngine.cs` |
| **查看工作流执行状态** | `ApprovalFlow.CurrentNodeIds`(活跃节点列表) + `BpmnTokens`(Token 状态字典,JSON 存储);调试时可在数据库直接查看或通过 `ApprovalController` 返回的流程详情分析执行路径 |
| **新增资产附件字段** | (1) Domain: `Asset` 实体加字段 (2) Infrastructure: `EntityTypeConfiguration` 配置长度/映射 (3) Application: DTO 加字段并在 Service 映射 (4) 迁移 (5) 前端: `api/asset.ts` 加类型,表单加上传组件 |
| **资产批量导入(Excel)** | `AssetImportController`(路由 `api/assets/import`)三段式:`GET .../template` 下模板 → `POST .../validate` 上传预览校验 → `POST .../confirm` 确认落库;实现在 `IAssetService.BuildImportTemplate`/`ValidateImportAsync`/`ConfirmImportAsync`。模板下载用 `asset:view`,校验/确认用 `asset:create` |
| **查询流转历史或审计日志** | 流转历史: `ApprovalFlows` 表按 `AssetId` 筛选; 审计日志: `AuditLogs` 表按 `TargetType=="Asset" && TargetId==资产ID` 筛选。参考 `AssetService.GetDetailAsync` 实现 |
| **实现数据权限隔离** | (1) JWT: `IJwtTokenService.Create` 加参数传入用户属性(如 departmentId) (2) Service: 注入 `IHttpContextAccessor`,从 `HttpContext.User.Claims` 读取 (3) 查询方法开头检查角色并附加过滤条件 (4) 测试: 创建不同角色用户验证隔离效果 |

## 调试与故障排查

**常见问题快速定位:**

1. **JWT 认证失败(401)**
   - 检查: `appsettings.json` 的 `Jwt:Key` 是否配置(生产环境必须替换占位符)
   - 检查: 前端 token 是否过期(`Jwt:ExpireMinutes` 默认 1440 分钟)
   - 检查: 请求头 `Authorization: Bearer <token>` 格式是否正确

2. **权限不足(403)**
   - 检查: 用户角色是否关联了目标权限码(`DbSeeder` 的 `RolePermission` 映射)
   - 检查: 控制器 action 的 `[HasPermission("code")]` 与数据库 `Permissions` 表是否一致
   - 调试: 在 `PermissionAuthorizationHandler.HandleRequirementAsync` 打断点查看当前用户的 claims

3. **EF 迁移冲突**
   - 现象: `dotnet ef migrations add` 报错或 `Migrate()` 失败
   - 解决: 删除 `Migrations/` 下冲突的迁移文件 → 重新 `add` → 删除旧 `.db` 文件让 `Program.cs` 重建

4. **前端请求 404**
   - 检查: 后端是否启动在 5000 端口(`dotnet run` 输出确认)
   - 检查: `apps/web-ele/vite.config.mts` 的代理配置 `/api -> http://localhost:5000`
   - 检查: 后端路由是否注册(`Program.cs` 的 `app.MapControllers()`)

5. **BPMN 工作流执行异常**
   - 检查: `Workflow.BpmnXml` 字段是否为合法 XML(可在设计器中验证)
   - 检查: 条件表达式语法(如 `${amount} > 5000`)是否正确
   - 调试: `BpmnEngine.cs` 的 `EvaluateCondition` 方法,查看上下文变量
   - 日志: `ApprovalFlow.BpmnTokens` JSON 字段记录了每个 Token 的完整执行路径

6. **文件上传失败**
   - 检查: `Attachment:Path` 配置的目录是否存在且有写权限(默认 `App_Data/uploads`)
   - 检查: 文件大小是否超过 5MB 或格式不在白名单(jpg/png/gif/webp)
   - 检查: `FileStorageService` 是否注册为 Singleton(`Program.cs`)

7. **测试失败(Flaky)**
   - 确认: 每个测试类使用独立的 in-memory SQLite(见 `TestWebAppFactory`)
   - 确认: 测试间没有共享状态(避免 `static` 字段或单例服务污染)
   - 重现: `dotnet test --filter "Name=<test_name>"` 单独运行失败的测试

**日志位置:**
- 后端: 控制台输出(开发环境) + `logs/` 目录(生产环境,需配置 Serilog)
- 前端: 浏览器 DevTools Console + Network 面板
- 审计: `AuditLogs` 表记录所有 CUD 操作,可通过 `views/admin/audit` 查询



- **路径分隔符**:Windows 环境下文件路径用反斜杠 `\`。
- 后端 C#:`Nullable` + `ImplicitUsings` 开启;控制器保持瘦,逻辑下沉到 service。
- 前端:TypeScript + Vue 3 `<script setup>`;遵循上游 Vben 的 ESLint/Prettier/Stylelint 配置。
- 界面文案、文档、提交说明均用中文。提交遵循 Conventional Commits(如 `feat(web): ...`、`fix: ...`、`test: ...`)。
- **不提交**:SQLite 库文件(`*.db`)、`web/dist/`、`dist.zip`、`bin/`、`obj/`、日志、真实员工数据、生产凭据、内网地址。
- 生产部署必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key` 占位符。

## 项目状态

当前完成度约 **90%**,四大核心模块(资产管理、审批工作流、报表统计、RBAC/基础数据)已全面打通,所有计划待办事项已完成。

最新里程碑(2026-06-17 ~ 2026-06-22):
- ✅ 确认入库接口对齐(`/api/approvals/pending-return`)
- ✅ 资产详情页及流转时间线(`GET /api/assets/{id}/detail`)
- ✅ 资产照片附件上传与回显(`Asset.ImageUrls` + `FileStorageService`)
- ✅ 多部门数据权限隔离(部门管理员仅看本部门资产,JWT 携带 `departmentId`)
- ✅ **BPMN 2.0 工作流引擎升级**(从简单线性引擎升级到标准 BPMN,支持并行网关/包容网关/排他网关)

系统已进入生产部署准备阶段。详见 `docs/plans/M7-进度分析与待办事项.md` 与 `docs/BPMN-*.md`。
