# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 仓库定位

部门资产管理系统的全栈实现仓库,包含四个并存的部分:

- `backend/` — **正式后端**:ASP.NET Core 8 + EF Core + MySQL 5.7,DDD 四层架构,JWT + 权限码鉴权,可配置审批工作流引擎。当前活跃开发对象。
- `web/` — **正式前端**:基于 vue-vben-admin 5.x 的 monorepo(pnpm + turbo)。实际开发的应用是 `apps/web-ele`(Vue 3 + Element Plus);`web-antd`、`web-naive` 为上游模板自带,**不使用**。
- `docs/` — 需求/设计/实施规划文档(`.md` 与 `.pdf` 并存,**修改以 `.md` 为准**)。审批与多部门设计见 `docs/审批工作流设计.md`、`docs/多部门预留设计.md`;路线图见 `docs/全栈实施规划.md` 与 `docs/plans/`。**注意**:`docs/` 下有大量过程性报告(`BPMN-*报告.md`、`*-报告-2026-06-2x.md`、`*测试报告*.md` 等)属历史快照,**仅供追溯,勿当作现行规范**;权威设计以 `审批工作流设计.md`、`架构设计文档.md`、`全栈实施规划.md` 与 `docs/plans/` 为准。
- `prototype/` — 早期纯静态 HTML 原型(零依赖),仅作参考,新功能不在此实现。
- `deploy/` — 内网部署说明、生产配置样例、数据库备份脚本(部署方案见 `deploy/README-部署.md`)。

> **本文件是单一信息源**:根目录另有 `AGENTS.md`(面向 Codex / Copilot / Cursor 等其他 AI 代理的快速入口),它**仅摘录**本文件最常用的部分并声明以 `CLAUDE.md` 为准。更新架构说明、开发场景速查或约定时,**只改本文件**;`AGENTS.md` 保持精简摘要,避免双份维护漂移。

## 常用命令

### 环境检查

```powershell
# 验证 dotnet 版本(需 8.0+)
dotnet --version

# 验证 pnpm 版本(需 9.12+,仓库锁定 pnpm@9.15.0;Node 需 20.10+)
pnpm --version

# 验证 MySQL 连接(修改连接字符串为实际值)
mysql -h localhost -u root -p123456
```

### 后端(在仓库根目录执行)

```powershell
# 构建
dotnet build .\backend\AssetManagement.sln

# 运行 API 服务
dotnet run --project backend\src\AssetManagement.Api      # 监听 http://localhost:5292

# 运行全部测试
dotnet test .\backend\tests\AssetManagement.Tests --no-build

# 运行单个测试类
dotnet test .\backend\tests\AssetManagement.Tests --filter "FullyQualifiedName~ApprovalApiTests"

# 运行单个测试方法
dotnet test .\backend\tests\AssetManagement.Tests --filter "Name=Health_returns_ok"

# 健康检查
curl http://localhost:5292/api/health
```

### EF Core 迁移

⚠️ **重要**: `dotnet-ef` 固定在 8.0.28,通过 `backend\dotnet-tools.json` 管理;必须在 `backend\` 目录执行

```powershell
cd backend

# 新增迁移(自动生成类)
dotnet ef migrations add <Name> --project src\AssetManagement.Infrastructure --startup-project src\AssetManagement.Api

# 应用启动默认不会改库；需要自动迁移/补种子时显式配置 Database:AutoMigrate=true、Database:AutoSeed=true
# 生产环境建议首次部署或升级时开启一次，确认完成后关闭

# 移除最后一个迁移
dotnet ef migrations remove
```

### 前端(在 `web/` 目录执行,**必须用 pnpm**)

```powershell
# 安装依赖
pnpm install

# 启动开发服务器(带 HMR 热重载)
pnpm -F @vben/web-ele dev                       # 监听 http://localhost:5777

# 类型检查
pnpm -F @vben/web-ele run typecheck

# 生产构建(含依赖包)
pnpm --filter @vben/web-ele... run build

# monorepo 全局检查(圆形依赖/类型检查/拼写/依赖版本)
pnpm check

# 构建并本地预览(需先构建)
pnpm -F @vben/web-ele run preview
```

### 集成测试(端到端)

```powershell
# 1. 启动后端 API(守留在运行状态)
dotnet run --project backend\src\AssetManagement.Api

# 2. 在新终端启动前端开发服务器(守留在运行状态)
cd web
pnpm -F @vben/web-ele dev

