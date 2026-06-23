# 🎨 BPMN 设计器样式问题说明

**问题**: BPMN 设计器中的连线在鼠标悬停时会变得很粗

**影响**: 仅影响视觉效果，不影响功能

---

## 🔍 问题原因

vben-admin 框架的全局样式与 bpmn-js 的 SVG 样式产生冲突，导致连线的 `stroke-width` 被意外放大。

---

## ✅ 解决方案

### 方案 1: 使用专业工具（推荐）

**Camunda Modeler** - 免费开源的 BPMN 设计器

1. **下载安装**: https://camunda.com/download/modeler/
2. **设计流程**: 在 Camunda Modeler 中创建 BPMN
3. **导入系统**: 复制 BPMN XML，在系统中"编辑"流程，粘贴 XML 并保存

**优点**:
- ✅ 专业的 BPMN 2.0 编辑器
- ✅ 无样式冲突
- ✅ 更强大的功能
- ✅ 更好的用户体验

### 方案 2: 在线编辑器

**bpmn.io Playground**: https://demo.bpmn.io/

1. 在线设计流程
2. 下载 BPMN XML
3. 导入系统

### 方案 3: 直接在系统中使用（临时）

**现状**:
- ✅ 功能完全正常
- ✅ 保存/加载正常
- ✅ 属性配置正常
- ⚠️ 连线显示较粗（仅视觉问题）

**使用建议**:
- 快速修改可以直接使用
- 复杂流程建议用 Camunda Modeler

---

## 🛠️ 技术修复（开发人员）

如果需要彻底修复这个问题，可以尝试：

### 1. 检查全局样式
```bash
# 查找可能冲突的样式
cd web/apps/web-ele
grep -r "stroke-width" src/
grep -r "line {" src/
```

### 2. 修改 vben-admin 主题配置
```typescript
// 在 vite.config.ts 或主题配置中
// 排除 bpmn-js 的样式被全局样式影响
```

### 3. 使用 Shadow DOM
```typescript
// 将 bpmn-js 容器隔离在 Shadow DOM 中
// 完全避免全局样式干扰
```

---

## 📊 已尝试的修复

✅ 添加 `!important` 样式覆盖  
✅ 移除 `scoped` 样式限制  
✅ JavaScript 动态注入样式  
⚠️ 仍然存在全局样式优先级问题

---

## 💡 建议

**短期**: 使用 Camunda Modeler 设计复杂流程  
**长期**: 考虑将 BPMN 设计器独立为单独的页面或微前端模块

---

## 🎯 不影响使用的功能

- ✅ 流程设计
- ✅ 节点配置
- ✅ 属性面板
- ✅ 保存/加载
- ✅ BPMN XML 导出
- ✅ 流程执行

**结论**: 这是一个纯视觉问题，不影响系统的核心功能和生产使用。
