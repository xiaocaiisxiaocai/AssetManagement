# 部门资产管理系统

本仓库包含部门资产管理系统的设计文档、静态原型和全栈实现。

## 目录

- `docs/`：需求、设计、实施规划和里程碑计划。
- `prototype/`：早期静态原型。
- `backend/`：ASP.NET Core 8 + EF Core + SQLite 后端。
- `web/`：Vue 3 / Vben / Element Plus 前端。
- `deploy/`：内网部署说明、生产配置样例和 SQLite 备份脚本。

## 本地运行

后端：

```powershell
dotnet run --project backend/src/AssetManagement.Api
```

健康检查：`http://localhost:5000/api/health` 或按控制台输出端口访问。

前端：

```powershell
cd web
pnpm install
pnpm -F @vben/web-ele dev
```

默认账号：`1001 / 123456`。

## 验证命令

```powershell
dotnet build .\backend\AssetManagement.sln
dotnet test .\backend\tests\AssetManagement.Tests --no-build
cd web
pnpm -F @vben/web-ele run typecheck
pnpm --filter @vben/web-ele... run build
```

## 部署

部署方案见 `deploy/README-部署.md`。

总体实施路线见 `docs/全栈实施规划.md`，分阶段计划见 `docs/plans/`。

## 注意

- 不提交真实员工数据、生产密钥、内网地址。
- 不提交 SQLite 数据库、前端 `dist/`、`dist.zip` 和日志文件。
- 生产环境必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key`。