# 3. 在第三个终端运行 E2E 测试
cd web
node e2e-comprehensive-test.js
```

### 数据库配置

仓库不保存可用的数据库口令或 JWT 密钥。启动前通过环境变量或部署平台的密钥配置注入:

```powershell
$env:ConnectionStrings__Default = 'Server=localhost;Port=3306;Database=assetmgmt;User=<本地用户>;Password=<本地密码>;CharSet=utf8mb4;'
$env:Jwt__Key = '<至少 32 字符的随机密钥>'
$env:ASSET_ADMIN_PASSWORD = '<首次初始化管理员的强密码>' # 可选；生产环境必须设置
```

- **配置键**:`ConnectionStrings:Default`、`Jwt:Key`。
- **生产模板**:`deploy\appsettings.Production.json` 仅含占位符，部署时必须替换或用环境变量覆盖。
- **初始化账号**:工号 `1001`;未设置 `ASSET_ADMIN_PASSWORD` 时仅在本地回退 `123456`。系统不强制首次登录改密，生产环境必须通过环境变量设置强密码。

## 前后端集成约定(关键)

- **代理**:前端 `apps/web-ele/vite.config.mts` 将 `/api` 代理到 `http://localhost:5292`。本地开发须先起后端再起前端。
- **统一响应体**:后端所有接口返回 `ApiResult<T>`(`AssetManagement.Application/Common/ApiResult.cs`),形如 `{ code, message, data }`,`code == 0` 为成功。前端响应拦截器(`apps/web-ele/src/api/request.ts`)按 HTTP 状态码解构。
- **鉴权**:登录返回 JWT,前端存入 access store,请求拦截器加 `Authorization: Bearer <token>` 头。401 触发登出。
- **动态路由/菜单**:菜单与权限由后端下发,**不在前端硬编码**。登录后 `GET /api/auth/user-info` 取用户信息、`GET /api/menu/routes`(`MenuController`,对应前端 `api/core/menu.ts` 的 `getAllMenusApi`)取当前用户的动态路由树;菜单的 `Component` 字段(如 `/admin/users/index`)映射到 `apps/web-ele/src/views` 下的页面。**注意区分**:`GET /api/menus`(`RbacMenuController`)是后台菜单 CRUD 管理接口,**不是**动态路由来源。新增受权限控制的页面需同时在后端 `DbSeeder` 注册 Menu + Permission。
- **错误**:业务异常抛 `BizException(code, message)`,由 `ExceptionMiddleware` 统一转 `ApiResult`。

## 后端架构

DDD 四层,依赖方向 Api → Infrastructure → Application → Domain:

- **Domain**(`AssetManagement.Domain`):实体(`Entities/`)、领域服务(`Services/`,如资产编号 `AssetNoGenerator`、类别编码 `CategoryCodeService`)、**纯函数审批引擎**(`Workflow/`)。
- **Application**(`AssetManagement.Application`):服务接口(`I*Service`)、DTO、`Common/`(`ApiResult`、`BizException`、`PagedResult`)。仅定义契约与数据形状,不含实现。服务按**限界上下文**粗粒度划分(**非每实体一个**):`IAssetService`(资产 CRUD + 详情/流转时间线 + Excel 批量导入)、`IAuthService`/`IJwtTokenService`、`IBaseDataService`(部门/分类/位置)、`IRbacService`(用户/角色/权限/菜单)、`IWorkflowService`(工作流 + 审批)、`IReportService`(汇总/借用/逾期报表)、`IFileStorageService`(文件存储,见 `Files/`)、`IAuditQueryService`(审计日志查询)/`IAuditMaintenanceService`(审计日志清理)/`IDatabaseBackupService`(数据库备份,三者均见 `Audit/`)、**`ITestProjectService`/`ITestMaterialService`/`IMaterialFlowService`**(新产品新技术(测试料件)模块,见下「新产品新技术(测试料件)模块」)。
- **Infrastructure**(`AssetManagement.Infrastructure`):服务实现、`Persistence/`(`AppDbContext` + `Configurations/` 每实体一个 `IEntityTypeConfiguration` + `Seed/DbSeeder`)、`Migrations/`、`Auth/`(JWT、权限策略)、`Audit/`(操作审计过滤器 + 审计查询)、`Reports/`(报表服务)、`Files/`(文件存储服务 `FileStorageService`,支持资产图片上传;在 `Program.cs` 注册为 **Scoped**,上传根目录由 `Attachment:Path` 配置,默认 `App_Data/uploads`)。
- **Api**(`AssetManagement.Api`):瘦控制器,每个 action 一行调用对应 `I*Service`;`Program.cs` 注册所有 DI、JWT、Swagger、自定义权限策略。

新增一个后端功能模块的路径:Domain 加实体/领域逻辑 → Application 定义 `IXxxService` + DTO → Infrastructure 实现并加 `EntityTypeConfiguration` → `Program.cs` 注册 DI → Api 加瘦控制器 → 加迁移 → 在 `DbSeeder` 注册菜单/权限。

### 鉴权模型(RBAC + 权限码)

- 控制器 action 用 `[HasPermission("asset:view")]` 标注(继承 `AuthorizeAttribute`,Policy 名为 `perm:<code>`)。
- 自定义 `PermissionPolicyProvider` + `PermissionAuthorizationHandler` 动态解析策略,无需预注册每个权限。
- 权限码、角色、角色-权限/菜单映射的种子数据集中在 `DbSeeder`;权限矩阵参照需求文档。资产删除相关的 `asset:delete`/`asset:restore`/`asset:purge` 见下「资产/分类删除模型」。

### 多部门数据隔离(已实现)

