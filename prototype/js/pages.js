// 所有页面的HTML模板
const pages = {
    // 资产列表 - 层级视图
    'asset-list': () => `
        <div class="breadcrumb">首页 / 资产管理 / 资产列表</div>
        <div class="page-header">
            <h2 class="page-title">资产列表</h2>
            <div class="page-actions">
                <button class="btn btn-success" onclick="showAddAssetModal()">+ 新增资产</button>
                <button class="btn btn-info" onclick="showImportModal()">📥 批量导入</button>
                <button class="btn btn-default" onclick="exportData('资产列表Excel')">📤 导出</button>
            </div>
        </div>
        <div class="card">
            <div class="view-switch">
                <button class="view-btn ${currentAssetView === 'hierarchy' ? 'active' : ''}" onclick="switchView('hierarchy')">🗂️ 层级视图</button>
                <button class="view-btn ${currentAssetView === 'table' ? 'active' : ''}" onclick="switchView('table')">📊 列表视图</button>
            </div>
            <div id="hierarchyView" style="display:${currentAssetView === 'hierarchy' ? 'block' : 'none'};">
                ${renderHierarchyLevel()}
            </div>
            <div id="tableView" style="display:${currentAssetView === 'table' ? 'block' : 'none'};">
                <div class="filter-box">
                    <form id="assetFilterForm">
                        <div class="filter-row">
                            <div class="filter-item">
                                <label>资产编号</label>
                                <input type="text" name="assetNo" placeholder="请输入" value="${currentFilters.assetNo || ''}">
                            </div>
                            <div class="filter-item">
                                <label>资产名称</label>
                                <input type="text" name="assetName" placeholder="请输入" value="${currentFilters.assetName || ''}">
                            </div>
                            <div class="filter-item">
                                <label>类别</label>
                                <select name="category">
                                    <option value="">全部</option>
                                    ${['电气设备', '工具', '仪器仪表'].map(c => `<option ${currentFilters.category === c ? 'selected' : ''}>${c}</option>`).join('')}
                                </select>
                            </div>
                            <div class="filter-item">
                                <label>状态</label>
                                <select name="status">
                                    <option value="">全部</option>
                                    <option value="available" ${currentFilters.status === 'available' ? 'selected' : ''}>在库</option>
                                    <option value="borrowed" ${currentFilters.status === 'borrowed' ? 'selected' : ''}>借出中</option>
                                </select>
                            </div>
                            <div class="filter-item">
                                <label>归属部门</label>
                                <select name="dept">
                                    <option value="">全部</option>
                                    ${flattenDepartments().map(d => `<option value="${d.id}" ${currentFilters.dept === d.id ? 'selected' : ''}>${'　'.repeat(d.depth)}${d.name}</option>`).join('')}
                                </select>
                            </div>
                            <div class="filter-actions">
                                <button type="button" class="btn btn-info" onclick="applyFilter('assetFilterForm')">查询</button>
                                <button type="button" class="btn btn-default" onclick="resetFilter('assetFilterForm')">重置</button>
                            </div>
                        </div>
                    </form>
                </div>
                <div class="table-container">
                    <div class="table-header">
                        <div style="display:flex;align-items:center;gap:15px;">
                            <span>共 ${getVisibleAssets().length} 条${currentDeptScope !== 'all' ? `（查看范围：${deptPath(currentDeptScope)}）` : ''}</span>
                            <select onchange="handlePageSizeChange(this.value)" style="padding:6px;border:1px solid #d9d9d9;border-radius:4px;">
                                <option value="20" selected>每页 20 条</option>
                                <option value="50">每页 50 条</option>
                                <option value="100">每页 100 条</option>
                            </select>
                        </div>
                        <div style="display:flex;gap:10px;">
                            <button class="btn btn-default" onclick="batchOperation('导出')">批量导出</button>
                            <button class="btn btn-default" onclick="batchOperation('打印标签')">批量打印标签</button>
                        </div>
                    </div>
                    <table>
                        <thead>
                            <tr>
                                <th><input type="checkbox" onchange="toggleAllCheckbox(this)"></th>
                                <th><a href="#" onclick="sortTable('编号','asc');return false;" style="color:#666;text-decoration:none;">编号 ↕</a></th>
                                <th><a href="#" onclick="sortTable('名称','asc');return false;" style="color:#666;text-decoration:none;">名称 ↕</a></th>
                                <th>类别</th>
                                <th>状态</th>
                                <th>归属部门</th>
                                <th>保管人</th>
                                <th>位置</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${getVisibleAssets().length === 0 ? `<tr><td colspan="9" style="text-align:center;padding:30px;color:#999;">该查看范围 / 筛选条件下暂无资产</td></tr>` : getVisibleAssets().map(asset => `
                                <tr>
                                    <td><input type="checkbox" class="row-checkbox"></td>
                                    <td>${asset.assetNo}</td>
                                    <td>${asset.name}</td>
                                    <td>${asset.category}</td>
                                    <td><span class="badge-status ${asset.status === 'available' ? 'badge-success' : 'badge-warning'}">${asset.status === 'available' ? '在库' : '借出中'}</span></td>
                                    <td>${asset.deptName || '-'}</td>
                                    <td>${asset.custodian || '-'}</td>
                                    <td>${asset.location}</td>
                                    <td>
                                        <button class="btn btn-info btn" onclick="showAssetDetail(${asset.id})">详情</button>
                                        ${asset.status === 'available' ? '<button class="btn btn-success btn" onclick="showBorrowModal(' + asset.id + ')">借用</button>' : ''}
                                        ${asset.status === 'available' ? '<button class="btn btn-warning btn" onclick="showTransferModal(' + asset.id + ')">转让</button>' : ''}
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                    <div class="table-footer">
                        <div class="pagination">
                            <button onclick="goToPage(0)">&lt;&lt;</button>
                            <button onclick="goToPage('prev')">&lt;</button>
                            <button class="active" onclick="goToPage(1)">1</button>
                            <button onclick="goToPage(2)">2</button>
                            <button onclick="goToPage(3)">3</button>
                            <button onclick="goToPage(4)">4</button>
                            <button onclick="goToPage(5)">5</button>
                            <button onclick="goToPage('next')">&gt;</button>
                            <button onclick="goToPage(999)">&gt;&gt;</button>
                            <span style="margin:0 10px;">跳转至</span>
                            <input type="number" min="1" style="width:60px;padding:6px;border:1px solid #d9d9d9;border-radius:4px;" onchange="goToPage(this.value)">
                            <span style="margin-left:5px;">页</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `,

    // 原 asset-level2/3/4 多层卡片导航已由统一编码树 renderHierarchyLevel() 取代

    // 新增资产
    'asset-add': () => `
        <div class="breadcrumb">首页 / 资产管理 / 新增资产</div>
        <div class="page-header">
            <h2 class="page-title">新增资产</h2>
        </div>
        <div class="card">
            <form id="assetCreateForm">
                <div class="card-title">基本信息</div>
                <div class="detail-grid">
                    <div class="form-group">
                        <label>* 资产类别</label>
                        <select required>
                            <option value="">请选择</option>
                            <option>电气设备</option>
                            <option>工具</option>
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
                        <input type="number" placeholder="请输入数量" value="1" min="1" required>
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
                        </select>
                    </div>
                </div>
                <div class="form-group" style="margin-top:20px">
                    <label>备注</label>
                    <textarea placeholder="请输入备注(最多500字)"></textarea>
                </div>
                <div style="margin-top:20px;display:flex;gap:10px;justify-content:flex-end">
                    <button type="button" class="btn btn-default" onclick="showPage('asset-list')">取消</button>
                    <button type="button" class="btn btn-success" onclick="savePrototypePageForm('assetCreateForm', 'asset-list')">保存</button>
                </div>
            </form>
        </div>
    `,

    // 批量导入
    'asset-import': () => `
        <div class="breadcrumb">首页 / 资产管理 / 批量导入</div>
        <div class="page-header">
            <h2 class="page-title">批量导入资产</h2>
        </div>
        <div class="card">
            <div class="card-title">第一步：下载模板</div>
            <p style="margin-bottom:20px;color:#666">请先下载标准Excel模板，按照模板格式填写资产信息</p>
            <button class="btn btn-info" onclick="downloadTemplate('资产导入')">下载Excel模板</button>
        </div>
        <div class="card">
            <div class="card-title">第二步：上传文件</div>
            <input type="file" accept=".xlsx,.xls" style="margin-bottom:20px">
            <button class="btn btn-success" onclick="validateImportFile()">预校验</button>
        </div>
        <div class="card">
            <div class="card-title">第三步：确认导入</div>
            <p style="color:#666">校验通过后，点击确认导入按钮完成导入</p>
            <button class="btn btn-success" style="margin-top:16px" onclick="confirmImport()">确认导入</button>
        </div>
    `,

    // 待我审批
    'approval-pending': () => `
        <div class="breadcrumb">首页 / 审批管理 / 待我审批</div>
        <div class="page-header">
            <h2 class="page-title">待我审批 <span class="badge" style="background:#e6a23c;color:white;padding:2px 8px;border-radius:10px;font-size:12px">${mockData.approvals.length}</span></h2>
        </div>
        ${mockData.approvals.length === 0 ? `<div class="card"><p style="text-align:center;padding:40px;color:#999">暂无待审批工单</p></div>` : mockData.approvals.map(flow => {
            const node = flow.nodes[flow.currentNodeIndex];
            const wf = mockData.workflows.find(w => w.id === flow.workflowId);
            const total = flow.nodes.filter(n => n.type !== 'start' && n.type !== 'end').length;
            const step = flow.nodes.slice(0, flow.currentNodeIndex).filter(n => n.type !== 'start' && n.type !== 'end').length + 1;
            const overdue = flow.deadline && flow.deadline < '2024-06-11';
            return `
            <div class="flow-card ${overdue ? 'type-rejected' : ''}">
                <div class="flow-card-header">
                    <div>
                        <span class="badge-status ${overdue ? 'badge-danger' : 'badge-warning'}">${overdue ? '⚠️ 已超时' : '🟡 待审批'}</span>
                        <span style="margin-left:10px;font-weight:600">工单号: ${flow.flowNo}</span>
                        <span style="margin-left:10px;color:#722ed1;font-size:12px">${wf ? wf.name : ''}</span>
                    </div>
                    <div style="font-size:12px;color:#999">节点进度 ${step}/${total}</div>
                </div>
                <div class="flow-card-body">
                    <div>类型: ${flow.type === 'transfer' ? '转让申请' : flow.type === 'return' ? '归还申请' : '借用申请'} | 申请人: ${flow.applicant} (${flow.applicantDept}) | 申请时间: ${flow.applyTime}</div>
                    <div>资产: ${flow.assetNo} ${flow.assetName}</div>
                    <div>📍 当前节点: <strong>${node.name}</strong>（${NODE_TYPE_LABEL[node.type]}）　审批人: ${node.approver || '—'}${(node.type === 'countersign' || node.type === 'orsign') && node.signers ? '（' + node.signers.join('、') + '）' : ''}</div>
                    <div>审批截止: ${flow.deadline}</div>
                </div>
                <div class="flow-card-actions">
                    <button class="btn btn-info btn" onclick="showApprovalDetail(${flow.id})">查看流程</button>
                    <button class="btn btn-success btn" onclick="handleApprove(${flow.id})">通过</button>
                    <button class="btn btn-danger btn" onclick="handleReject(${flow.id})">驳回</button>
                </div>
            </div>
        `;}).join('')}
    `,

    // 我的申请
    'approval-mine': () => `
        <div class="breadcrumb">首页 / 审批管理 / 我的申请</div>
        <div class="page-header">
            <h2 class="page-title">我的申请</h2>
            <div class="page-actions">
                <button class="btn btn-success" onclick="startBorrowRequest()">+ 发起借用申请</button>
                <button class="btn btn-warning" onclick="startTransferRequest()">+ 发起转让申请</button>
            </div>
        </div>
        <div class="card">
            <p style="text-align:center;padding:40px;color:#999">暂无申请记录</p>
        </div>
    `,

    // 待确认归还
    'approval-return': () => `
        <div class="breadcrumb">首页 / 审批管理 / 待确认归还</div>
        <div class="page-header">
            <h2 class="page-title">待确认归还</h2>
        </div>
        <div class="card">
            <p style="text-align:center;padding:40px;color:#999">暂无待确认归还记录</p>
        </div>
    `,

    // 报表 - 资产汇总
    'report-summary': () => `
        <div class="breadcrumb">首页 / 统计报表 / 资产汇总</div>
        <div class="page-header">
            <h2 class="page-title">资产汇总报表</h2>
            <div class="page-actions">
                <button class="btn btn-default" onclick="selectTimeRange('month')">📅 本月</button>
                <button class="btn btn-default" onclick="selectTimeRange('year')">📅 本年</button>
                <button class="btn btn-success" onclick="exportData('资产汇总Excel')">📤 导出Excel</button>
            </div>
        </div>
        <div class="card">
            <div class="card-title">📊 总体概况</div>
            <div class="detail-grid" style="grid-template-columns: repeat(4, 1fr);">
                <div class="detail-item" style="text-align:center;padding:20px;background:#e6f7ff;border-radius:8px;">
                    <div class="detail-label" style="font-size:14px;margin-bottom:10px;">资产总数</div>
                    <div class="detail-value" style="font-size:32px;color:#1890ff;font-weight:700;">${mockData.reports.summary.total}</div>
                </div>
                <div class="detail-item" style="text-align:center;padding:20px;background:#f6ffed;border-radius:8px;">
                    <div class="detail-label" style="font-size:14px;margin-bottom:10px;">在库数量</div>
                    <div class="detail-value" style="font-size:32px;color:#52c41a;font-weight:700;">${mockData.reports.summary.available}</div>
                </div>
                <div class="detail-item" style="text-align:center;padding:20px;background:#fff7e6;border-radius:8px;">
                    <div class="detail-label" style="font-size:14px;margin-bottom:10px;">借出数量</div>
                    <div class="detail-value" style="font-size:32px;color:#faad14;font-weight:700;">${mockData.reports.summary.borrowed}</div>
                </div>
                <div class="detail-item" style="text-align:center;padding:20px;background:#fff1f0;border-radius:8px;">
                    <div class="detail-label" style="font-size:14px;margin-bottom:10px;">总价值</div>
                    <div class="detail-value" style="font-size:28px;color:#ff4d4f;font-weight:700;">¥${(mockData.reports.summary.totalValue/10000).toFixed(1)}万</div>
                </div>
            </div>
        </div>
        <div class="card">
            <div class="card-title">📈 按类别统计</div>
            <table>
                <thead>
                    <tr>
                        <th>类别</th>
                        <th>总数</th>
                        <th>在库</th>
                        <th>借出</th>
                        <th>总价值</th>
                        <th>占比</th>
                    </tr>
                </thead>
                <tbody>
                    ${mockData.reports.summary.byCategory.map(cat => `
                        <tr>
                            <td><strong>${cat.category}</strong></td>
                            <td>${cat.total}</td>
                            <td><span class="badge-status badge-success">${cat.available}</span></td>
                            <td><span class="badge-status badge-warning">${cat.borrowed}</span></td>
                            <td>¥${cat.value.toLocaleString()}</td>
                            <td>
                                <div style="display:flex;align-items:center;gap:10px;">
                                    <div style="flex:1;height:20px;background:#f0f0f0;border-radius:10px;overflow:hidden;">
                                        <div style="width:${cat.percent}%;height:100%;background:linear-gradient(90deg, #1890ff, #40a9ff);"></div>
                                    </div>
                                    <span style="min-width:40px;font-weight:600;color:#1890ff;">${cat.percent}%</span>
                                </div>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
        <div class="card">
            <div class="card-title">🏢 按部门统计</div>
            <table>
                <thead>
                    <tr>
                        <th>归属部门</th>
                        <th>总数</th>
                        <th>在库</th>
                        <th>借出</th>
                        <th>总价值</th>
                        <th>占比</th>
                    </tr>
                </thead>
                <tbody>
                    ${mockData.reports.summary.byDept.map(d => `
                        <tr>
                            <td><strong>${d.dept}</strong></td>
                            <td>${d.total}</td>
                            <td><span class="badge-status badge-success">${d.available}</span></td>
                            <td><span class="badge-status badge-warning">${d.borrowed}</span></td>
                            <td>¥${d.value.toLocaleString()}</td>
                            <td>
                                <div style="display:flex;align-items:center;gap:10px;">
                                    <div style="flex:1;height:20px;background:#f0f0f0;border-radius:10px;overflow:hidden;">
                                        <div style="width:${d.percent}%;height:100%;background:linear-gradient(90deg, #722ed1, #9254de);"></div>
                                    </div>
                                    <span style="min-width:40px;font-weight:600;color:#722ed1;">${d.percent}%</span>
                                </div>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:20px;">
            <div class="card">
                <div class="card-title">📊 状态分布</div>
                <div style="padding:40px;text-align:center;">
                    <div style="font-size:18px;margin-bottom:20px;color:#666;">
                        <span style="color:#52c41a;font-weight:700;">77%</span> 在库 /
                        <span style="color:#faad14;font-weight:700;">23%</span> 借出中
                    </div>
                    <svg width="200" height="200" viewBox="0 0 200 200" style="margin:0 auto;">
                        <circle cx="100" cy="100" r="80" fill="none" stroke="#52c41a" stroke-width="40" stroke-dasharray="387 500" transform="rotate(-90 100 100)"/>
                        <circle cx="100" cy="100" r="80" fill="none" stroke="#faad14" stroke-width="40" stroke-dasharray="115 500" stroke-dashoffset="-387" transform="rotate(-90 100 100)"/>
                    </svg>
                </div>
            </div>
            <div class="card">
                <div class="card-title">📈 月度趋势</div>
                <div style="padding:40px;text-align:center;color:#999;">
                    趋势图（简化展示）
                </div>
            </div>
        </div>
    `,

    // 报表 - 借用明细
    'report-borrowed': () => `
        <div class="breadcrumb">首页 / 统计报表 / 借用明细</div>
        <div class="page-header">
            <h2 class="page-title">借用明细报表</h2>
            <button class="btn btn-success" onclick="exportData('借用明细Excel')">📤 导出Excel</button>
        </div>
        <div class="card">
            <div class="filter-box">
                <div class="filter-row">
                    <div class="filter-item">
                        <label>时间范围</label>
                        <input type="date" value="2024-06-01">
                    </div>
                    <div class="filter-item">
                        <label>至</label>
                        <input type="date" value="2024-06-30">
                    </div>
                    <div class="filter-item">
                        <label>类别</label>
                        <select><option>全部</option><option>电气设备</option><option>工具</option></select>
                    </div>
                    <div class="filter-item">
                        <label>借用人</label>
                        <select><option>全部</option></select>
                    </div>
                    <div class="filter-item">
                        <label>状态</label>
                        <select><option>全部</option><option>使用中</option><option>已归还</option></select>
                    </div>
                    <div class="filter-actions">
                        <button class="btn btn-info" onclick="simulateQuery('借用明细')">查询</button>
                        <button class="btn btn-default" onclick="resetQuery('借用明细')">重置</button>
                    </div>
                </div>
            </div>
        </div>
        <div class="card">
            <div style="margin-bottom:20px;padding:15px;background:#e6f7ff;border-radius:8px;border-left:4px solid #1890ff;">
                <strong>📊 统计摘要：</strong>共 ${mockData.reports.borrowed.length} 条记录，使用中 1 条，已归还 1 条
            </div>
            <table>
                <thead>
                    <tr>
                        <th>工单号</th>
                        <th>资产编号</th>
                        <th>借用人</th>
                        <th>申请时间</th>
                        <th>审批人</th>
                        <th>审批时间</th>
                        <th>归还时间</th>
                        <th>状态</th>
                    </tr>
                </thead>
                <tbody>
                    ${mockData.reports.borrowed.map(record => `
                        <tr>
                            <td><a href="#" onclick="showFlowDetail('${record.flowNo}');return false;" style="color:#1890ff;text-decoration:none;">${record.flowNo}</a></td>
                            <td>${record.assetNo}</td>
                            <td>${record.borrower}</td>
                            <td>${record.applyTime}</td>
                            <td>${record.approver}</td>
                            <td>${record.approveTime}</td>
                            <td>${record.returnTime}</td>
                            <td><span class="badge-status ${record.status === '使用中' ? 'badge-warning' : 'badge-success'}">${record.status}</span></td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `,

    // 报表 - 逾期资产
    'report-overdue': () => `
        <div class="breadcrumb">首页 / 统计报表 / 逾期资产</div>
        <div class="page-header">
            <h2 class="page-title">逾期资产报表</h2>
            <div class="page-actions">
                <button class="btn btn-warning" onclick="sendNotification('催还', '批量逾期资产')">🔔 批量站内催办</button>
                <button class="btn btn-success" onclick="exportData('逾期资产Excel')">📤 导出Excel</button>
            </div>
        </div>
        <div class="card">
            <div style="padding:20px;background:#fff7e6;border-radius:8px;margin-bottom:20px;border-left:4px solid #faad14;">
                <div style="display:flex;align-items:center;gap:15px;">
                    <span style="font-size:48px;">⚠️</span>
                    <div>
                        <div style="font-size:18px;font-weight:600;color:#d46b08;margin-bottom:5px;">当前逾期资产: ${mockData.reports.overdue.length} 件</div>
                        <div style="color:#8c8c8c;">请及时联系保管人归还资产</div>
                    </div>
                </div>
            </div>
            <table>
                <thead>
                    <tr>
                        <th>资产编号</th>
                        <th>名称</th>
                        <th>保管人</th>
                        <th>应还日期</th>
                        <th>逾期天数</th>
                        <th>操作</th>
                    </tr>
                </thead>
                <tbody>
                    ${mockData.reports.overdue.map(asset => `
                        <tr style="background:${asset.overdueDays > 10 ? '#fff1f0' : 'white'};">
                            <td>${asset.assetNo}</td>
                            <td>${asset.name}</td>
                            <td><strong>${asset.custodian}</strong></td>
                            <td>${asset.dueDate}</td>
                            <td><span class="badge-status badge-danger" style="font-size:16px;padding:6px 12px;">⚠️ ${asset.overdueDays} 天</span></td>
                            <td>
                                <button class="btn btn-warning btn" onclick="sendNotification('催还', '${asset.custodian}')">🔔 站内催办</button>
                                <button class="btn btn-info btn" onclick="showOverdueDetail('${asset.assetNo}')">详情</button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `,

    // 用户管理
    'admin-users': () => `
        <div class="breadcrumb">首页 / 系统管理 / 用户管理</div>
        <div class="page-header">
            <h2 class="page-title">用户管理</h2>
            <button class="btn btn-success" onclick="showAddUserModal()">+ 新增用户</button>
        </div>
        <div class="card">
            <div class="filter-box">
                <div class="filter-row">
                    <div class="filter-item">
                        <label>工号</label>
                        <input type="text" placeholder="请输入工号">
                    </div>
                    <div class="filter-item">
                        <label>姓名</label>
                        <input type="text" placeholder="请输入姓名">
                    </div>
                    <div class="filter-item">
                        <label>角色</label>
                        <select><option>全部</option><option>系统管理员</option><option>仓库管理员</option><option>部门主管</option></select>
                    </div>
                    <div class="filter-actions">
                        <button class="btn btn-info" onclick="simulateQuery('用户')">查询</button>
                        <button class="btn btn-default" onclick="resetQuery('用户')">重置</button>
                    </div>
                </div>
            </div>
        </div>
        <div class="card">
            <table>
                <thead>
                    <tr>
                        <th>工号</th>
                        <th>姓名</th>
                        <th>角色</th>
                        <th>部门</th>
                        <th>直属主管</th>
                        <th>邮箱</th>
                        <th>手机</th>
                        <th>状态</th>
                        <th>操作</th>
                    </tr>
                </thead>
                <tbody>
                    ${mockData.users.map(user => `
                        <tr>
                            <td>${user.employeeNo}</td>
                            <td><strong>${user.name}</strong></td>
                            <td><span class="badge-status badge-info">${user.roleName}</span></td>
                            <td>${user.dept}</td>
                            <td>${user.supervisor}</td>
                            <td>${user.email}</td>
                            <td>${user.phone}</td>
                            <td><span class="badge-status badge-success">● 启用</span></td>
                            <td>
                                <button class="btn btn-info btn" onclick="showAdminEditor('用户', '编辑', '${user.name}')">编辑</button>
                                <button class="btn btn-warning btn" onclick="resetPassword('${user.employeeNo}')">重置密码</button>
                                <button class="btn btn-danger btn" onclick="toggleStatus('用户', '${user.employeeNo}', 'active')">禁用</button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `,

    // 组织架构（部门支持任意层级）
    'admin-departments': () => `
        <div class="breadcrumb">首页 / 系统管理 / 组织架构</div>
        <div class="page-header">
            <h2 class="page-title">组织架构</h2>
            <button class="btn btn-success" onclick="showAdminEditor('部门', '新增', '')">+ 新增部门</button>
        </div>
        <div class="card">
            <div style="margin-bottom:20px;padding:15px;background:#e6f7ff;border-radius:8px;border-left:4px solid #1890ff;">
                <strong>💡 提示：</strong>组织架构支持任意层级（公司 / 部门 / 科室 / 组……）。资产归属到具体的组织单元；按上级部门查看时会自动包含其下所有子部门的资产。
            </div>
            ${renderDepartmentTree(mockData.departments)}
        </div>
    `,

    // 审批流程设计器
    'admin-workflows': () => `
        <div class="breadcrumb">首页 / 系统管理 / 审批流程</div>
        <div class="page-header">
            <h2 class="page-title">审批流程设计</h2>
            <button class="btn btn-success" onclick="saveWorkflow()">💾 保存流程</button>
        </div>
        <div class="card">
            <div style="margin-bottom:16px;padding:12px 15px;background:#e6f7ff;border-radius:8px;border-left:4px solid #1890ff;">
                <strong>💡 提示：</strong>可自由增删审批节点、配置审批人；支持会签（都通过）、或签（一人通过）、条件分支；运行时可加签、转签。发起申请时按对应流程自动流转。
            </div>
            <div class="wf-tabs">
                ${mockData.workflows.map(w => `<button class="wf-tab ${w.id === currentWorkflowId ? 'active' : ''}" onclick="switchWorkflow('${w.id}')">${w.name}</button>`).join('')}
            </div>
            <div class="wf-designer">
                ${renderWorkflowDesigner()}
            </div>
        </div>
    `,

    // 类别管理
    'admin-categories': () => `
        <div class="breadcrumb">首页 / 系统管理 / 类别管理</div>
        <div class="page-header">
            <h2 class="page-title">类别管理</h2>
            <button class="btn btn-success" onclick="showAddCategoryModal()">+ 新增类别</button>
        </div>
        <div class="card">
            <div style="margin-bottom:20px;padding:15px;background:#e6f7ff;border-radius:8px;border-left:4px solid #1890ff;">
                <strong>💡 提示：</strong>类别支持多级结构，删除父类别会同时删除其下所有子类别
            </div>
            ${mockData.categories.map(cat => `
                <div style="margin-bottom:20px;">
                    <div style="padding:15px;background:#fafafa;border-radius:8px;display:flex;justify-content:space-between;align-items:center;border:1px solid #e8e8e8;">
                        <span style="font-size:16px;font-weight:600;">📦 ${cat.name} <span style="color:#999;font-size:14px;font-weight:normal;">(${cat.assetCount} 件资产)</span></span>
                        <div>
                            <button class="btn btn-info btn" onclick="showAdminEditor('类别', '编辑', '${cat.name}')">编辑</button>
                            <button class="btn btn-success btn" onclick="showAdminEditor('子类别', '新增', '${cat.name}-子类')">+ 子类别</button>
                            <button class="btn btn-danger btn" onclick="deleteItem('类别', '${cat.name}')">删除</button>
                        </div>
                    </div>
                    ${cat.children ? cat.children.map(child => `
                        <div style="padding:12px;padding-left:50px;border-left:2px solid #e8e8e8;margin-left:20px;margin-top:8px;display:flex;justify-content:space-between;align-items:center;background:white;border-radius:4px;">
                            <span>├─ ${child.name} <span style="color:#999;font-size:12px;">(${child.assetCount} 件)</span></span>
                            <div>
                                <button class="btn btn-info btn" onclick="showAdminEditor('类别', '编辑', '${child.name}')">编辑</button>
                                <button class="btn btn-danger btn" onclick="deleteItem('类别', '${child.name}')">删除</button>
                            </div>
                        </div>
                    `).join('') : ''}
                </div>
            `).join('')}
        </div>
    `,

    // 位置管理
    'admin-locations': () => `
        <div class="breadcrumb">首页 / 系统管理 / 位置管理</div>
        <div class="page-header">
            <h2 class="page-title">位置管理</h2>
            <button class="btn btn-success" onclick="showAdminEditor('位置', '新增', '新仓库')">+ 新增位置</button>
        </div>
        <div class="card">
            <div style="margin-bottom:20px;padding:15px;background:#fff7e6;border-radius:8px;border-left:4px solid #faad14;">
                <strong>💡 提示：</strong>位置支持三级结构：仓库 > 区域 > 货架
            </div>
            <div style="margin-bottom:20px;">
                <div style="padding:15px;background:#fafafa;border-radius:8px;display:flex;justify-content:space-between;align-items:center;border:1px solid #e8e8e8;">
                    <span style="font-size:16px;font-weight:600;">🏢 一号仓库 <span style="color:#999;font-size:14px;font-weight:normal;">(120 件资产)</span></span>
                    <div>
                        <button class="btn btn-info btn" onclick="showAdminEditor('位置', '编辑', '一号仓库')">编辑</button>
                        <button class="btn btn-success btn" onclick="showAdminEditor('区域', '新增', '新区')">+ 区域</button>
                    </div>
                </div>
                <div style="padding:12px;padding-left:50px;border-left:2px solid #e8e8e8;margin-left:20px;margin-top:8px;background:white;border-radius:4px;">
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;">
                        <span>├─ A区 <span style="color:#999;font-size:12px;">(50 件)</span></span>
                        <div>
                            <button class="btn btn-info btn" onclick="showAdminEditor('区域', '编辑', 'A区')">编辑</button>
                            <button class="btn btn-success btn" onclick="showAdminEditor('货架', '新增', '新货架')">+ 货架</button>
                        </div>
                    </div>
                    <div style="padding-left:30px;margin-top:8px;">
                        <div style="padding:8px;background:#fafafa;border-radius:4px;margin-bottom:5px;display:flex;justify-content:space-between;align-items:center;">
                            <span>└─ A01货架 <span style="color:#999;font-size:12px;">(25 件)</span></span>
                            <div>
                                <button class="btn btn-info btn" onclick="showAdminEditor('货架', '编辑', 'A01货架')">编辑</button>
                                <button class="btn btn-danger btn" onclick="deleteItem('货架', 'A01')">删除</button>
                            </div>
                        </div>
                        <div style="padding:8px;background:#fafafa;border-radius:4px;display:flex;justify-content:space-between;align-items:center;">
                            <span>└─ A02货架 <span style="color:#999;font-size:12px;">(25 件)</span></span>
                            <div>
                                <button class="btn btn-info btn" onclick="showAdminEditor('货架', '编辑', 'A02货架')">编辑</button>
                                <button class="btn btn-danger btn" onclick="deleteItem('货架', 'A02')">删除</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `,

    // 系统参数
    'admin-settings': () => `
        <div class="breadcrumb">首页 / 系统管理 / 系统参数</div>
        <div class="page-header">
            <h2 class="page-title">系统参数配置</h2>
        </div>
        <div class="card">
            <div class="card-title">系统设置</div>
            <form>
                <div class="detail-grid">
                    <div class="form-group">
                        <label>审计日志保留月数</label>
                        <input type="number" value="12">
                    </div>
                    <div class="form-group">
                        <label>附件大小限制(MB)</label>
                        <input type="number" value="20">
                    </div>
                    <div class="form-group">
                        <label>每页显示记录数</label>
                        <select>
                            <option value="20" selected>20</option>
                            <option value="50">50</option>
                            <option value="100">100</option>
                        </select>
                    </div>
                </div>
            </form>
        </div>
        <div class="card">
            <div style="padding:12px 15px;background:#f6ffed;border-radius:8px;border-left:4px solid #52c41a;">
                <strong>说明：</strong>审批流程已改为可视化配置，请在「系统管理 → 审批流程」中设计。本系统不含定时任务与邮件提醒——审批不会超时自动通过（超时仅作展示标记），逾期催办通过站内消息发送。
            </div>
        </div>
        <div style="text-align:right;margin-top:20px;">
            <button class="btn btn-default" onclick="handleSettingsReset()">重置</button>
            <button class="btn btn-success" onclick="handleSettingsSave()">保存配置</button>
        </div>
    `,

    // 审计日志
    'admin-logs': () => `
        <div class="breadcrumb">首页 / 系统管理 / 审计日志</div>
        <div class="page-header">
            <h2 class="page-title">审计日志</h2>
            <button class="btn btn-success" onclick="exportData('审计日志Excel')">📤 导出</button>
        </div>
        <div class="card">
            <div class="filter-box">
                <div class="filter-row">
                    <div class="filter-item">
                        <label>时间范围</label>
                        <input type="date" value="2024-06-01">
                    </div>
                    <div class="filter-item">
                        <label>至</label>
                        <input type="date" value="2024-06-10">
                    </div>
                    <div class="filter-item">
                        <label>操作人</label>
                        <input type="text" placeholder="请输入姓名">
                    </div>
                    <div class="filter-item">
                        <label>操作类型</label>
                        <select>
                            <option>全部</option>
                            <option>新增</option>
                            <option>编辑</option>
                            <option>删除</option>
                            <option>审批</option>
                        </select>
                    </div>
                    <div class="filter-item">
                        <label>模块</label>
                        <select>
                            <option>全部</option>
                            <option>资产管理</option>
                            <option>审批管理</option>
                            <option>系统管理</option>
                        </select>
                    </div>
                    <div class="filter-actions">
                        <button class="btn btn-info" onclick="simulateQuery('审计日志')">查询</button>
                        <button class="btn btn-default" onclick="resetQuery('审计日志')">重置</button>
                    </div>
                </div>
            </div>
        </div>
        <div class="card">
            <table>
                <thead>
                    <tr>
                        <th>时间</th>
                        <th>操作人</th>
                        <th>IP地址</th>
                        <th>模块</th>
                        <th>操作类型</th>
                        <th>操作内容</th>
                        <th>结果</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td>2024-06-10 14:30:25</td>
                        <td>张三</td>
                        <td>192.168.1.100</td>
                        <td>资产管理</td>
                        <td><span class="badge-status badge-success">新增</span></td>
                        <td>新增资产 PLC-MIT-Q-005</td>
                        <td><span class="badge-status badge-success">成功</span></td>
                    </tr>
                    <tr>
                        <td>2024-06-10 14:28:15</td>
                        <td>李四</td>
                        <td>192.168.1.102</td>
                        <td>审批管理</td>
                        <td><span class="badge-status badge-info">审批</span></td>
                        <td>审批通过工单 APV-20240610-001</td>
                        <td><span class="badge-status badge-success">成功</span></td>
                    </tr>
                    <tr>
                        <td>2024-06-10 14:25:10</td>
                        <td>王五</td>
                        <td>192.168.1.105</td>
                        <td>资产管理</td>
                        <td><span class="badge-status badge-warning">编辑</span></td>
                        <td>修改资产 PLC-MIT-Q-003 信息</td>
                        <td><span class="badge-status badge-success">成功</span></td>
                    </tr>
                    <tr>
                        <td>2024-06-10 14:20:05</td>
                        <td>系统管理员</td>
                        <td>192.168.1.1</td>
                        <td>系统管理</td>
                        <td><span class="badge-status badge-danger">删除</span></td>
                        <td>删除用户 test001</td>
                        <td><span class="badge-status badge-success">成功</span></td>
                    </tr>
                    <tr>
                        <td>2024-06-10 14:15:00</td>
                        <td>张三</td>
                        <td>192.168.1.100</td>
                        <td>资产管理</td>
                        <td><span class="badge-status badge-info">导入</span></td>
                        <td>批量导入资产 50 条</td>
                        <td><span class="badge-status badge-success">成功</span></td>
                    </tr>
                </tbody>
            </table>
            <div class="table-footer">
                <div class="pagination">
                    <button>&lt;</button>
                    <button class="active">1</button>
                    <button>2</button>
                    <button>3</button>
                    <button>&gt;</button>
                </div>
            </div>
        </div>
    `
};

window.pages = pages;
