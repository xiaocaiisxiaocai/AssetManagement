// 应用主逻辑
let currentPage = 'asset-list';
let hierarchyPath = []; // 记录层级导航路径
let currentFilters = {}; // 当前筛选条件
let currentDeptScope = 'all'; // 顶栏“查看范围”当前选中的部门id，'all'=全部部门
let currentAssetView = 'hierarchy'; // 资产列表当前视图，重渲染时保持
let currentWorkflowId = 'wf-borrow'; // 审批流程设计器当前编辑的流程
let currentTreePath = []; // 资产分类编码树当前钻取路径（节点 id）

const dialogIcons = {
    info: 'ℹ️',
    success: '✅',
    warning: '⚠️',
    danger: '⛔'
};

function closeModal(element) {
    const modal = element?.closest ? element.closest('.modal') : element;
    if (modal) modal.remove();
}

function ensureToastContainer() {
    let container = document.querySelector('.toast-container');
    if (!container) {
        container = document.createElement('div');
        container.className = 'toast-container';
        document.body.appendChild(container);
    }
    return container;
}

function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `<span>${dialogIcons[type] || dialogIcons.info}</span><span>${message}</span>`;
    ensureToastContainer().appendChild(toast);
    setTimeout(() => toast.classList.add('visible'), 20);
    setTimeout(() => {
        toast.classList.remove('visible');
        setTimeout(() => toast.remove(), 220);
    }, 2600);
}