- **JWT增强**:登录时将用户的 `DepartmentId` 写入 JWT token 的 `departmentId` claim。
- **数据隔离逻辑**(`AssetService.ApplyQuery`):
  - 超级管理员(`admin` 角色):无限制,查看全部资产
  - 部门主管(`supervisor` 角色且非 `admin`):自动过滤,只能查看本部门+子部门的资产
  - 普通员工:无限制(共享资产池模式)
- **实现方式**:通过 `IHttpContextAccessor` 获取当前用户的角色和部门信息,在 EF 查询条件中自动附加 `DepartmentId` 过滤。
- 参考设计文档:`docs/多部门预留设计.md`。

### 资产/分类删除模型(软删除 + 撤销/彻底删除)

`Asset`/`AssetCategory` 含 `IsDeleted` + `DeletedAt`,删除采用软删除,**无独立"回收站"视图**——已删除项仍显示在主清单/分类树中(置灰 + "已删除"标签)。三个删除相关权限码:

- **`asset:delete`** — 软删除("删除"):置 `IsDeleted=true`;借出中资产不可删;有资产的分类不可删。
- **`asset:restore`** — 撤销删除(恢复):`AssetService.RestoreAsync`/`BaseDataService.RestoreCategoryAsync`(分类级联恢复子树,且要求上级未删除)。默认授予 `admin` + `supervisor`。
- **`asset:purge`** — 彻底删除(物理删除):**必须先软删除**才能彻底删除。默认授予 `admin`。

要点:

- **查询三态**:资产 `AssetQuery.DeleteStatus`(`active`/`all`/`deleted`),主清单默认传 `all`;分类树 `GetCategoryTreeAsync(string? deleteStatus)` 同三态(旧 `deletedOnly` 布尔已废弃)。报表/可借用/工作流流转(`ReportService`/`BizEffectApplier`/`WorkflowService`)**自动排除**已删除资产。
- **详情可见**:`AssetService.GetDetailAsync` **允许查看已删除资产**(不经会拦截已删除的 `GetAsync`),供主清单已删除行的「详情」按钮使用。
- **角色**:系统仅保留 `admin`(系统管理员)/`supervisor`(部门主管)/`employee`(普通员工)三个角色。`warehouse`/`dept_admin` 为历史角色，增量种子会将其用户合并到 `supervisor` 并删除旧角色。
- 前端:`asset/list`、`asset/categories` 已删除行按权限显示「撤销删除」「彻底删除」;详情对话框 `AssetDetailDialog.vue` 用 `ElDescriptions` 展示并高亮删除状态。

### 新产品新技术(测试料件)模块(独立模块,2026-06-25 新增,后续持续扩展)

> **命名说明**:界面一级菜单显示为 **"新产品新技术"**,但代码命名空间与权限前缀仍沿用 `material`/`TestProject`/`TestMaterial`(历史名"测试料件")。本文档"新产品新技术"= 代码中的测试料件/测试项目模块。

围绕**新产品/新技术测评项目**的全生命周期管理:测评项目(类型、进度、负责人、计划/结案时间、定期跟进)+ 厂商寄送的测试料件(非固定资产,临时编号 `TM-YYYYMMDD-流水号`,保管人/部门/位置/照片)+ 料件在人员间流转(可选审批)。

#### 核心实体

- **`TestProject`**(测评项目):`Name`(必填)、`Code`、`ProjectTypeCode`(项目类型,取自选项字典)、`ProgressCode`(进度状态,取自选项字典)、`OwnerId`(负责人)、`StartDate`/`PlannedFinishDate`/`ClosedDate`(开始/计划完成/结案)、`TestStatus`、`FollowUpIntervalDays`(跟进周期天数,默认 14)、软删除字段。**已从早期薄分组实体扩展为完整项目生命周期实体**。
- **`TestProjectOption`**(项目选项字典):`Kind`(分组,如项目类型 `project_type`/进度状态 `project_progress`)、`Code`/`Label`/`Sort`/`IsActive`。为项目类型、进度等下拉提供可维护字典项。
- **`TestProjectFollowup`**(项目跟进记录):`ProjectId`、`DueDate`(跟进到期日)、`Content`、`FilledById`/`FilledAt`。按 `FollowUpIntervalDays` 周期性跟进。
- **`TestMaterial`**(测试料件):`MaterialNo`(自动生成 `TM-YYYYMMDD-XXX`)、`Name`(必填)、`ProjectId`(必填,外键到 `TestProject`)、`VendorName`(厂商)、`Model`/`Brand`/`Quantity`、`DepartmentId`/`LocationId`/`CustodianId`(可选)、`ReceivedDate`(接收日期)、`Status`(0=在用、1=已退回厂商)、`Images`(JSON 数组,复用 `FileStorageService`)、`Remark`、软删除字段、`HasPendingFlow`(计算属性,标识有待审批流转)。
- **`MaterialFlow`**(流转记录):`FlowNo`(流转单号 `MF-YYYYMMDD-XXX`)、`MaterialId`、`ApplicantId`/`TransfereeId`(受让人)、`Reason`、`Status`(pending/approved/rejected)、`DirectTransfer`(布尔,标识绕过审批直接转移)、`BpmnTokens`(BPMN 引擎状态,JSON)、`CurrentNodeIds`(活跃节点列表,JSON)。
- **`MaterialFlowRecord`**(流转操作记录):`FlowId`、`Action`(start/approve/reject/direct_transfer)、`Operator`(操作人名)、`Comment`、`OperatedAt`。

