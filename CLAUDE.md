# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 仓库定位

部门资产管理系统的**需求/设计文档 + 静态 HTML 原型**仓库,不包含真实后端代码。

- `docs/` — 产品决策与设计文档:需求文档、设计概要、架构设计文档、软件原型说明(均有 .md 和 .pdf 两份,**修改时以 .md 为准**)。
- `prototype/` — 可运行的纯静态原型(HTML + CSS + 原生 JavaScript,无框架、无 npm、无构建管线)。

注意:文档中规划的目标技术栈(Vue 3 + Element Plus + Pure Admin / ASP.NET Core 8 + EF Core + SQL Server)是**未来正式系统**的选型,与当前原型实现无关;原型刻意保持零依赖。审批已采用可配置工作流引擎(自由加节点/会签/或签/条件分支/加签转签),定时任务(Hangfire)与邮件提醒(MailKit)已移除;资产支持多部门归属(共享资产池)。相关设计见 `docs/审批工作流设计.md`、`docs/多部门预留设计.md`。

## 运行与测试

无包管理器、无构建、无自动化测试。原型以静态文件方式运行:

```bash
cd prototype && python -m http.server 8080
# 浏览器打开 http://localhost:8080/
```

优先使用本地服务器而非直接双击 index.html(file:// 协议下浏览器安全策略不同)。

提交前进行浏览器冒烟测试:登录流程(任意工号密码可登录)、四大模块页面导航、筛选/重置/分页/弹窗/批量操作、桌面与移动宽度的响应式布局。

## 原型架构

单页应用,三个 script 按依赖顺序加载(`data.js` → `pages.js` → `app.js`),全部挂在全局作用域:

- `prototype/index.html` — 静态外壳:登录页(`#page-login`)+ 主布局(`#page-main`,含顶栏、侧边菜单、空的 `#mainContent` 容器)。侧边菜单通过 `onclick="showPage('页面key')"` 驱动导航。
- `prototype/js/data.js` — 全局 `mockData` 对象,所有模拟数据(当前用户、资产层级、工单、报表、用户/类别/位置等)集中于此。**共享样例数据只放这里,不要硬编码进模板**。
- `prototype/js/pages.js` — 全局 `pages` 对象:`{ '页面key': () => '...html模板字符串' }`,每个页面是一个返回 HTML 的函数,内部用模板字符串遍历 `mockData` 渲染。
- `prototype/js/app.js` — 交互逻辑:`showPage(key)` 把 `pages[key]()` 注入 `#mainContent` 完成页面切换;还包含层级视图导航(`hierarchyPath`)、筛选(`currentFilters`)、模态弹窗(`showNotice`/`showConfirm`/`closeModal`)、Toast(`showToast`)、表单验证、分页排序等。所有按钮通过内联 `onclick` 调用 app.js 中的全局函数。

新增一个页面的完整路径:在 `pages.js` 注册页面 key 与模板函数 → 在 `index.html` 侧边菜单加入 `showPage('key')` 入口 → 交互函数写在 `app.js` → 数据放 `mockData`。

页面 key 共 20 个,按模块分组命名:`asset-*`(资产)、`approval-*`(审批)、`report-*`(报表)、`admin-*`(系统管理,含 `admin-departments` 组织架构、`admin-workflows` 审批流程设计器)。

## 编码约定

- HTML/CSS/JS 统一 4 空格缩进;原生 JS,不引入新依赖或抽象,除非重复逻辑确有提取必要。
- JS 函数与变量:`camelCase`(如 `handleLogin`、`currentFilters`)。
- 页面 key:kebab-case 字符串(如 `asset-list`、`approval-pending`)。
- CSS 类与 ID:kebab-case(如 `.login-container`、`#page-main`)。
- 界面文案为中文,文档与提交说明也使用中文。

## 提交规范

当前目录尚未初始化 Git。引入版本控制后使用简洁的 Conventional Commits(如 `feat: add asset import modal`)。PR 需说明变更目的、影响的页面或文档、手动测试记录,UI 可见变更附截图。

## 安全

不要提交真实员工数据、生产凭据或内部系统地址;样例数据须明显为合成数据,且仅存放于 `prototype/js/data.js`。
