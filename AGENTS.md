# Repository Guidelines

> 本文件为各类 AI 代理(Codex / Copilot / Cursor 等)的快速入口。**完整的架构说明、开发场景速查与约定以 `CLAUDE.md` 为单一信息源**,本文件仅摘录最常用部分,避免双份维护漂移。

## 仓库结构

部门资产管理系统的**全栈**仓库(非静态原型):

- `backend/` — 正式后端:ASP.NET Core 8 + EF Core + MySQL 5.7,DDD 四层架构(Api → Infrastructure → Application → Domain),JWT + 权限码鉴权,可配置审批工作流引擎。**当前活跃开发对象**。
- `web/` — 正式前端:基于 vue-vben-admin 5.x 的 monorepo(pnpm + turbo)。实际开发的应用是 `apps/web-ele`(Vue 3 + Element Plus);`web-antd`、`web-naive` 为上游模板自带,**不使用**。
- `docs/` — 需求/设计/实施规划文档(`.md` 与 `.pdf` 并存,**修改以 `.md` 为准**)。
- `prototype/` — 早期纯静态 HTML 原型,**仅作参考,新功能不在此实现**。
- `deploy/` — 内网部署说明、生产配置样例、数据库备份脚本。

## 常用命令

后端(仓库根目录执行):

```powershell
dotnet build .\backend\AssetManagement.sln
dotnet run --project backend\src\AssetManagement.Api      # 启动 API,默认 http://localhost:5292
dotnet test .\backend\tests\AssetManagement.Tests --no-build
```

前端(`web/` 目录执行,**必须用 pnpm**):

```powershell
pnpm install
pnpm -F @vben/web-ele dev                       # 开发服务器,端口 5777
pnpm -F @vben/web-ele run typecheck             # 类型检查
pnpm check                                       # monorepo 全局检查
```

本地开发须**先起后端再起前端**(前端 `/api` 代理到 `http://localhost:5292`)。健康检查 `GET http://localhost:5292/api/health`;默认账号 `1001 / 123456`。

## 编码与提交约定

- **路径分隔符**:Windows 环境下文件路径用反斜杠 `\`。
- 后端 C#:`Nullable` + `ImplicitUsings` 开启;控制器保持瘦,逻辑下沉到 service。
- 前端:TypeScript + Vue 3 `<script setup>`;遵循上游 Vben 的 ESLint/Prettier/Stylelint 配置。
- 界面文案、文档、提交说明均用**中文**;提交遵循 Conventional Commits(如 `feat(web): ...`、`fix: ...`、`test: ...`)。
- **提交与推送必须使用 Git 命令**:`git status`/`git add`/`git commit`/`git push`;不得用 GitHub App 或其他发布工具替代 Git 提交、推送流程。
- 阶段性功能完成并通过关键验证后，应及时进行**本地 commit**，避免长时间堆积未提交改动；提交前确认只包含本阶段相关文件。
- PR 应包含变更目的、受影响页面/文档、手动测试说明,可见 UI 变更附截图。

## 验证范围与效率

- **小修改默认只做直接相关验证**:运行受影响模块的定向测试，以及必要的构建、类型检查或格式检查；不得默认执行完整后端或前端回归测试。
- **完整回归测试仅在以下情况执行**:跨模块或架构级大修改、数据库迁移、核心权限/工作流等高风险修改，或用户明确要求执行完整回归测试。
- 提交前应明确报告实际执行的验证范围；未执行完整回归时不得表述为“全部测试通过”。

## 安全与配置

- **不提交**:SQLite 库文件(`*.db`)、`web/dist/`、`bin/`、`obj/`、日志、真实员工数据、生产凭据、内网地址。
- 生产部署必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key` 占位符。