#### 编号生成器

`Domain/Services/MaterialNoGenerator.cs` 与 `FlowNoGenerator.cs`,纯函数,TDD 覆盖(含跨日重置、三位流水号补零)。格式:`TM-YYYYMMDD-001`、`MF-YYYYMMDD-001`。

#### 服务与接口

- **`TestProjectService`**:项目 CRUD + 软删除三态(active/all/deleted)+ 撤销/彻底删除(删除项目前检查下辖料件);项目**选项字典**(`TestProjectOption`)增删改查;项目**跟进记录**(`TestProjectFollowup`)增删改查;**统计**(`GetStatsAsync` → `TestProjectStatsDto`:总数/结案/进行中/落地 + 类型分布 `typeDist` + 月度统计 `monthlyStat`,供总览仪表盘)。
- **`TestMaterialService`**:CRUD + 软删除三态 + 详情(含流转历史 `MaterialFlows` 与操作记录 `MaterialFlowRecords`)+ 退回厂商(`ReturnToVendorAsync`,置 `Status=1`)。`CreateAsync` 自动生成 `MaterialNo`。
- **`MaterialFlowService`**:
  - `InitiateTransferAsync`:发起流转;若全局开关 `material.transfer.approval.enabled=false`(默认),直接转移(`DirectTransfer=true`,立刻改 `CustodianId`);否则创建 pending 流转并启动 BPMN 引擎(`material_transfer` 工作流模板)。
  - `ApproveAsync`/`RejectAsync`:审批;通过时触发 `BizEffectApplier.ApplyMaterialTransfer`,改 `CustodianId` 并记录操作。
  - `PendingAsync`/`MineAsync`:待我审批列表 / 我的发起列表。
- **控制器**:`TestProjectController`(`api/test-projects`,含 `GET stats` 统计、`options` 选项字典 CRUD、`{id}/followups` 跟进 CRUD)、`TestMaterialController`、`MaterialFlowController`。

#### 权限码与菜单

权限码分三组(共约 20 个):
- **`project:`**(9):`view`/`create`/`edit`/`delete`/`restore`/`purge`/`option`(管理选项字典)/`followup`(管理项目跟进)/`manage`。
- **`material:`**(9):`view`/`create`/`edit`/`delete`/`restore`/`purge`/`return`(退回厂商)/`transfer`/`approve`。
- **`material-flow:`**(3):`view`/`transfer`/`approve`(流转单据维度)。

菜单结构:一级入口界面名 **"新产品新技术"**(根菜单 `Name=Material`、`Path=/material`、`Component=BasicLayout`、图标 `lucide:flask-conical`)→ 两个子页面:**项目总览**(`/material/home` → `/material/home/index`)、**测试项目**(`/material/projects` → `/material/projects/index`),二者均以 `project:view` 控权。`DbSeeder.SeedTestMaterialModule`(幂等增量种子)确保权限/菜单/工作流模板/系统参数(`material.transfer.approval.enabled`,默认 `false`)齐全。**旧的 `/material/list`、`/material/transfers` 独立路由已废弃**,料件清单与流转审批现内嵌为「测试项目」页的 Tab。

#### 前端页面

- `views/material/home/index.vue`:**项目总览**仪表盘(ECharts:测评类型分布/进度状态分布饼图 + 结案/落地月度柱线组合图),数据来自 `getTestProjectStatsApi`;图表 option 构造抽到纯函数 `home/chart-options.ts`(带 `chart-options.spec.ts` 单测)。
- `views/material/projects/index.vue`:**测试项目**主页面与状态编排入口。页面展示按职责拆分为 `ProjectTable`、`ProjectFormDialog`、`ProjectOptionDialog`、`ProjectMaterialsTab`、`ProjectFlowsTab`、`ProjectFollowupsTab`;父组件继续统一负责 API 调用、权限、状态与请求时序。
- `views/material/components/`:三个对话框 `MaterialFormDialog.vue`(含图片上传,复用 `asset.ts` 的 `uploadAssetImageApi`/`assetImageUrl`/`stripImageToken`)、`MaterialDetailDialog.vue`(流转时间线)、`TransferDialog.vue`;以及表单校验纯函数 `material-form-rules.ts` 与 `projects/project-form-rules.ts`(均带 `.spec.ts` 单测)。
- 前端 API:`api/material.ts`(料件/流转)、`api/test-project.ts`(项目/选项/跟进/统计 `getTestProjectStatsApi`)。

#### 测试覆盖