function showNotice(title, message = '', type = 'info', options = {}) {
    const modal = document.createElement('div');
    modal.className = 'modal active notice-modal';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="notice-body">
                <div class="notice-icon notice-${type}">${dialogIcons[type] || dialogIcons.info}</div>
                <div>
                    <h3 class="modal-title">${title}</h3>
                    ${message ? `<p class="notice-message">${message}</p>` : ''}
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-primary" onclick="closeModal(this);${options.onClose || ''}">知道了</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function showConfirm(title, message, onConfirm, options = {}) {
    const modal = document.createElement('div');
    modal.className = 'modal active notice-modal';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="notice-body">
                <div class="notice-icon notice-${options.type || 'warning'}">${dialogIcons[options.type || 'warning']}</div>
                <div>
                    <h3 class="modal-title">${title}</h3>
                    <p class="notice-message">${message}</p>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn ${options.confirmClass || 'btn-success'}" id="confirmActionBtn">确认</button>
            </div>
        </div>
    `;
    modal.querySelector('#confirmActionBtn').addEventListener('click', () => {
        closeModal(modal);
        if (typeof onConfirm === 'function') onConfirm();
    });
    document.body.appendChild(modal);
}

function finishAction(title, message = '', type = 'success') {
    showToast(title, type);
    if (message) {
        showNotice(title, message, type);
    }
}

function saveModalForm(formId, title, message, refreshPage) {
    const form = document.getElementById(formId);
    if (form && !form.checkValidity()) {
        form.reportValidity();
        return;
    }

    document.querySelectorAll('.modal').forEach(m => m.remove());
    if (refreshPage) showPage(refreshPage);
    showNotice(title, message, 'success');
}

function handlePageSizeChange(value) {
    showToast(`每页显示已切换为 ${value} 条`, 'info');
}

function openNotificationCenter() {
    showNotice(
        '通知中心',
        '2 条待处理通知：借用申请、转让申请各 1 条待您审批。进入审批模块可继续处理。',
        'info'
    );
}

function simulateQuery(scope) {
    showToast(`${scope}查询条件已应用`, 'success');
}

function resetQuery(scope) {
    showToast(`${scope}查询条件已重置`, 'info');
}

function savePrototypePageForm(formId, returnPage) {
    const form = document.getElementById(formId);
    if (form && !form.checkValidity()) {
        form.reportValidity();
        return;
    }
    showPage(returnPage || 'asset-list');
    showNotice('保存成功', '表单内容已在当前原型流程中保存。', 'success');
}

function startBorrowRequest() {
    const asset = mockData.assets.find(item => item.status === 'available');
    if (asset) {
        showBorrowModal(asset.id);
        return;
    }
    showNotice('暂无可借资产', '当前没有可直接发起借用的在库资产。', 'warning');
}

function startTransferRequest() {
    const asset = mockData.assets.find(item => item.status === 'available');
    if (asset) {
        showTransferModal(asset.id);
        return;
    }
    showNotice('暂无可转让资产', '仅“在库”状态的资产可发起转让；借出中的资产需先归还。', 'warning');
}

function showFlowDetail(flowNo) {
    const flow = mockData.approvals.find(item => item.flowNo === flowNo);
    if (flow) {
        showApprovalDetail(flow.id);
        return;
    }
    showNotice('工单详情', `工单 ${flowNo} 为历史记录，当前原型仅展示摘要信息。`, 'info');
}

function showOverdueDetail(assetNo) {
    const asset = mockData.reports.overdue.find(item => item.assetNo === assetNo);
    showNotice(
        '逾期资产详情',
        asset ? `${asset.name} 由 ${asset.custodian} 保管，已逾期 ${asset.overdueDays} 天。` : '未找到该逾期资产记录。',
        asset ? 'warning' : 'info'
    );
}

function showAdminEditor(type, mode = '编辑', name = '') {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">${mode}${type}</h3>
                <span class="modal-close" onclick="closeModal(this)">&times;</span>
            </div>
            <div class="modal-body">
                <form id="adminEditForm">
                    <div class="detail-grid">
                        <div class="form-group">
                            <label>* 名称</label>
                            <input type="text" required value="${name}" placeholder="请输入名称">
                        </div>
                        <div class="form-group">
                            <label>编码</label>
                            <input type="text" value="${Date.now().toString().slice(-6)}" placeholder="自动生成">
                        </div>
                        <div class="form-group">
                            <label>状态</label>
                            <select><option>启用</option><option>停用</option></select>
                        </div>
                    </div>
                    <div class="form-group" style="margin-top:20px">
                        <label>说明</label>
                        <textarea placeholder="请输入说明，便于后续维护"></textarea>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="saveModalForm('adminEditForm', '${type}已保存', '${mode}${type}操作已在当前原型中完成。', currentPage)">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function resetPassword(employeeNo) {
    showConfirm('确认重置密码？', `工号 ${employeeNo} 的密码将重置为工号后6位。`, () => {
        showNotice('密码已重置', '请提醒用户首次登录后及时修改密码。', 'success');
    });
}

function handleSettingsReset() {
    showConfirm('确认重置配置？', '系统参数将恢复为当前原型默认值。', () => {
        showToast('配置已重置', 'success');
    });
}

function handleSettingsSave() {
    showNotice('配置保存成功', '审批、分页等参数已在当前原型中保存。', 'success');
}

// 登录处理
function handleLogin(e) {
    e.preventDefault();
    const employeeNo = document.getElementById('employeeNo').value;
    const password = document.getElementById('password').value;

    if (employeeNo && password) {
        // 模拟登录
        document.getElementById('page-login').classList.remove('active');
        document.getElementById('page-main').classList.add('active');
        document.getElementById('currentUser').textContent = employeeNo === '1001' ? '张三' : `员工${employeeNo}`;
        currentDeptScope = 'all';
        currentAssetView = 'hierarchy';
        currentFilters = {};
        populateDeptScope();
        showPage('asset-list');
        showToast('登录成功，已进入资产列表', 'success');
        return false;
    }
    showNotice('登录信息不完整', '请输入工号和密码后再登录。', 'warning');
    return false;
}

// 退出登录
function handleLogout() {
    showConfirm('确认退出登录？', '退出后将返回登录页，当前原型操作状态不会保存。', () => {
        document.getElementById('page-main').classList.remove('active');
        document.getElementById('page-login').classList.add('active');
        document.getElementById('employeeNo').value = '';
        document.getElementById('password').value = '';
        showToast('已退出登录', 'success');
    }, { confirmClass: 'btn-danger', type: 'warning' });
}

// 显示页面
function showPage(pageName) {
    currentPage = pageName;
    const content = document.getElementById('mainContent');

    // 更新菜单激活状态
    document.querySelectorAll('.menu-item').forEach(item => {
        item.classList.remove('active');
        if (item.textContent.includes(getMenuName(pageName))) {
            item.classList.add('active');
        }
    });

    // 渲染页面内容
    if (pages[pageName]) {
        content.innerHTML = pages[pageName]();
    } else {
        content.innerHTML = `
            <div class="empty-state">
                <div class="empty-icon">🧩</div>
                <h3>页面正在完善</h3>
                <p>该模块已预留入口，后续可继续补齐业务表单和数据。</p>
            </div>
        `;
    }
}

// 获取菜单名称
function getMenuName(pageName) {
    const nameMap = {
        'asset-list': '资产列表',
        'approval-pending': '待我审批',
        'approval-mine': '我的申请',
        'approval-return': '待确认归还',
        'report-summary': '资产汇总',
        'report-borrowed': '借用明细',
        'report-overdue': '逾期资产',
        'admin-users': '用户管理',
        'admin-departments': '组织架构',
        'admin-workflows': '审批流程',
        'admin-categories': '类别管理',
        'admin-locations': '位置管理',
        'admin-settings': '系统参数',
        'admin-logs': '审计日志'
    };
    return nameMap[pageName] || '';
}

// 筛选功能
function applyFilter(formId) {
    const form = document.getElementById(formId);
    if (!form) return;

    const formData = new FormData(form);
    currentFilters = Object.fromEntries(formData.entries());

    showToast('筛选条件已应用', 'success');
    if (currentPage === 'asset-list') {
        currentAssetView = 'table';
        showPage('asset-list');
    }
}

// 重置筛选
function resetFilter(formId) {
    const form = document.getElementById(formId);
    if (!form) return;

    form.reset();
    currentFilters = {};
    showToast('筛选条件已重置', 'info');
    if (currentPage === 'asset-list') {
        currentAssetView = 'table';
        showPage('asset-list');
    }
}

// 搜索功能
function handleSearch(keyword) {
    if (!keyword) {
        showNotice('请输入搜索关键词', '搜索内容不能为空。', 'warning');
        return;
    }
    showToast(`正在搜索：${keyword}`, 'info');
}

// 排序功能
function sortTable(column, order) {
    showToast(`已按${column}${order === 'asc' ? '升序' : '降序'}排序`, 'info');
}

// 分页功能
function goToPage(page) {
    const pageName = page === 'prev' ? '上一页' : page === 'next' ? '下一页' : `第 ${page} 页`;
    showToast(`已切换到${pageName}`, 'info');
}

// 视图切换
function switchView(viewType) {
    currentAssetView = (viewType === 'hierarchy') ? 'hierarchy' : 'table';
    const hierarchyView = document.getElementById('hierarchyView');
    const tableView = document.getElementById('tableView');
    const buttons = document.querySelectorAll('.view-btn');

    if (!hierarchyView || !tableView) return;

    buttons.forEach(btn => btn.classList.remove('active'));

    if (viewType === 'hierarchy') {
        hierarchyView.style.display = 'block';
        tableView.style.display = 'none';
        buttons[0].classList.add('active');
    } else {
        hierarchyView.style.display = 'none';
        tableView.style.display = 'block';
        buttons[1].classList.add('active');
    }
}

// ========== 多部门支持（共享资产池 + 任意层级部门树） ==========

// 扁平化部门树为带层级深度的列表（用于下拉、筛选）
function flattenDepartments(nodes = mockData.departments, depth = 0, acc = []) {
    nodes.forEach(node => {
        acc.push({ id: node.id, name: node.name, depth });
        if (node.children && node.children.length) {
            flattenDepartments(node.children, depth + 1, acc);
        }
    });
    return acc;
}

// 某部门的完整层级路径名（如“研发部 / 硬件组”）
function deptPath(deptId, nodes = mockData.departments, trail = []) {
    for (const node of nodes) {
        const next = trail.concat(node.name);
        if (node.id === deptId) return next.join(' / ');
        if (node.children) {
            const found = deptPath(deptId, node.children, next);
            if (found) return found;
        }
    }
    return null;
}

// 收集某节点及其所有后代的 id
function collectDeptIds(node, acc = []) {
    acc.push(node.id);
    if (node.children) node.children.forEach(c => collectDeptIds(c, acc));
    return acc;
}

// 取某部门及其所有后代的 id 集合（层级上卷：选父部门含子部门资产）
function getDeptAndDescendants(deptId, nodes = mockData.departments) {
    for (const node of nodes) {
        if (node.id === deptId) return collectDeptIds(node);
        if (node.children) {
            const found = getDeptAndDescendants(deptId, node.children);
            if (found) return found;
        }
    }
    return null;
}

// 当前查看范围内的资产（部门隔离视图）
function getScopedAssets() {
    if (currentDeptScope === 'all') return mockData.assets;
    const ids = getDeptAndDescendants(currentDeptScope) || [currentDeptScope];
    return mockData.assets.filter(a => ids.includes(a.deptId));
}

// 查看范围 + 筛选条件后的可见资产（资产列表列表视图用）
function getVisibleAssets() {
    let list = getScopedAssets();
    const f = currentFilters || {};
    if (f.assetNo) list = list.filter(a => a.assetNo.includes(f.assetNo));
    if (f.assetName) list = list.filter(a => a.name.includes(f.assetName));
    if (f.category) list = list.filter(a => a.category === f.category);
    if (f.status) list = list.filter(a => a.status === f.status);
    if (f.dept) {
        const ids = getDeptAndDescendants(f.dept) || [f.dept];
        list = list.filter(a => ids.includes(a.deptId));
    }
    return list;
}

// 填充顶栏“查看范围”下拉（登录后调用）
function populateDeptScope() {
    const select = document.getElementById('deptScopeSelect');
    if (!select) return;
    const flat = flattenDepartments();
    select.innerHTML = '<option value="all">全部部门</option>' +
        flat.map(d => `<option value="${d.id}">${'　'.repeat(d.depth)}${d.name}</option>`).join('');
    select.value = currentDeptScope;
}

// 切换查看范围
function switchDeptScope(value) {
    currentDeptScope = value;
    const label = value === 'all' ? '全部部门' : (deptPath(value) || value);
    showToast(`查看范围：${label}`, 'info');
    showPage(currentPage);
}

// 递归渲染部门树（部门管理页，支持任意层级）
function renderDepartmentTree(nodes, depth = 0) {
    return nodes.map(dept => `
        <div style="margin-top:8px;">
            <div style="padding:12px 15px;border-radius:6px;display:flex;justify-content:space-between;align-items:center;gap:12px;margin-left:${depth * 28}px;${depth === 0 ? 'background:#fafafa;border:1px solid #e8e8e8;' : 'background:#fff;border-left:2px solid #e8e8e8;'}">
                <span style="font-size:${depth === 0 ? '16px' : '14px'};font-weight:${depth === 0 ? '600' : '500'};">
                    ${depth > 0 ? '├─ ' : '🏢 '}${dept.name}
                    <span style="color:#999;font-size:12px;font-weight:normal;margin-left:6px;">编码 ${dept.code} · 负责人 ${dept.manager} · ${dept.assetCount} 件</span>
                    <span class="badge-status ${dept.status === 'active' ? 'badge-success' : 'badge-warning'}" style="margin-left:6px;">${dept.status === 'active' ? '启用' : '停用'}</span>
                </span>
                <span style="white-space:nowrap;">
                    <button class="btn btn-info btn" onclick="showAdminEditor('部门', '编辑', '${dept.name}')">编辑</button>
                    <button class="btn btn-success btn" onclick="showAdminEditor('子部门', '新增', '')">+ 子部门</button>
                    <button class="btn btn-danger btn" onclick="deleteItem('部门', '${dept.name}')">删除</button>
                </span>
            </div>
            ${dept.children && dept.children.length ? renderDepartmentTree(dept.children, depth + 1) : ''}
        </div>
    `).join('');
}

// 导出功能
function exportData(type) {
    showToast(`正在生成${type}文件`, 'info');
    setTimeout(() => {
        showNotice('导出完成', `${type}文件已生成，可在下载中心查看。`, 'success');
    }, 500);
}

// 批量操作
function batchOperation(action) {
    const checkboxes = document.querySelectorAll('input[type="checkbox"]:checked');
    if (checkboxes.length === 0) {
        showNotice('请选择资产', `请先勾选要${action}的资产。`, 'warning');
        return;
    }

    showConfirm(`确认${action}？`, `将对 ${checkboxes.length} 个已选项目执行${action}操作。`, () => {
        showNotice(`${action}完成`, `已处理 ${checkboxes.length} 个项目。`, 'success');
    });
}

// ========== 资产分类编码树（逐层构建 + 增删改查） ==========

// 当前钻取路径对应的：当前层节点、父节点、路径节点链
function getTreeContext() {
    let level = mockData.assetTree;
    let parent = null;
    const trail = [];
    for (const id of currentTreePath) {
        const node = (level || []).find(n => n.id === id);
        if (!node) break;
        parent = node;
        trail.push(node);
        level = node.children || [];
    }
    return { nodes: level || [], parent, trail };
}

// 递归统计节点下资产总数
function countNodeAssets(node) {
    let n = (node.assets || []).length;
    (node.children || []).forEach(c => { n += countNodeAssets(c); });
    return n;
}

// 当前父节点的 children 数组（根则 assetTree 本身）
function currentChildArray() {
    const { parent } = getTreeContext();
    if (!parent) return mockData.assetTree;
    if (!parent.children) parent.children = [];
    return parent.children;
}

// 渲染资产列表层级视图（面包屑 + 子分类卡片 + CRUD + 末端资产）
function renderHierarchyLevel() {
    const { nodes, parent, trail } = getTreeContext();

    let crumbs = `<a href="#" onclick="drillToPath(-1);return false;">📁 全部分类</a>`;
    trail.forEach((n, i) => {
        crumbs += ` / <a href="#" onclick="drillToPath(${i});return false;">${n.name} <span style="color:#722ed1">(${n.code})</span></a>`;
    });

    const back = parent ? `<button class="btn btn-default" onclick="drillToPath(${currentTreePath.length - 2})">← 返回上一层</button>` : '';
    const addCat = `<button class="btn btn-success" onclick="addTreeNode()">+ 新增${parent ? '子分类' : '分类'}</button>`;
    const addAsset = parent ? `<button class="btn btn-info" onclick="showAddAssetToNode('${parent.id}')">+ 新增资产</button>` : '';

    const cards = nodes.map(node => `
        <div class="hierarchy-card" onclick="drillInto('${node.id}')">
            <div class="hierarchy-card-title">${node.name}</div>
            <div style="text-align:center;color:#722ed1;font-weight:700;margin-bottom:10px;letter-spacing:0.5px">${node.code}</div>
            <div class="hierarchy-card-info">
                <div><span>📁 子分类</span><span>${(node.children || []).length} 个</span></div>
                <div><span>📦 资产数</span><span>${countNodeAssets(node)} 件</span></div>
            </div>
            <div class="tree-card-ops" onclick="event.stopPropagation()">
                <button class="btn btn-info btn" onclick="editTreeNode('${node.id}')">编辑</button>
                <button class="btn btn-danger btn" onclick="deleteTreeNode('${node.id}')">删除</button>
            </div>
            <div class="hierarchy-card-action">点击进入 →</div>
        </div>
    `).join('');

    let assetsTable = '';
    if (parent && parent.assets && parent.assets.length) {
        assetsTable = `
            <div class="card" style="margin-top:16px">
                <div class="card-title">📦 ${parent.code} 下的资产（${parent.assets.length}）</div>
                <table>
                    <thead><tr><th>资产编号</th><th>名称</th><th>规格型号</th><th>状态</th><th>保管人</th></tr></thead>
                    <tbody>
                        ${parent.assets.map(a => `
                            <tr>
                                <td>${a.assetNo}</td><td>${a.name}</td><td>${a.model || '-'}</td>
                                <td><span class="badge-status ${a.status === 'available' ? 'badge-success' : 'badge-warning'}">${a.status === 'available' ? '在库' : '借出中'}</span></td>
                                <td>${a.custodian || '-'}</td>
                            </tr>`).join('')}
                    </tbody>
                </table>
            </div>`;
    }

    const empty = (!nodes.length && !(parent && parent.assets && parent.assets.length))
        ? `<p style="color:#999;padding:30px;text-align:center">该分类下暂无子分类与资产，可点击上方按钮新增</p>` : '';

    return `
        <div class="tree-crumb">${crumbs}</div>
        <div class="tree-toolbar">${back}${addCat}${addAsset}</div>
        <div class="hierarchy-cards">${cards}</div>
        ${empty}
        ${assetsTable}
    `;
}

function drillInto(nodeId) {
    currentTreePath.push(nodeId);
    currentAssetView = 'hierarchy';
    showPage('asset-list');
}

function drillToPath(index) {
    currentTreePath = index < 0 ? [] : currentTreePath.slice(0, index + 1);
    currentAssetView = 'hierarchy';
    showPage('asset-list');
}

function treeNodeEditorModal(title, node, onSave) {
    const { parent } = getTreeContext();
    const parentCode = parent ? parent.code : '';
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">${title}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <form id="treeNodeForm">
                    <div class="form-group"><label>* 分类名称</label><input type="text" id="tnName" required value="${node ? node.name : ''}" placeholder="如：三菱 MITSUBISHI"></div>
                    <div class="form-group"><label>* 本层编码段</label>
                        <input type="text" id="tnSeg" required value="${node ? node.codeSeg : ''}" placeholder="如：MIT" oninput="updateCodePreview('${parentCode}')">
                    </div>
                    <div class="form-group"><label>完整编码（自动生成）</label>
                        <div id="tnPreview" class="code-preview">${parentCode ? parentCode + '-' : ''}${node ? node.codeSeg : ''}</div>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="${onSave}">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function updateCodePreview(parentCode) {
    const seg = document.getElementById('tnSeg').value.trim();
    const el = document.getElementById('tnPreview');
    if (el) el.textContent = (parentCode ? parentCode + '-' : '') + seg;
}

function addTreeNode() {
    treeNodeEditorModal('新增分类节点', null, 'confirmAddTreeNode()');
}

function confirmAddTreeNode() {
    const form = document.getElementById('treeNodeForm');
    if (form && !form.checkValidity()) { form.reportValidity(); return; }
    const { parent } = getTreeContext();
    const name = document.getElementById('tnName').value.trim();
    const seg = document.getElementById('tnSeg').value.trim();
    const code = (parent ? parent.code + '-' : '') + seg;
    const arr = currentChildArray();
    if (arr.some(n => n.id === code)) { showNotice('编码重复', `编码 ${code} 已存在，请更换编码段。`, 'warning'); return; }
    arr.push({ id: code, name, codeSeg: seg, code, children: [], assets: [] });
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('asset-list');
    showToast('已新增分类：' + name + '（' + code + '）', 'success');
}

function editTreeNode(nodeId) {
    const { nodes } = getTreeContext();
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;
    treeNodeEditorModal('编辑分类节点', node, `confirmEditTreeNode('${nodeId}')`);
}

function confirmEditTreeNode(nodeId) {
    const form = document.getElementById('treeNodeForm');
    if (form && !form.checkValidity()) { form.reportValidity(); return; }
    const { nodes, parent } = getTreeContext();
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;
    node.name = document.getElementById('tnName').value.trim();
    node.codeSeg = document.getElementById('tnSeg').value.trim();
    recalcCode(node, parent ? parent.code : '');
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('asset-list');
    showToast('分类已更新：' + node.name + '（' + node.code + '）', 'success');
}

// 级联重算 node 及其子孙的完整编码与资产编号
function recalcCode(node, parentCode) {
    node.code = (parentCode ? parentCode + '-' : '') + node.codeSeg;
    node.id = node.code;
    (node.children || []).forEach(c => recalcCode(c, node.code));
    (node.assets || []).forEach((a, i) => { a.assetNo = node.code + '-' + String(i + 1).padStart(3, '0'); });
}

function deleteTreeNode(nodeId) {
    const { nodes } = getTreeContext();
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;
    const sub = (node.children || []).length, ast = countNodeAssets(node);
    const warn = (sub || ast) ? `该分类下含 ${sub} 个子分类、${ast} 件资产，将一并删除。` : '确认删除该分类？';
    showConfirm('确认删除分类？', `「${node.name}（${node.code}）」${warn}`, () => {
        const arr = currentChildArray();
        const idx = arr.findIndex(n => n.id === nodeId);
        if (idx >= 0) arr.splice(idx, 1);
        showPage('asset-list');
        showToast('分类已删除', 'success');
    }, { confirmClass: 'btn-danger', type: 'danger' });
}

// 末端节点新增资产（编号自动拼接 = 节点编码 + 流水号）
function showAddAssetToNode(nodeId) {
    const { parent } = getTreeContext();
    if (!parent || parent.id !== nodeId) return;
    const presetNo = parent.code + '-' + String((parent.assets || []).length + 1).padStart(3, '0');
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">新增资产 · ${parent.code}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <form id="treeAssetForm">
                    <div class="form-group"><label>资产编号（自动生成）</label><div class="code-preview">${presetNo}</div></div>
                    <div class="form-group"><label>* 资产名称</label><input type="text" id="taName" required placeholder="如：三菱Q系列PLC"></div>
                    <div class="form-group"><label>规格型号</label><input type="text" id="taModel" placeholder="如：Q06UDVCPU"></div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="confirmAddAssetToNode('${nodeId}','${presetNo}')">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function confirmAddAssetToNode(nodeId, presetNo) {
    const form = document.getElementById('treeAssetForm');
    if (form && !form.checkValidity()) { form.reportValidity(); return; }
    const { parent } = getTreeContext();
    if (!parent) return;
    if (!parent.assets) parent.assets = [];
    parent.assets.push({
        assetNo: presetNo,
        name: document.getElementById('taName').value.trim(),
        model: document.getElementById('taModel').value.trim(),
        status: 'available', custodian: null
    });
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('asset-list');
    showToast('已新增资产：' + presetNo, 'success');
}

// 显示资产详情
function showAssetDetail(assetId) {
    const asset = mockData.assets.find(a => a.id === assetId);
    if (!asset) return;

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">资产详情</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <div class="detail-box">
                    <div class="detail-title">基本信息</div>
                    <div class="detail-grid">
                        <div class="detail-item">
                            <div class="detail-label">资产编号</div>
                            <div class="detail-value">${asset.assetNo}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">资产名称</div>
                            <div class="detail-value">${asset.name}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">类别</div>
                            <div class="detail-value">${asset.category}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">规格型号</div>
                            <div class="detail-value">${asset.model}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">品牌</div>
                            <div class="detail-value">${asset.brand}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">数量</div>
                            <div class="detail-value">${asset.quantity} 台</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">单价</div>
                            <div class="detail-value">¥${asset.price.toLocaleString()}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">当前状态</div>
                            <div class="detail-value">
                                <span class="badge-status ${asset.status === 'available' ? 'badge-success' : 'badge-warning'}">
                                    ${asset.status === 'available' ? '🟢 在库' : '🟠 借出中'}
                                </span>
                            </div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">当前保管人</div>
                            <div class="detail-value">${asset.custodian || '-'}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">存放位置</div>
                            <div class="detail-value">${asset.location}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">归属部门</div>
                            <div class="detail-value">${asset.deptName || '-'}</div>
                        </div>
                    </div>
                </div>
                <div class="detail-box">
                    <div class="detail-title">操作记录</div>
                    <div style="padding:20px;text-align:center;color:#999">暂无操作记录</div>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">关闭</button>
                ${asset.status === 'available' ? '<button class="btn btn-success" onclick="this.closest(\'.modal\').remove();showBorrowModal(' + asset.id + ')">借用</button>' : ''}
                ${asset.status === 'available' ? '<button class="btn btn-warning" onclick="this.closest(\'.modal\').remove();showTransferModal(' + asset.id + ')">转让</button>' : ''}
                <button class="btn btn-info" onclick="showEditAssetModal(${asset.id})">编辑</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 显示编辑资产弹窗
function showEditAssetModal(assetId) {
    const asset = mockData.assets.find(a => a.id === assetId);
    if (!asset) return;

    // 关闭详情弹窗
    document.querySelectorAll('.modal').forEach(m => m.remove());

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">编辑资产</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="editAssetForm">
                    <div class="detail-grid">
                        <div class="form-group">
                            <label>* 资产类别</label>
                            <select required>
                                <option value="${asset.category}" selected>${asset.category}</option>
                                <option>工具</option>
                                <option>仪器仪表</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>* 资产名称</label>
                            <input type="text" value="${asset.name}" required>
                        </div>
                        <div class="form-group">
                            <label>规格型号</label>
                            <input type="text" value="${asset.model}">
                        </div>
                        <div class="form-group">
                            <label>品牌</label>
                            <input type="text" value="${asset.brand}">
                        </div>
                        <div class="form-group">
                            <label>* 数量</label>
                            <input type="number" value="${asset.quantity}" required>
                        </div>
                        <div class="form-group">
                            <label>单价(元)</label>
                            <input type="number" value="${asset.price}">
                        </div>
                        <div class="form-group">
                            <label>* 存放位置</label>
                            <select required>
                                <option value="${asset.location}" selected>${asset.location}</option>
                                <option>一号仓库 > B区 > B02货架</option>
                            </select>
                        </div>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="saveModalForm('editAssetForm', '资产已更新', '资产基础信息已保存到当前原型数据视图。')">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 显示审批详情
// ========== 审批工作流引擎（运行时） ==========

const NODE_TYPE_LABEL = {
    start: '发起', approval: '审批', countersign: '会签', orsign: '或签',
    condition: '条件分支', cc: '抄送', end: '结束'
};
const NODE_STATUS_ICON = {
    done: '✅', current: '⏳', pending: '○', skipped: '⊘', rejected: '⛔'
};

function currentNode(flow) {
    return flow.nodes[flow.currentNodeIndex];
}

// 原型无定时任务，超时仅作展示标记（按截止日期字符串简单比较）
function isOverdue(flow) {
    return flow.deadline && flow.deadline < '2024-06-11';
}

// 条件节点判定（演示：amount>N）
function evalCondition(cond, flow) {
    if (!cond) return true;
    const m = cond.match(/amount\s*>\s*(\d+)/);
    if (m) return (flow.amount || 0) > Number(m[1]);
    return true;
}

// 渲染审批时间线
function renderApprovalTimeline(flow) {
    return flow.nodes.map(node => {
        const icon = NODE_STATUS_ICON[node.status] || '○';
        const typeTag = (node.type !== 'start' && node.type !== 'end')
            ? `<span class="wf-node-type">${NODE_TYPE_LABEL[node.type] || ''}</span>` : '';
        let meta = '';
        if (node.status === 'done') {
            meta = `${node.approver || ''}　${node.opinion ? '「' + node.opinion + '」' : ''}　${node.time || ''}`;
        } else if (node.type === 'countersign' || node.type === 'orsign') {
            const all = (node.signers || []).concat(node.addedSigners || []);
            const signed = all.filter(s => (node.signStates || {})[s]);
            meta = `${NODE_TYPE_LABEL[node.type]}：${all.join('、')}${signed.length ? '（已签：' + signed.join('、') + '）' : ''}`;
        } else if (node.status === 'skipped') {
            meta = '不满足条件，已跳过';
        } else if (node.approver) {
            meta = `审批人：${node.approver}${node.addedSigners && node.addedSigners.length ? '；加签：' + node.addedSigners.join('、') : ''}`;
        }
        const condTag = (node.type === 'condition' && node.condition) ? `　·　条件：${node.condition}` : '';
        return `
            <div class="wf-tl-item wf-${node.status}">
                <span class="wf-tl-icon">${icon}</span>
                <div class="wf-tl-body">
                    <div class="wf-tl-title">${node.name} ${typeTag}</div>
                    ${meta ? `<div class="wf-tl-meta">${meta}${condTag}</div>` : (condTag ? `<div class="wf-tl-meta">${condTag}</div>` : '')}
                </div>
            </div>`;
    }).join('');
}

// 推进到下一个有效节点（跳过不满足的条件节点、自动通过抄送），返回是否到达结束
function advanceFlow(flow) {
    let i = flow.currentNodeIndex + 1;
    while (i < flow.nodes.length) {
        const node = flow.nodes[i];
        if (node.type === 'end') {
            node.status = 'done';
            flow.currentNodeIndex = i;
            return true;
        }
        if (node.type === 'condition' && !evalCondition(node.condition, flow)) {
            node.status = 'skipped';
            i++;
            continue;
        }
        if (node.type === 'cc') {
            node.status = 'done';
            node.opinion = '已知会';
            node.time = '刚刚';
            i++;
            continue;
        }
        node.status = 'current';
        flow.currentNodeIndex = i;
        return false;
    }
    return true;
}

// 流程完成后执行业务动作
function applyBizEffect(flow) {
    const asset = mockData.assets.find(a => a.assetNo === flow.assetNo);
    if (!asset) return '';
    if (flow.type === 'borrow') {
        asset.status = 'borrowed';
        asset.custodian = flow.applicant;
        return `资产已借出给 ${flow.applicant}。`;
    }
    if (flow.type === 'transfer') {
        const fromDept = asset.deptName;
        const toUser = mockData.users.find(u => u.name === flow.transferee);
        asset.custodian = flow.transferee;
        const toDeptName = toUser ? (deptPath(toUser.deptId) || flow.transfereeDept) : flow.transfereeDept;
        if (toUser) asset.deptId = toUser.deptId;
        if (toDeptName) asset.deptName = toDeptName;
        return `保管人变更为 ${flow.transferee}，归属部门：${fromDept} → ${asset.deptName}。`;
    }
    if (flow.type === 'return') {
        asset.status = 'available';
        asset.custodian = null;
        return '资产已确认归还入库。';
    }
    return '';
}

// 审批详情（时间线 + 操作）
function showApprovalDetail(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const wf = mockData.workflows.find(w => w.id === flow.workflowId);
    const node = currentNode(flow);

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content" style="max-width:640px;">
            <div class="modal-header">
                <h3 class="modal-title">审批详情 · ${wf ? wf.name : ''}</h3>
                <span class="modal-close" onclick="closeModal(this)">&times;</span>
            </div>
            <div class="modal-body">
                <div class="detail-box">
                    <div class="detail-grid">
                        <div class="detail-item"><div class="detail-label">工单号</div><div class="detail-value">${flow.flowNo}</div></div>
                        <div class="detail-item"><div class="detail-label">类型</div><div class="detail-value">${flow.type === 'transfer' ? '转让' : flow.type === 'return' ? '归还' : '借用'}申请</div></div>
                        <div class="detail-item"><div class="detail-label">申请人</div><div class="detail-value">${flow.applicant}（${flow.applicantDept}）</div></div>
                        <div class="detail-item"><div class="detail-label">资产</div><div class="detail-value">${flow.assetNo} ${flow.assetName}</div></div>
                        <div class="detail-item"><div class="detail-label">申报金额</div><div class="detail-value">¥${(flow.amount || 0).toLocaleString()}</div></div>
                        <div class="detail-item"><div class="detail-label">审批截止</div><div class="detail-value">${flow.deadline}${isOverdue(flow) ? ' <span class="badge-status badge-danger">已超时</span>' : ''}</div></div>
                    </div>
                    <p style="margin-top:12px;color:#666;line-height:1.7">${flow.type === 'transfer' ? '转让原因' : '申请原因'}：${flow.reason}${flow.type === 'transfer' && flow.transferee ? `；接收人：${flow.transferee}（${flow.transfereeDept || ''}）` : ''}</p>
                </div>
                <div class="detail-box">
                    <div class="detail-title">审批流转（当前节点：${node ? node.name : '—'}）</div>
                    <div class="wf-timeline">${renderApprovalTimeline(flow)}</div>
                </div>
            </div>
            <div class="modal-footer" style="flex-wrap:wrap;gap:8px;">
                <button class="btn btn-default" onclick="closeModal(this)">关闭</button>
                <button class="btn btn-info btn" onclick="closeModal(this);showAddSignModal(${flow.id})">加签</button>
                <button class="btn btn-info btn" onclick="closeModal(this);showTransignModal(${flow.id})">转签</button>
                <button class="btn btn-danger" onclick="closeModal(this);handleReject(${flow.id})">驳回</button>
                <button class="btn btn-success" onclick="closeModal(this);handleApprove(${flow.id})">通过当前节点</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 通过当前节点
function handleApprove(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);

    let signerPick = '';
    if (node.type === 'countersign' || node.type === 'orsign') {
        const pending = (node.signers || []).concat(node.addedSigners || []).filter(s => !(node.signStates || {})[s]);
        signerPick = `
            <div class="form-group">
                <label>${NODE_TYPE_LABEL[node.type]} — 以谁的身份通过</label>
                <select id="signerSelect">${pending.map(s => `<option>${s}</option>`).join('')}</select>
            </div>`;
    }

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">通过节点：${node.name}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <div class="detail-box"><p style="color:#666">工单 ${flow.flowNo} · ${node.name}（${NODE_TYPE_LABEL[node.type]}）</p></div>
                <form id="approveForm">
                    ${signerPick}
                    <div class="form-group"><label>审批意见（选填）</label><textarea placeholder="请输入审批意见(最多200字)" maxlength="200"></textarea></div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="confirmApprove(${flow.id})">确认通过</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function confirmApprove(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);
    const opinionEl = document.querySelector('#approveForm textarea');
    const opinion = (opinionEl && opinionEl.value) ? opinionEl.value : '同意';

    // 会签/或签：记录签署，会签需全员通过才推进
    if (node.type === 'countersign' || node.type === 'orsign') {
        const signerEl = document.getElementById('signerSelect');
        const signer = signerEl ? signerEl.value : (node.approver || '');
        node.signStates = node.signStates || {};
        node.signStates[signer] = true;
        const all = (node.signers || []).concat(node.addedSigners || []);
        const remaining = all.filter(s => !node.signStates[s]);
        if (node.type === 'countersign' && remaining.length) {
            document.querySelectorAll('.modal').forEach(m => m.remove());
            showPage('approval-pending');
            showNotice('会签已记录', `${signer} 已通过；仍需 ${remaining.join('、')} 会签后方可推进。`, 'info');
            return;
        }
    }

    node.status = 'done';
    node.opinion = opinion;
    node.time = '刚刚';

    const finished = advanceFlow(flow);
    document.querySelectorAll('.modal').forEach(m => m.remove());

    if (finished) {
        flow.status = 'approved';
        const bizMsg = applyBizEffect(flow);
        mockData.approvals = mockData.approvals.filter(f => f.id !== flow.id);
        showPage('approval-pending');
        showNotice('流程审批完成', `工单 ${flow.flowNo} 全部节点已通过。${bizMsg}`, 'success');
    } else {
        showPage('approval-pending');
        const next = currentNode(flow);
        showNotice('已通过当前节点', `流程已流转至「${next.name}」节点，审批人：${next.approver || '—'}。`, 'success');
    }
}

// 驳回
function handleReject(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">驳回节点：${currentNode(flow).name}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <div class="detail-box"><p style="color:#666">工单 ${flow.flowNo} · 申请人 ${flow.applicant} · ${flow.assetNo} ${flow.assetName}</p></div>
                <form id="rejectForm">
                    <div class="form-group"><label>* 驳回理由（必填，10-200字）</label><textarea placeholder="请输入驳回理由" required minlength="10" maxlength="200"></textarea></div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-danger" onclick="confirmReject(${flow.id})">确认驳回</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function confirmReject(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    const form = document.getElementById('rejectForm');
    if (form && !form.checkValidity()) { form.reportValidity(); return; }
    if (flow) {
        currentNode(flow).status = 'rejected';
        flow.status = 'rejected';
        mockData.approvals = mockData.approvals.filter(f => f.id !== flow.id);
    }
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('approval-pending');
    showNotice('审批已驳回', '驳回理由已记录，流程终止并通知申请人。', 'success');
}

// 加签
function showAddSignModal(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);
    const candidates = mockData.users.filter(u => u.status === 'active').map(u => u.name);
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">加签 · ${node.name}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <form id="addSignForm">
                    <div class="form-group"><label>加签方式</label>
                        <select id="signMode"><option value="after">后加签（当前审批人之后）</option><option value="before">前加签（先于当前审批人）</option></select>
                    </div>
                    <div class="form-group"><label>* 加签审批人</label>
                        <select id="signWho" required>${candidates.map(c => `<option>${c}</option>`).join('')}</select>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="confirmAddSign(${flow.id})">确认加签</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function confirmAddSign(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);
    const who = document.getElementById('signWho').value;
    const mode = document.getElementById('signMode').value;
    node.addedSigners = node.addedSigners || [];
    node.addedSigners.push(who);
    if (node.type === 'countersign' || node.type === 'orsign') {
        node.signers = node.signers || [];
        node.signers.push(who);
    }
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('approval-pending');
    showNotice('加签成功', `已为「${node.name}」节点${mode === 'before' ? '前' : '后'}加签 ${who}，需其审批后方可推进。`, 'success');
}

// 转签
function showTransignModal(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);
    const candidates = mockData.users.filter(u => u.status === 'active').map(u => u.name);
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">转签 · ${node.name}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">
                <div class="detail-box"><p style="color:#666">当前审批人：${node.approver || '—'}</p></div>
                <form id="transignForm">
                    <div class="form-group"><label>* 转交给</label>
                        <select id="transignWho" required>${candidates.map(c => `<option>${c}</option>`).join('')}</select>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="confirmTransign(${flow.id})">确认转签</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function confirmTransign(flowId) {
    const flow = mockData.approvals.find(f => f.id === flowId);
    if (!flow) return;
    const node = currentNode(flow);
    const who = document.getElementById('transignWho').value;
    const old = node.approver;
    node.approver = who;
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('approval-pending');
    showNotice('转签成功', `「${node.name}」节点审批人由 ${old || '—'} 转交给 ${who}。`, 'success');
}

// ========== 审批流程设计器 ==========

const NODE_TYPE_COLOR = {
    start: '#1890ff', approval: '#52c41a', countersign: '#722ed1',
    orsign: '#fa8c16', condition: '#eb2f96', cc: '#13c2c2', end: '#8c8c8c'
};

function getCurrentWorkflow() {
    return mockData.workflows.find(w => w.id === currentWorkflowId) || mockData.workflows[0];
}

function switchWorkflow(id) {
    currentWorkflowId = id;
    showPage('admin-workflows');
}

function renderWorkflowDesigner() {
    const wf = getCurrentWorkflow();
    if (!wf) return '';
    return wf.nodes.map((node, i) => {
        const isStart = node.type === 'start', isEnd = node.type === 'end';
        const color = NODE_TYPE_COLOR[node.type] || '#52c41a';
        const arrow = i > 0 ? `<div class="wf-d-arrow">▼${node.type === 'condition' && node.condition ? ` <span style="color:#eb2f96">[${node.condition}]</span>` : ''}</div>` : '';
        const sub = (isStart || isEnd) ? ('流程' + (isStart ? '起点' : '终点'))
            : '审批人：' + (node.approver || '—') + ((node.type === 'countersign' || node.type === 'orsign') && node.signers && node.signers.length ? '（' + node.signers.join('、') + '）' : '');
        const ops = (!isStart && !isEnd) ? `
            <div class="wf-d-ops">
                <button class="btn btn-default btn" title="上移" onclick="moveWorkflowNode('${node.id}',-1)">↑</button>
                <button class="btn btn-default btn" title="下移" onclick="moveWorkflowNode('${node.id}',1)">↓</button>
                <button class="btn btn-info btn" onclick="editWorkflowNode('${node.id}')">配置</button>
                <button class="btn btn-danger btn" onclick="deleteWorkflowNode('${node.id}')">删除</button>
            </div>` : '';
        const addBtn = !isEnd ? `<div class="wf-d-add"><button class="btn btn-success btn" onclick="addWorkflowNode('${node.id}')">+ 在此下方加节点</button></div>` : '';
        return `
            ${arrow}
            <div class="wf-d-node" style="border-left:5px solid ${color}">
                <div class="wf-d-node-head">
                    <span class="wf-d-name">${node.name}</span>
                    <span class="wf-node-type" style="background:${color}">${NODE_TYPE_LABEL[node.type]}</span>
                </div>
                <div class="wf-d-sub">${sub}</div>
                ${ops}
            </div>
            ${addBtn}
        `;
    }).join('');
}

function nodeEditorForm(node) {
    const at = node ? node.approverType : 'user';
    const type = node ? node.type : 'approval';
    const userOpts = mockData.users.filter(u => u.status === 'active').map(u => u.name);
    const typeOpts = [['approval', '审批（单人）'], ['countersign', '会签（都通过）'], ['orsign', '或签（一人通过）'], ['condition', '条件分支'], ['cc', '抄送']];
    const srcOpts = [['user', '指定人'], ['role', '按角色'], ['supervisor', '直属主管'], ['deptManager', '部门负责人'], ['applicantPick', '发起人指定']];
    return `
        <form id="nodeForm">
            <div class="form-group"><label>* 节点名称</label><input type="text" id="ndName" required value="${node ? node.name : ''}" placeholder="如：部门经理审批"></div>
            <div class="detail-grid">
                <div class="form-group"><label>节点类型</label>
                    <select id="ndType" onchange="toggleNodeCondition()">
                        ${typeOpts.map(([v, l]) => `<option value="${v}" ${type === v ? 'selected' : ''}>${l}</option>`).join('')}
                    </select>
                </div>
                <div class="form-group"><label>审批人来源</label>
                    <select id="ndApproverType">
                        ${srcOpts.map(([v, l]) => `<option value="${v}" ${at === v ? 'selected' : ''}>${l}</option>`).join('')}
                    </select>
                </div>
            </div>
            <div class="form-group"><label>审批人 / 会签成员（多人用逗号分隔）</label>
                <input type="text" id="ndApprover" value="${node ? (node.signers && node.signers.length ? node.signers.join('，') : (node.approver || '')) : ''}" placeholder="如：张三，赵敏" list="ndUserList">
                <datalist id="ndUserList">${userOpts.map(u => `<option value="${u}">`).join('')}</datalist>
            </div>
            <div class="form-group" id="ndCondWrap" style="display:${type === 'condition' ? 'block' : 'none'}"><label>条件表达式</label>
                <input type="text" id="ndCond" value="${node && node.condition ? node.condition : 'amount>5000'}" placeholder="如：amount>5000">
                <p style="margin-top:4px;font-size:12px;color:#999">满足条件才经过此节点，否则自动跳过（原型支持 amount&gt;数字）</p>
            </div>
        </form>
    `;
}

function toggleNodeCondition() {
    const t = document.getElementById('ndType').value;
    const wrap = document.getElementById('ndCondWrap');
    if (wrap) wrap.style.display = (t === 'condition') ? 'block' : 'none';
}

function nodeEditorModal(title, node, onSaveCall) {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header"><h3 class="modal-title">${title}</h3><span class="modal-close" onclick="closeModal(this)">&times;</span></div>
            <div class="modal-body">${nodeEditorForm(node)}</div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="closeModal(this)">取消</button>
                <button class="btn btn-success" onclick="${onSaveCall}">保存节点</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function readNodeForm() {
    const form = document.getElementById('nodeForm');
    if (form && !form.checkValidity()) { form.reportValidity(); return null; }
    const type = document.getElementById('ndType').value;
    const approverRaw = document.getElementById('ndApprover').value.trim();
    const parts = approverRaw.split(/[，,]/).map(s => s.trim()).filter(Boolean);
    const node = {
        name: document.getElementById('ndName').value.trim(),
        type,
        approverType: document.getElementById('ndApproverType').value,
        approver: parts[0] || ''
    };
    if (type === 'countersign' || type === 'orsign') node.signers = parts.length ? parts : [node.approver].filter(Boolean);
    if (type === 'condition') node.condition = document.getElementById('ndCond').value.trim();
    return node;
}

function addWorkflowNode(afterId) {
    nodeEditorModal('新增节点', null, `confirmAddWorkflowNode('${afterId}')`);
}

function confirmAddWorkflowNode(afterId) {
    const data = readNodeForm();
    if (!data) return;
    const wf = getCurrentWorkflow();
    data.id = 'n' + Date.now().toString().slice(-6);
    const idx = wf.nodes.findIndex(n => n.id === afterId);
    wf.nodes.splice(idx + 1, 0, data);
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('admin-workflows');
    showToast('已新增节点：' + data.name, 'success');
}

function editWorkflowNode(nodeId) {
    const wf = getCurrentWorkflow();
    const node = wf.nodes.find(n => n.id === nodeId);
    if (!node || node.type === 'start' || node.type === 'end') return;
    nodeEditorModal('配置节点', node, `confirmEditWorkflowNode('${nodeId}')`);
}

function confirmEditWorkflowNode(nodeId) {
    const data = readNodeForm();
    if (!data) return;
    const wf = getCurrentWorkflow();
    const node = wf.nodes.find(n => n.id === nodeId);
    node.name = data.name;
    node.type = data.type;
    node.approverType = data.approverType;
    node.approver = data.approver;
    node.signers = data.signers;
    node.condition = data.condition;
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showPage('admin-workflows');
    showToast('节点已更新：' + data.name, 'success');
}

function moveWorkflowNode(nodeId, dir) {
    const wf = getCurrentWorkflow();
    const i = wf.nodes.findIndex(n => n.id === nodeId);
    const j = i + dir;
    if (j <= 0 || j >= wf.nodes.length - 1) { showToast('已到边界，发起与结束节点固定', 'info'); return; }
    const tmp = wf.nodes[i]; wf.nodes[i] = wf.nodes[j]; wf.nodes[j] = tmp;
    showPage('admin-workflows');
}

function deleteWorkflowNode(nodeId) {
    const wf = getCurrentWorkflow();
    const node = wf.nodes.find(n => n.id === nodeId);
    if (!node || node.type === 'start' || node.type === 'end') return;
    showConfirm('确认删除节点？', `节点「${node.name}」将从流程中移除。`, () => {
        wf.nodes = wf.nodes.filter(n => n.id !== nodeId);
        showPage('admin-workflows');
        showToast('节点已删除', 'success');
    }, { confirmClass: 'btn-danger', type: 'danger' });
}

function saveWorkflow() {
    showNotice('流程已保存', `「${getCurrentWorkflow().name}」的节点配置已在当前原型中保存，新发起的申请将按此流程流转。`, 'success');
}

// 显示借用申请弹窗
function showBorrowModal(assetId) {
    const asset = mockData.assets.find(a => a.id === assetId);
    if (!asset) return;

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">发起借用申请</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <div class="detail-box">
                    <div class="detail-title">资产信息</div>
                    <div class="detail-grid">
                        <div class="detail-item">
                            <div class="detail-label">资产编号</div>
                            <div class="detail-value">${asset.assetNo}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">资产名称</div>
                            <div class="detail-value">${asset.name}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">当前状态</div>
                            <div class="detail-value"><span class="badge-status badge-success">🟢 在库</span></div>
                        </div>
                    </div>
                </div>
                <form id="borrowForm">
                    <div class="form-group">
                        <label>* 借用原因</label>
                        <textarea placeholder="请详细说明借用原因(10-200字)" required minlength="10" maxlength="200"></textarea>
                    </div>
                    <div class="form-group">
                        <label>* 预计归还日期</label>
                        <input type="date" required>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="handleSubmitBorrow()">提交申请</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 提交借用申请
function handleSubmitBorrow() {
    const form = document.getElementById('borrowForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showNotice('借用申请已提交', '申请已进入审批流程，请在“我的申请”中查看进度。', 'success');
    } else {
        form.reportValidity();
    }
}

// 显示转让申请弹窗（保管责任永久变更，无时间限制）
function showTransferModal(assetId) {
    const asset = mockData.assets.find(a => a.id === assetId);
    if (!asset) return;

    const recipientOptions = mockData.users
        .filter(u => u.status === 'active' && u.name !== asset.custodian)
        .map(u => `<option value="${u.name}">${u.name}（${u.dept} / ${u.roleName}）</option>`)
        .join('');

    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">发起转让申请</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <div class="detail-box">
                    <div class="detail-title">资产信息</div>
                    <div class="detail-grid">
                        <div class="detail-item">
                            <div class="detail-label">资产编号</div>
                            <div class="detail-value">${asset.assetNo}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">资产名称</div>
                            <div class="detail-value">${asset.name}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">当前保管人</div>
                            <div class="detail-value">${asset.custodian || '-'}</div>
                        </div>
                        <div class="detail-item">
                            <div class="detail-label">当前状态</div>
                            <div class="detail-value"><span class="badge-status badge-success">🟢 在库</span></div>
                        </div>
                    </div>
                </div>
                <form id="transferForm">
                    <div class="form-group">
                        <label>* 接收人</label>
                        <select required>
                            <option value="">请选择接收人</option>
                            ${recipientOptions}
                        </select>
                    </div>
                    <div class="form-group">
                        <label>* 转让原因</label>
                        <textarea placeholder="请详细说明转让原因(10-200字)" required minlength="10" maxlength="200"></textarea>
                    </div>
                    <p class="hint">转让为保管责任的永久变更，无时间限制、无需归还；审批通过后资产归属部门将变更为接收人所在部门。</p>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-warning" onclick="handleSubmitTransfer()">提交申请</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 提交转让申请
function handleSubmitTransfer() {
    const form = document.getElementById('transferForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showNotice('转让申请已提交', '申请已进入审批流程，审批通过后保管人与归属部门都将变更为接收人一方。请在“我的申请”中查看进度。', 'success');
    } else {
        form.reportValidity();
    }
}

// 显示新增资产弹窗
function showAddAssetModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">新增资产</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="addAssetForm">
                    <div class="detail-grid">
                        <div class="form-group">
                            <label>* 资产类别</label>
                            <select required>
                                <option value="">请选择</option>
                                <option>电气设备</option>
                                <option>工具</option>
                                <option>仪器仪表</option>
                            </select>
                            <p style="margin-top:5px;font-size:12px;color:#999">编号预览: PLC-MIT-Q-004 (自动生成)</p>
                        </div>
                        <div class="form-group">
                            <label>* 资产名称</label>
                            <input type="text" placeholder="请输入资产名称" required>
                        </div>
                        <div class="form-group">
                            <label>规格型号</label>
                            <input type="text" placeholder="请输入规格型号">
                        </div>
                        <div class="form-group">
                            <label>品牌</label>
                            <input type="text" placeholder="请输入品牌">
                        </div>
                        <div class="form-group">
                            <label>* 数量</label>
                            <input type="number" placeholder="请输入数量" value="1" required>
                        </div>
                        <div class="form-group">
                            <label>单价(元)</label>
                            <input type="number" placeholder="请输入单价">
                        </div>
                        <div class="form-group">
                            <label>供应商</label>
                            <input type="text" placeholder="请输入供应商">
                        </div>
                        <div class="form-group">
                            <label>* 存放位置</label>
                            <select required>
                                <option value="">请选择</option>
                                <option>一号仓库 > A区 > A01货架</option>
                                <option>一号仓库 > B区 > B02货架</option>
                            </select>
                        </div>
                    </div>
                    <div class="form-group" style="margin-top:20px">
                        <label>备注</label>
                        <textarea placeholder="请输入备注(最多500字)"></textarea>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="saveModalForm('addAssetForm', '资产已新增', '资产编号已按规则生成，并加入当前资产列表。', 'asset-list')">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 显示批量导入弹窗
function showImportModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">批量导入资产</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <div class="detail-box">
                    <div class="detail-title">第一步：下载模板</div>
                    <p style="margin-bottom:20px;color:#666">请先下载标准Excel模板，按照模板格式填写资产信息</p>
                    <button class="btn btn-info" onclick="downloadTemplate('资产导入')">📥 下载Excel模板</button>
                </div>
                <div class="detail-box">
                    <div class="detail-title">第二步：上传文件</div>
                    <input type="file" accept=".xlsx,.xls" style="margin-bottom:20px">
                    <button class="btn btn-success" onclick="validateImportFile()">🔍 预校验</button>
                </div>
                <div class="detail-box">
                    <div class="detail-title">第三步：确认导入</div>
                    <p style="color:#666">校验通过后，点击确认导入按钮完成导入</p>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="confirmImport()">确认导入</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

// 初始化
document.addEventListener('DOMContentLoaded', function() {
    console.log('资产管理系统原型已加载');
    console.log('提示：工号任意，密码任意即可登录');
});

// 全选/取消全选
function toggleAllCheckbox(checkbox) {
    const checkboxes = document.querySelectorAll('.row-checkbox');
    checkboxes.forEach(cb => {
        cb.checked = checkbox.checked;
    });
}

// 删除操作
function deleteItem(type, id) {
    showConfirm(`确认删除${type}？`, `编号 ${id || '-'} 的${type}将从当前原型视图移除。`, () => {
        showNotice(`${type}已删除`, '该操作仅模拟原型交互，不会影响真实数据。', 'success');
    }, { confirmClass: 'btn-danger', type: 'danger' });
}

// 启用/禁用
function toggleStatus(type, id, currentStatus) {
    const action = currentStatus === 'active' ? '禁用' : '启用';
    showConfirm(`确认${action}${type}？`, `操作对象编号：${id || '-'}。`, () => {
        showNotice(`${type}已${action}`, '状态变更已在当前原型中完成。', 'success');
    }, { confirmClass: action === '禁用' ? 'btn-danger' : 'btn-success' });
}

// 打印标签
function printLabel(id) {
    showToast('正在生成标签', 'info');
    setTimeout(() => {
        showNotice('标签已发送到打印机', `资产 ${id || ''} 的标签任务已创建。`, 'success');
    }, 500);
}

// 发送通知
function sendNotification(type, id) {
    const messages = {
        '催还': '站内催办已发送',
        '提醒': '站内提醒已发送',
        '审批': '站内审批提醒已发送'
    };
    showNotice(messages[type] || '站内通知已发送', `已在系统内向 ${id || '相关人员'} 推送站内消息，无邮件发送。`, 'success');
}

// 下载模板
function downloadTemplate(type) {
    showToast(`正在准备${type}模板`, 'info');
    setTimeout(() => {
        showNotice('模板已生成', `${type}模板已准备完成，可用于离线填写后导入。`, 'success');
    }, 500);
}

function validateImportFile() {
    showNotice('预校验通过', '共识别 100 条记录，其中 98 条可导入，2 条需要补全存放位置。', 'success');
}

function confirmImport() {
    document.querySelectorAll('.modal').forEach(m => m.remove());
    showNotice('导入完成', '本次模拟导入 98 条资产记录，异常记录已生成校验报告。', 'success');
}

// 时间范围快捷选择
function selectTimeRange(range) {
    const today = new Date();
    let startDate, endDate = today;

    switch(range) {
        case 'today':
            startDate = today;
            break;
        case 'week':
            startDate = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000);
            break;
        case 'month':
            startDate = new Date(today.getFullYear(), today.getMonth(), 1);
            break;
        case 'year':
            startDate = new Date(today.getFullYear(), 0, 1);
            break;
    }

    showToast(`时间范围：${startDate?.toLocaleDateString()} 至 ${endDate.toLocaleDateString()}`, 'info');
}

// 新增用户
function showAddUserModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">新增用户</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="addUserForm">
                    <div class="detail-grid">
                        <div class="form-group">
                            <label>* 工号</label>
                            <input type="text" required placeholder="请输入工号">
                        </div>
                        <div class="form-group">
                            <label>* 姓名</label>
                            <input type="text" required placeholder="请输入姓名">
                        </div>
                        <div class="form-group">
                            <label>* 角色</label>
                            <select required>
                                <option value="">请选择</option>
                                <option>系统管理员</option>
                                <option>仓库管理员</option>
                                <option>部门主管</option>
                                <option>普通员工</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>* 部门</label>
                            <input type="text" required placeholder="请输入部门">
                        </div>
                        <div class="form-group">
                            <label>直属主管</label>
                            <input type="text" placeholder="请输入主管姓名">
                        </div>
                        <div class="form-group">
                            <label>* 邮箱</label>
                            <input type="email" required placeholder="请输入邮箱">
                        </div>
                        <div class="form-group">
                            <label>* 手机</label>
                            <input type="tel" required placeholder="请输入手机号">
                        </div>
                    </div>
                    <div style="margin-top:15px;padding:12px;background:#e6f7ff;border-radius:6px;font-size:13px;color:#0050b3;">
                        💡 初始密码为工号后6位，用户首次登录需修改密码
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="handleAddUser()">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function handleAddUser() {
    const form = document.getElementById('addUserForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showPage('admin-users');
        showNotice('用户创建成功', '初始密码为工号后6位，首次登录需修改密码。', 'success');
    } else {
        form.reportValidity();
    }
}

// 新增类别
function showAddCategoryModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">新增类别</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="addCategoryForm">
                    <div class="form-group">
                        <label>* 类别名称</label>
                        <input type="text" required placeholder="请输入类别名称">
                    </div>
                    <div class="form-group">
                        <label>上级类别</label>
                        <select>
                            <option value="">无（顶级类别）</option>
                            <option>电气设备</option>
                            <option>工具</option>
                            <option>仪器仪表</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>类别说明</label>
                        <textarea placeholder="请输入类别说明（选填）"></textarea>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="handleAddCategory()">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function handleAddCategory() {
    const form = document.getElementById('addCategoryForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showPage('admin-categories');
        showNotice('类别创建成功', '新类别已加入当前分类树。', 'success');
    } else {
        form.reportValidity();
    }
}

// 全选/取消全选
function toggleAllCheckbox(checkbox) {
    const checkboxes = document.querySelectorAll('.row-checkbox');
    checkboxes.forEach(cb => cb.checked = checkbox.checked);
}

// 新增用户
function showAddUserModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">新增用户</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="addUserForm">
                    <div class="detail-grid">
                        <div class="form-group">
                            <label>* 工号</label>
                            <input type="text" required placeholder="请输入工号">
                        </div>
                        <div class="form-group">
                            <label>* 姓名</label>
                            <input type="text" required placeholder="请输入姓名">
                        </div>
                        <div class="form-group">
                            <label>* 角色</label>
                            <select required>
                                <option value="">请选择</option>
                                <option>系统管理员</option>
                                <option>仓库管理员</option>
                                <option>部门主管</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>* 部门</label>
                            <input type="text" required placeholder="请输入部门">
                        </div>
                        <div class="form-group">
                            <label>* 邮箱</label>
                            <input type="email" required placeholder="请输入邮箱">
                        </div>
                        <div class="form-group">
                            <label>* 手机</label>
                            <input type="tel" required placeholder="请输入手机号">
                        </div>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="handleAddUser()">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function handleAddUser() {
    const form = document.getElementById('addUserForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showPage('admin-users');
        showNotice('用户创建成功', '初始密码为工号后6位，首次登录需修改密码。', 'success');
    } else {
        form.reportValidity();
    }
}

// 新增类别
function showAddCategoryModal() {
    const modal = document.createElement('div');
    modal.className = 'modal active';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3 class="modal-title">新增类别</h3>
                <span class="modal-close" onclick="this.closest('.modal').remove()">&times;</span>
            </div>
            <div class="modal-body">
                <form id="addCategoryForm">
                    <div class="form-group">
                        <label>* 类别名称</label>
                        <input type="text" required placeholder="请输入类别名称">
                    </div>
                    <div class="form-group">
                        <label>上级类别</label>
                        <select>
                            <option value="">无（顶级类别）</option>
                            <option>电气设备</option>
                            <option>工具</option>
                        </select>
                    </div>
                </form>
            </div>
            <div class="modal-footer">
                <button class="btn btn-default" onclick="this.closest('.modal').remove()">取消</button>
                <button class="btn btn-success" onclick="handleAddCategory()">保存</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
}

function handleAddCategory() {
    const form = document.getElementById('addCategoryForm');
    if (form.checkValidity()) {
        document.querySelectorAll('.modal').forEach(m => m.remove());
        showPage('admin-categories');
        showNotice('类别创建成功', '新类别已加入当前分类树。', 'success');
    } else {
        form.reportValidity();
    }
}
