# 部门资产管理系统部署入口

本文件原有内容基于已废弃的 SQLite 部署方案，现仅保留入口以避免继续误用。

当前系统使用 **ASP.NET Core 8 + MySQL 5.7+**。请以 [README-部署.md](./README-部署.md) 为唯一现行部署说明，并使用 `appsettings.Production.json` 中的 MySQL 占位符模板；真实连接字符串、JWT 密钥和管理员初始密码必须通过部署配置或环境变量注入，不得提交到仓库。