- **后端单元测试**:`MaterialNoGeneratorTests.cs`、`FlowNoGeneratorTests.cs`(编号生成,TDD)、`TestProjectServiceNoTrackingTests.cs`(NoTracking 写路径)。
- **后端集成测试**:`TestMaterialApiTests.cs`(料件 CRUD、软删除三态、退回厂商、项目占用检查)、`MaterialFlowApiTests.cs`(开关关=直接转移、开关开=审批通过/驳回)。
- **前端单元测试**(Vitest,与视图同目录 colocated):`material-form-rules.spec.ts`、`project-form-rules.spec.ts`、`project-filter.spec.ts`、`home/chart-options.spec.ts`,以及资产模块的 `asset/list/components/asset-form-rules.spec.ts` 等。在 `web/` 下跑 `pnpm test:unit`。

#### 与固定资产模块的差异

| 维度 | 固定资产 | 新产品新技术(测试料件) |
|------|----------|----------|
| 编号 | 分类驱动三层(`一级-二级-三级-流水`),自动 | 临时编号 `TM-YYYYMMDD-XXX`,自动 |
| 分类 | 三层树形强制关联(`CategoryId` 必填) | 无分类,关联测试项目(`ProjectId` 必填) |
| 流转审批 | 借用/转让/归还三类,BPMN 工作流强制 | 仅转移一类,可全局关闭审批 |
| 删除 | 软删除,置灰保留在主清单 | 同左(复用模式) |
| 前端入口 | 独立一级菜单"资产管理" | 独立一级菜单"新产品新技术"(项目总览 + 测试项目工作台) |

**设计参考**:`docs/superpowers/plans/2026-06-25-测试料件管理.md`、`docs/superpowers/specs/2026-06-25-测试料件管理-design.md`。

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

#### 迁移说明（已完成，仅作参考）

从旧引擎迁移到 BPMN 的关键变更：
- `Workflow.Nodes` → `Workflow.BpmnXml`
- `ApprovalFlow.CurrentNodeIndex` → `ApprovalFlow.CurrentNodeIds` (支持并行)
- `ApprovalFlow.Nodes` → `ApprovalFlow.BpmnTokens`
- 种子数据已转换为 BPMN XML

参考文档: `docs/BPMN-*.md`（6 份详细文档）

## 站内通知模块（2026-06-28 新增）

所有核心业务动作（审批、流转、到期）均会生成站内通知，前端铃铛 5 分钟轮询展示。

### 架构

- **`Domain/Entities/Notification.cs`**：通知实体（`Id`、`Type`、`Title`、`Body`、`FlowId`、`UserId`、`IsRead`、`CreatedAt`、`IdempotencyKey`）。
- **`Application/Notifications/INotificationService.cs`**：接口，含 `GetMyNotificationsAsync`/`GetUnreadCountAsync`/`MarkReadAsync`/`MarkAllReadAsync`/`CreateAsync`/`CreateBatchAsync`（`CreateNotificationRequest` 带可选幂等键）。
- **`Infrastructure/Notifications/NotificationService.cs`**：实现，`CreateAsync`/`CreateBatchAsync` 做幂等检查（相同 `IdempotencyKey` 静默跳过）。
- **`Infrastructure/Notifications/OverdueNotificationWorker.cs`**：`BackgroundService`，每天午夜运行，扫描借用超期/即将到期，生成 `overdue`/`due_soon_1d`/`due_soon_3d` 通知。幂等键 `{type}_{flowId}_{yyyyMMdd}`。
- **`Infrastructure/Notifications/PendingApprovalReminderWorker.cs`**：`BackgroundService`，每天早 9 点运行，扫描等待超过 1 天的待审批资产流和料件流，向审批人发催办通知。幂等键 `pending_remind_{flowId}_{yyyyMMdd}_{userId}`。
- **`Infrastructure/Workflow/WorkflowService.cs`**：在 `StartAsync`/`ApproveAsync`/`RejectAsync`/`ConfirmReturnAsync` 中注入 `INotificationService`，触发对应通知。
- **`Infrastructure/TestMaterials/MaterialFlowService.cs`**：在 `InitiateTransferAsync`/`ApproveAsync`/`RejectAsync` 中注入 `INotificationService`，触发对应通知。
- **`Api/Controllers/NotificationController.cs`**：REST 接口（路由 `api/notifications`），`[Authorize]` 鉴权。

### 通知类型

| Type | 触发时机 | 接收人 |
|------|---------|-------|
| `overdue` | 借用已逾期（每日午夜扫描） | 借用人 |
| `due_soon_1d` | 借用明天到期（每日午夜扫描） | 借用人 |
| `due_soon_3d` | 借用 3 天后到期（每日午夜扫描） | 借用人 |
| `approval_pending` | 资产流程发起 / 流转进入下一审批节点 | 待审批节点的审批人 |
| `approval_approved` | 资产审批流程全部通过 | 申请人 |
| `approval_rejected` | 资产审批被驳回 | 申请人 |
| `return_confirmed` | 资产确认归还入库 | 借用人 |
| `material_transferred` | 料件直接转移完成（无审批模式） | 接收人 |
| `material_approval_pending` | 料件流转审批发起 / 进入下一审批节点 | 待审批节点的审批人 |
| `material_approved` | 料件流转审批全部通过 | 申请人 |
| `material_rejected` | 料件流转审批被驳回 | 申请人 |
| `approval_reminder` | 资产流程等待超过 1 天（每日早 9 点催办） | 审批人 |
| `material_approval_reminder` | 料件流转等待超过 1 天（每日早 9 点催办） | 审批人 |

