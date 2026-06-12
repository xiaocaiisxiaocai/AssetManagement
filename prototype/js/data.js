// 模拟数据
const mockData = {
    // 当前用户
    currentUser: {
        id: 1,
        name: '张三',
        role: 'warehouse',
        roleName: '仓库管理员',
        dept: '行政部',
        deptId: 'ADMIN'
    },

    // 资产分类编码树（任意多层：节点含 名称/编码段/完整编码/子节点/末端资产）
    assetTree: [
        { id: 'PLC', name: 'PLC控制器', codeSeg: 'PLC', code: 'PLC', children: [
            { id: 'PLC-MIT', name: '三菱 MITSUBISHI', codeSeg: 'MIT', code: 'PLC-MIT', children: [
                { id: 'PLC-MIT-Q', name: 'Q系列', codeSeg: 'Q', code: 'PLC-MIT-Q', children: [], assets: [
                    { assetNo: 'PLC-MIT-Q-001', name: '三菱Q系列PLC', model: 'Q06UDVCPU', status: 'available', custodian: null },
                    { assetNo: 'PLC-MIT-Q-002', name: '三菱Q系列PLC', model: 'Q06UDVCPU', status: 'borrowed', custodian: '张三' }
                ] },
                { id: 'PLC-MIT-FX', name: 'FX系列', codeSeg: 'FX', code: 'PLC-MIT-FX', children: [], assets: [
                    { assetNo: 'PLC-MIT-FX-001', name: '三菱FX系列PLC', model: 'FX3U-64MR', status: 'available', custodian: null }
                ] }
            ], assets: [] },
            { id: 'PLC-SIE', name: '西门子 SIEMENS', codeSeg: 'SIE', code: 'PLC-SIE', children: [
                { id: 'PLC-SIE-S7', name: 'S7系列', codeSeg: 'S7', code: 'PLC-SIE-S7', children: [], assets: [] }
            ], assets: [] }
        ], assets: [] },
        { id: 'TOOL', name: '工具', codeSeg: 'TOOL', code: 'TOOL', children: [
            { id: 'TOOL-HAM', name: '手动工具', codeSeg: 'HAM', code: 'TOOL-HAM', children: [], assets: [
                { assetNo: 'TOOL-HAM-A-01', name: '电动扳手', model: 'DCA-500', status: 'available', custodian: null }
            ] }
        ], assets: [] },
        { id: 'METER', name: '仪器仪表', codeSeg: 'METER', code: 'METER', children: [], assets: [] }
    ],

    // 资产列表
    assets: [
        { id: 1, assetNo: 'PLC-MIT-Q-001', name: '三菱PLC', category: '电气设备', model: 'FX3U-64MR', brand: '三菱', status: 'available', custodian: null, location: 'A01货架', price: 3500, quantity: 1, deptId: 'RD-HW', deptName: '研发部 / 硬件组' },
        { id: 2, assetNo: 'PLC-MIT-Q-002', name: '西门子PLC', category: '电气设备', model: 'S7-1200', brand: '西门子', status: 'borrowed', custodian: '张三', location: '-', price: 4200, quantity: 1, deptId: 'TECH', deptName: '技术部' },
        { id: 3, assetNo: 'TOOL-HAM-A-01', name: '电动扳手', category: '工具', model: 'DCA-500', brand: 'DCA', status: 'available', custodian: null, location: 'B02货架', price: 800, quantity: 1, deptId: 'MAINT', deptName: '维修部' }
    ],

    // 审批工单（运行时实例：含工作流节点流转状态）
    approvals: [
        { id: 1, flowNo: 'APV-20240610-001', type: 'borrow', workflowId: 'wf-borrow', assetNo: 'PLC-MIT-Q-001', assetName: '三菱PLC', applicant: '张三', applicantDept: '研发部', status: 'pending', reason: '项目测试需要，用于验证新开发的控制程序，预计使用2周。', returnDate: '2024-06-20', amount: 3500, applyTime: '2024-06-10 09:00', deadline: '2024-06-12 09:00', currentNodeIndex: 1,
            nodes: [
                { name: '发起申请', type: 'start', status: 'done', approver: '张三', opinion: '提交借用申请', time: '06-10 09:00' },
                { name: '直属主管审批', type: 'approval', status: 'current', approver: '李主管' },
                { name: '资产管理员会签', type: 'countersign', status: 'pending', approver: '资产管理员', signers: ['张三', '赵敏'], signStates: {} },
                { name: '分管副总审批', type: 'condition', status: 'pending', approver: '周副总', condition: 'amount>5000' },
                { name: '结束', type: 'end', status: 'pending' }
            ] },
        { id: 2, flowNo: 'APV-20240610-002', type: 'borrow', workflowId: 'wf-borrow', assetNo: 'TOOL-HAM-A-01', assetName: '电动扳手', applicant: '李四', applicantDept: '维修部', status: 'pending', reason: '设备维修使用，申报价值较高需经副总审批。', returnDate: '2024-06-15', amount: 8000, applyTime: '2024-06-10 10:30', deadline: '2024-06-12 10:30', currentNodeIndex: 2,
            nodes: [
                { name: '发起申请', type: 'start', status: 'done', approver: '李四', opinion: '提交借用申请', time: '06-10 10:30' },
                { name: '直属主管审批', type: 'approval', status: 'done', approver: '王主管', opinion: '同意', time: '06-10 11:00' },
                { name: '资产管理员会签', type: 'countersign', status: 'current', approver: '资产管理员', signers: ['张三', '赵敏'], signStates: {} },
                { name: '分管副总审批', type: 'condition', status: 'pending', approver: '周副总', condition: 'amount>5000' },
                { name: '结束', type: 'end', status: 'pending' }
            ] },
        { id: 3, flowNo: 'APV-20240608-015', type: 'borrow', workflowId: 'wf-borrow', assetNo: 'PLC-SIE-S7-003', assetName: '西门子PLC', applicant: '王五', applicantDept: '技术部', status: 'pending', reason: '客户现场调试', returnDate: '2024-06-18', amount: 4200, applyTime: '2024-06-08 15:00', deadline: '2024-06-10 15:00', urgent: true, currentNodeIndex: 1,
            nodes: [
                { name: '发起申请', type: 'start', status: 'done', approver: '王五', opinion: '提交借用申请', time: '06-08 15:00' },
                { name: '直属主管审批', type: 'approval', status: 'current', approver: '李主管' },
                { name: '资产管理员会签', type: 'countersign', status: 'pending', approver: '资产管理员', signers: ['张三', '赵敏'], signStates: {} },
                { name: '分管副总审批', type: 'condition', status: 'pending', approver: '周副总', condition: 'amount>5000' },
                { name: '结束', type: 'end', status: 'pending' }
            ] },
        { id: 4, flowNo: 'APV-20240610-003', type: 'transfer', workflowId: 'wf-transfer', assetNo: 'TOOL-HAM-A-01', assetName: '电动扳手', applicant: '李四', applicantDept: '维修部', status: 'pending', reason: '该设备长期由维修部使用，原保管人已转岗，申请将保管责任永久转让给王五统一管理。', transferee: '王五', transfereeDept: '技术部', amount: 800, applyTime: '2024-06-10 14:00', deadline: '2024-06-12 14:00', currentNodeIndex: 1,
            nodes: [
                { name: '发起申请', type: 'start', status: 'done', approver: '李四', opinion: '提交转让申请', time: '06-10 14:00' },
                { name: '直属主管审批', type: 'approval', status: 'current', approver: '王主管' },
                { name: '接收部门负责人', type: 'approval', status: 'pending', approver: '李主管' },
                { name: '结束', type: 'end', status: 'pending' }
            ] }
    ],

    // 审批工作流定义（可在“系统管理 → 审批流程”中自由设计：加节点、会签/或签、条件分支等）
    workflows: [
        {
            id: 'wf-borrow', name: '借用审批流', bizType: 'borrow',
            nodes: [
                { id: 'b1', name: '发起申请', type: 'start', approverType: 'applicant', approver: '申请人' },
                { id: 'b2', name: '直属主管审批', type: 'approval', approverType: 'supervisor', approver: '直属主管' },
                { id: 'b3', name: '资产管理员会签', type: 'countersign', approverType: 'role', approver: '资产管理员', signers: ['张三', '赵敏'] },
                { id: 'b4', name: '分管副总审批', type: 'condition', approverType: 'user', approver: '周副总', condition: 'amount>5000' },
                { id: 'b5', name: '结束', type: 'end', approver: '' }
            ]
        },
        {
            id: 'wf-transfer', name: '转让审批流', bizType: 'transfer',
            nodes: [
                { id: 't1', name: '发起申请', type: 'start', approverType: 'applicant', approver: '申请人' },
                { id: 't2', name: '直属主管审批', type: 'approval', approverType: 'supervisor', approver: '直属主管' },
                { id: 't3', name: '接收部门负责人', type: 'approval', approverType: 'deptManager', approver: '接收部门负责人' },
                { id: 't4', name: '结束', type: 'end', approver: '' }
            ]
        },
        {
            id: 'wf-return', name: '归还审批流', bizType: 'return',
            nodes: [
                { id: 'r1', name: '发起归还', type: 'start', approverType: 'applicant', approver: '申请人' },
                { id: 'r2', name: '资产管理员确认', type: 'approval', approverType: 'role', approver: '资产管理员' },
                { id: 'r3', name: '结束', type: 'end', approver: '' }
            ]
        }
    ],

    // 用户列表
    users: [
        { id: 1, employeeNo: '1001', name: '张三', role: 'warehouse', roleName: '仓库管理员', dept: '行政部', deptId: 'ADMIN', supervisor: '-', email: 'zhangsan@company.com', phone: '13800138000', status: 'active' },
        { id: 2, employeeNo: '1002', name: '李四', role: 'supervisor', roleName: '部门主管', dept: '研发部', deptId: 'RD', supervisor: '张总', email: 'lisi@company.com', phone: '13800138001', status: 'active' },
        { id: 3, employeeNo: '1003', name: '王五', role: 'employee', roleName: '普通员工', dept: '技术部', deptId: 'TECH', supervisor: '李主管', email: 'wangwu@company.com', phone: '13800138002', status: 'active' },
        { id: 4, employeeNo: '1004', name: '赵敏', role: 'dept_admin', roleName: '部门管理员', dept: '研发部', deptId: 'RD', supervisor: '张总', email: 'zhaomin@company.com', phone: '13800138003', status: 'active' }
    ],

    // 部门树（共享资产池：每个资产归属一个部门，支持任意多层级）
    departments: [
        { id: 'RD', name: '研发部', code: 'RD', parentId: null, manager: '李四', assetCount: 1, status: 'active', children: [
            { id: 'RD-SW', name: '软件组', code: 'RD-SW', parentId: 'RD', manager: '赵敏', assetCount: 0, status: 'active', children: [
                { id: 'RD-SW-FE', name: '前端小组', code: 'RD-SW-FE', parentId: 'RD-SW', manager: '钱七', assetCount: 0, status: 'active' },
                { id: 'RD-SW-BE', name: '后端小组', code: 'RD-SW-BE', parentId: 'RD-SW', manager: '孙八', assetCount: 0, status: 'active' }
            ]},
            { id: 'RD-HW', name: '硬件组', code: 'RD-HW', parentId: 'RD', manager: '周九', assetCount: 1, status: 'active' }
        ]},
        { id: 'TECH', name: '技术部', code: 'TECH', parentId: null, manager: '李主管', assetCount: 1, status: 'active', children: [
            { id: 'TECH-OPS', name: '运维组', code: 'TECH-OPS', parentId: 'TECH', manager: '吴十', assetCount: 0, status: 'active' }
        ]},
        { id: 'MAINT', name: '维修部', code: 'MAINT', parentId: null, manager: '王主管', assetCount: 1, status: 'active' },
        { id: 'ADMIN', name: '行政部', code: 'ADMIN', parentId: null, manager: '张三', assetCount: 0, status: 'active' }
    ],

    // 类别树
    categories: [
        { id: 1, name: '电气设备', parentId: null, assetCount: 45, children: [
            { id: 11, name: 'PLC', parentId: 1, assetCount: 20 },
            { id: 12, name: '传感器', parentId: 1, assetCount: 15 },
            { id: 13, name: '控制器', parentId: 1, assetCount: 10 }
        ]},
        { id: 2, name: '工具', parentId: null, assetCount: 80, children: [
            { id: 21, name: '电动工具', parentId: 2, assetCount: 50 },
            { id: 22, name: '手动工具', parentId: 2, assetCount: 30 }
        ]},
        { id: 3, name: '仪器仪表', parentId: null, assetCount: 31, children: [
            { id: 31, name: '测量仪器', parentId: 3, assetCount: 31 }
        ]}
    ],

    // 报表数据
    reports: {
        summary: {
            total: 156,
            available: 120,
            borrowed: 36,
            totalValue: 1250000,
            byCategory: [
                { category: '电气设备', total: 45, available: 35, borrowed: 10, value: 450000, percent: 36 },
                { category: '工具', total: 80, available: 60, borrowed: 20, value: 320000, percent: 26 },
                { category: '仪器仪表', total: 31, available: 25, borrowed: 6, value: 480000, percent: 38 }
            ],
            byDept: [
                { dept: '研发部', total: 60, available: 45, borrowed: 15, value: 520000, percent: 42 },
                { dept: '技术部', total: 50, available: 40, borrowed: 10, value: 430000, percent: 34 },
                { dept: '维修部', total: 46, available: 35, borrowed: 11, value: 300000, percent: 24 }
            ]
        },
        borrowed: [
            { flowNo: 'APV-001', assetNo: 'PLC-001', borrower: '张三', applyTime: '06-10', approver: '李主管', approveTime: '06-10', returnTime: '-', status: '使用中' },
            { flowNo: 'APV-002', assetNo: 'TOOL-05', borrower: '李四', applyTime: '06-08', approver: '王主管', approveTime: '06-08', returnTime: '06-15', status: '已归还' }
        ],
        overdue: [
            { assetNo: 'TOOL-02', name: '电动扳手', custodian: '张三', dueDate: '05-25', overdueDays: 16 },
            { assetNo: 'PLC-008', name: '西门子PLC', custodian: '李四', dueDate: '06-01', overdueDays: 9 }
        ]
    }
};

// 导出数据
window.mockData = mockData;