### 接口

| 方法 | 路由 | 说明 |
|------|------|------|
| `GET` | `/api/notifications` | 获取我的通知（`?unreadOnly=true` 过滤未读）|
| `GET` | `/api/notifications/unread-count` | 未读数量 |
| `POST` | `/api/notifications/{id}/read` | 标记单条已读 |
| `POST` | `/api/notifications/read-all` | 全部标记已读 |

### 前端集成

- **`api/notification.ts`**：封装以上四个接口。
- **`layouts/basic.vue`**：页面加载时拉取通知列表，之后每 **5 分钟**轮询一次。铃铛右上角红点表示有未读；点击通知调 `markReadApi` 标记已读；「全部已读」调 `markAllReadApi`。复用 `@vben/layouts` 的 `Notification` 组件，无需额外 UI 组件。

## 数据库备份与审计维护

系统管理下新增「数据库备份」页面(`/admin/backups`),并把审计日志从「只读查询」扩展为「查询 + 导出 + 定时清理」。两者实现都在 `Infrastructure/Audit/` 目录,均由 `DbSeeder` 增量种子补齐权限/菜单/系统参数。

### 数据库备份

- **`IDatabaseBackupService`/`DatabaseBackupService`**:调用 `mysqldump`(可执行文件路径可配,默认 `mysqldump`)导出 `assetmgmt_<时间戳>.sql`,再由 **`DatabaseBackupPackageBuilder`** 打包成含 `database/*.sql` + `attachments/`(复用 `Attachment:Path` 上传目录,默认 `App_Data/uploads`)的完整 ZIP 备份包 `assetmgmt_<时间戳>.zip`。备份目录取系统参数 `database_backup_path`(默认 `Backups/`,即 `AssetManagement.Api/Backups`)。
- **`DatabaseBackupWorker`**(`BackgroundService`):按 `database_backup_time`(默认 `02:00`)每日定时备份,`database_backup_enabled` 可关闭。
- **`DatabaseBackupController`**(`api/database-backups`,全部 `[HasPermission("backup:manage")]`):`GET` 列出备份文件、`POST` 立即备份、`GET /{fileName}/download` 下载。

### 审计日志维护

- **`IAuditMaintenanceService`/`AuditMaintenanceService`**:按保留天数预览/执行清理(`PreviewCleanupAsync`/`CleanupAsync`),清理动作记录操作人。
- **`AuditCleanupWorker`**(`BackgroundService`):按 `audit_cleanup_time`(默认 `02:10`)每日定时清理,`audit_cleanup_enabled` 可关闭,保留天数取 `audit_retention_days`(默认 `30`,可选 7/14/30;旧 `audit_retention_months` 仅历史兼容)。
- **`AuditLogController`**(`api/audit-logs`)已扩展:`GET`(`audit:view`)分页查询、`GET /export`(`audit:export`)导出 Excel、`GET /cleanup-preview?retentionDays=`(`audit:cleanup`)预览、`DELETE ?retentionDays=`(`audit:cleanup`)清理。

### 权限码、后台任务与前端

- **新增权限码**:`backup:manage`(数据库备份)、`audit:export`(导出审计)、`audit:cleanup`(清理审计);连同已有 `audit:view` 由 `DbSeeder` 授予 `admin`。
- **`Program.cs` 共注册四个 `BackgroundService`**:`OverdueNotificationWorker`、`PendingApprovalReminderWorker`、`DatabaseBackupWorker`、`AuditCleanupWorker`。
- **前端**:`views/admin/backups/index.vue`(备份列表 + 立即备份 + 下载);审计日志查询/导出/清理入口在 `views/admin/audit`。

## 后端测试

- xUnit + FluentAssertions;集成测试用 `Microsoft.AspNetCore.Mvc.Testing` 的 `TestWebAppFactory`。
- **每个测试类用独立的 MySQL 数据库**(GUID 后缀,见 `MySqlFixtureBase`/`TestWebAppFactory`),避免跨类数据污染。
- 纯领域逻辑(引擎、编号生成、类别编码)有独立单元测试,无需 Web 工厂。

## 前端约定(apps/web-ele)

- 业务代码在 `apps/web-ele/src` 下:`api/`(按模块分文件,封装 `requestClient` 调用)、`views/`(各模块对应菜单路由)、`store/`(Pinia,登录逻辑在 `store/auth.ts`)。
  - `views/asset/` — 资产管理(`list` 列表、`hierarchy` 层级视图、`categories` 分类、`locations` 位置)
  - `views/approval/` — 审批流程(`pending` 待我审批、`mine` 我的申请、`confirm-return` 确认入库)
  - `views/report/` — 报表(`summary` 汇总、`borrow` 借用明细、`overdue` 逾期)
  - `views/admin/` — 基础数据与系统管理(`departments` 部门、`users` 用户、`roles` 角色、`settings` 系统参数、`audit` 审计日志、`workflows` 工作流设计器、`backups` 数据库备份)
  - `views/material/` — 新产品新技术/测试料件(`home` 项目总览仪表盘、`projects` 测试项目工作台并内嵌料件清单/流转审批 Tab、`components` 共享对话框与校验规则)
  - `views/dashboard/`、`views/demos/`、`views/_core/` — vue-vben-admin 上游模板自带目录,业务不涉及,**新功能不在此实现**
- 复用上游 `@vben/*`、`@core/*` 包的能力(布局、请求客户端、preferences、stores),不要重复造轮子;改动 `web/packages/` 下的核心包会影响所有 app,需谨慎。
- 提交前跑 `pnpm -F @vben/web-ele run typecheck`;monorepo 根有 `pnpm check`(circular/dep/type/cspell)。
- **前端单元测试**:纯函数(表单校验规则、图表 option 构造)用 Vitest,`*.spec.ts` 与被测源码**同目录 colocated**(如 `views/material/**/*.spec.ts`);在 `web/` 下跑 `pnpm test:unit`(`vitest run --dom`)。
- **端到端测试**:`web/e2e-comprehensive-test.js`(Playwright,覆盖登录 + 资产/审批/报表/系统管理全部页面)。须先起后端(5292)再起前端(5777),设置临时环境变量 `E2E_PASSWORD`(可选 `E2E_EMPLOYEE_NO`,默认 `1001`),然后 `cd web && node e2e-comprehensive-test.js`。脚本不保存密码,截图产物(`e2e-final-state.png`、`debug-*.png`)已在 `.gitignore` 忽略,不入库。

## 常见开发场景速查

| 场景 | 步骤 |
|------|------|
| **修改 BPMN 工作流定义** | (1) 前端: `views/admin/workflows/bpmn-modeler.vue` 可视化设计器调整流程 (2) 保存后 `Workflow.BpmnXml` 字段自动更新 (3) 测试: `BpmnEngineTests.cs` 覆盖新场景 (4) 如需扩展网关类型,修改 `Domain/Workflow/BpmnParser.cs` + `BpmnEngine.cs` |
| **扩展基础数据(分类/部门/位置)** | (1) Domain: `AssetCategory`/`Department`/`Location` 实体(分类编码生成在 `Services/CategoryCodeService`) (2) Application: 复用 `IBaseDataService`(三类基础数据共用同一粗粒度服务)+ DTO (3) Infrastructure: 实现 + EntityTypeConfiguration (4) DbSeeder: 种子数据 (5) Api: 对应控制器(如 `AssetCategoryController`) (6) 迁移 (7) 前端页面(如 `views/admin/categories`)+ 菜单注册 |
| **后端新增权限** | (1) DbSeeder: `Permission` 表加行 + 角色-权限映射 (2) Api: action 标注 `[HasPermission("code")]` (3) 迁移 (4) 前端菜单由后端下发,无需改前端代码 |
| **前端新页面映射后端菜单** | (1) 后端 DbSeeder 注册 Menu(name/path/component) + Permission (2) 前端在 `views/<module>/` 创建页面,Component 路径须与后端 menu.Component 一致 (3) 登录后菜单自动下发,无需硬编码路由 |
| **新增报表/导出** | (1) Application: `IReportService` 加方法 + DTO (2) Infrastructure: `Reports/ReportService` 实现查询(注意复用 `AssetService.ApplyQuery` 的部门隔离逻辑) (3) Api: `ReportController`(路由 `api/reports`)加 action,统一 `[HasPermission("report:view")]`,导出走 `.../export` 后缀返回 Excel (4) 前端: `views/report/` 加页面 + `api/report.ts` |
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
   - 解决:确认迁移顺序与模型快照,修正最后一个未部署迁移后重新生成；已部署迁移不得直接删除或改写

4. **前端请求 404**
   - 检查: 后端是否启动在 5292 端口(`dotnet run` 输出确认)
   - 检查: `apps/web-ele/vite.config.mts` 的代理配置 `/api -> http://localhost:5292`
   - 检查: 后端路由是否注册(`Program.cs` 的 `app.MapControllers()`)

5. **BPMN 工作流执行异常**
   - 检查: `Workflow.BpmnXml` 字段是否为合法 XML(可在设计器中验证)
   - 检查: 条件表达式语法(如 `${amount} > 5000`)是否正确
   - 调试: `BpmnEngine.cs` 的 `EvaluateCondition` 方法,查看上下文变量
   - 日志: `ApprovalFlow.BpmnTokens` JSON 字段记录了每个 Token 的完整执行路径

6. **文件上传失败**
   - 检查: `Attachment:Path` 配置的目录是否存在且有写权限(默认 `App_Data/uploads`)
   - 检查: 文件大小是否超过 5MB 或格式不在白名单(jpg/png/gif/webp)
   - 检查: `FileStorageService` 是否按 Scoped 注册(`Program.cs`),并能读取当前请求作用域的 `AppDbContext`

7. **测试失败(Flaky)**
   - 确认: 每个测试类使用独立的 MySQL 数据库(GUID 后缀,见 `MySqlFixtureBase`/`TestWebAppFactory`)
   - 确认: 测试间没有共享状态(避免 `static` 字段或单例服务污染)
   - 重现: `dotnet test --filter "Name=<test_name>"` 单独运行失败的测试

**日志位置:**
- 后端: 控制台输出(开发环境) + `logs/` 目录(生产环境,需配置 Serilog)
- 前端: 浏览器 DevTools Console + Network 面板
- 审计: `AuditLogs` 表记录所有 CUD 操作,可通过 `views/admin/audit` 查询

## 编码约定

- **路径分隔符**:Windows 环境下文件路径用反斜杠 `\`。
- 后端 C#:`Nullable` + `ImplicitUsings` 开启;控制器保持瘦,逻辑下沉到 service。
- **全局 NoTracking**:`Program.cs` 设置了 `QueryTrackingBehavior.NoTracking`。所有"查询实体后修改属性再 `SaveChanges`"的写路径必须显式加 `.AsTracking()`,再按场景使用 `SingleOrDefaultAsync(...)` / `FirstOrDefaultAsync(...)`;**禁止**使用 `FindAsync`（NoTracking 下不追踪修改）。`ExecuteUpdateAsync` / `ExecuteDeleteAsync` 无需 AsTracking。新增实体（`Add`）不受影响。
- **DbSeeder 菜单 ID**:新增菜单时**不手动指定 `Id`**，让 MySQL 自增，避免测试库主键冲突。
- 前端:TypeScript + Vue 3 `<script setup>`;遵循上游 Vben 的 ESLint/Prettier/Stylelint 配置。
- 界面文案、文档、提交说明均用中文。提交遵循 Conventional Commits(如 `feat(web): ...`、`fix: ...`、`test: ...`)。
- **不提交**:SQLite 库文件(`*.db`)、`web/dist/`、`dist.zip`、`bin/`、`obj/`、日志、真实员工数据、生产凭据、内网地址。
- 生产部署必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key` 占位符。

## 项目状态

五大核心模块(资产管理、审批工作流、报表统计、RBAC/基础数据、**新产品新技术(测试料件)**)已全面打通。2026-07-11 安全与一致性加固后，后端 **276 个**测试、前端 **309 个**单元测试及主要页面回归均通过。

最新里程碑(2026-06-17 ~ 2026-06-30):
- ✅ 确认入库接口对齐(`/api/approvals/pending-return`)
- ✅ 资产详情页及流转时间线(`GET /api/assets/{id}/detail`)
- ✅ 资产照片附件上传与回显(`Asset.ImageUrls` + `FileStorageService`)
- ✅ 多部门数据权限隔离(部门管理员仅看本部门资产,JWT 携带 `departmentId`)
- ✅ **BPMN 2.0 工作流引擎升级**(从简单线性引擎升级到标准 BPMN,支持并行网关/包容网关/排他网关)
- ✅ P0/P1/P2 优化任务全部完成,关键业务与权限边界均有自动化回归测试
- ✅ 前端 UI 统一优化(样式规范与布局改进、登录跳转修复)
- ✅ **资产/分类删除子系统重构**(删除即软删除并保留在主清单、`asset:restore` 撤销删除 + `asset:purge` 彻底删除、详情接口支持已删除项、`AssetDetailDialog` 详情页重构)
- ✅ **新产品新技术(测试料件)模块**(2026-06-25 起,独立模块并持续扩展:测评项目全生命周期(类型/进度/负责人/计划结案/定期跟进)+ 项目总览仪表盘 + 料件管理 + 流转审批(可选);临时编号 `TM-YYYYMMDD-XXX`;权限码扩至约 20 个(`project:*`/`material:*`/`material-flow:*`);界面一级菜单已更名为"新产品新技术")
- ✅ **全面站内通知系统**(2026-06-28，借用到期提醒 + 审批任务通知 + 审批结果通知 + 资产转让接收通知 + 料件流转通知，共 13 种通知类型；`PendingApprovalReminderWorker` 每日早 9 点催办超 1 天未处理流程；`INotificationService.CreateAsync/CreateBatchAsync` 支持幂等键防重复；铃铛 UI 5 分钟轮询，支持已读/全部已读)
- ✅ **数据库备份与审计维护**(系统管理新增「数据库备份」页,`mysqldump` 全量导出 + 附件打包为 ZIP 备份包、`DatabaseBackupWorker` 每日定时备份;审计日志支持导出与 `AuditCleanupWorker` 定时清理,新增 `backup:manage`/`audit:export`/`audit:cleanup` 权限码)

系统已进入生产部署准备阶段。详见 `docs/plans/M7-进度分析与待办事项.md`、`docs/superpowers/plans/2026-06-25-测试料件管理.md` 与 `docs/BPMN-*.md`。
