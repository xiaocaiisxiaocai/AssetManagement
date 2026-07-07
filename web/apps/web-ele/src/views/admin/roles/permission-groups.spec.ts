import type { MenuDto, PermissionDto } from '#/api/role';

import { describe, expect, it } from 'vitest';

import { buildPermissionGroups } from './permission-groups';

const permissions: PermissionDto[] = [
  { id: 1, code: 'project:view', name: '查看项目', module: 'project' },
  { id: 2, code: 'project:create', name: '新增项目', module: 'project' },
  { id: 3, code: 'material:view', name: '查看料件', module: 'material' },
  { id: 4, code: 'material-flow:approve', name: '审批流转', module: 'material-flow' },
];

const menus: MenuDto[] = [
  {
    id: 1,
    name: 'Material',
    title: '新产品新技术',
    sort: 1,
    type: 'menu',
    children: [
      {
        id: 2,
        name: 'MaterialHome',
        title: '项目总览',
        sort: 1,
        type: 'menu',
      },
      {
        id: 3,
        name: 'MaterialProjects',
        title: '测试项目',
        sort: 2,
        type: 'menu',
      },
    ],
  },
];

describe('角色权限分组', () => {
  it('子菜单权限被兄弟菜单完全包含时只展示更完整的分组', () => {
    const groups = buildPermissionGroups({
      menus,
      permissions,
      selectedPermissionIds: [1, 2, 3, 4],
    });

    expect(groups.map((group) => [group.label, group.total])).toEqual([
      ['测试项目', 4],
    ]);
  });

  it('测试项目分组展示正式料件流转权限码', () => {
    const groups = buildPermissionGroups({
      menus,
      permissions: [
        ...permissions,
        { id: 5, code: 'material-flow:transfer', name: '发起料件流转', module: 'material-flow' },
      ],
      selectedPermissionIds: [1, 2, 3, 4, 5],
    });

    const projectGroup = groups.find((group) => group.label === '测试项目');

    expect(projectGroup?.permissions.map((perm) => perm.code)).toEqual([
      'project:create',
      'project:view',
      'material:view',
      'material-flow:approve',
      'material-flow:transfer',
    ]);
    expect(projectGroup?.selected).toBe(5);
    expect(projectGroup?.total).toBe(5);
  });

  it('多个审批子菜单共用同一批权限时只展示父级分组', () => {
    const groups = buildPermissionGroups({
      menus: [
        {
          id: 10,
          name: 'Approval',
          title: '审批管理',
          sort: 1,
          type: 'menu',
          children: [
            { id: 11, name: 'ApprovalPending', title: '待我审批', sort: 1, type: 'menu' },
            { id: 12, name: 'ApprovalMine', title: '我的申请', sort: 2, type: 'menu' },
            { id: 13, name: 'ConfirmReturn', title: '待确认入库', sort: 3, type: 'menu' },
          ],
        },
      ],
      permissions: [
        { id: 10, code: 'approval:view', name: '查看审批', module: 'approval' },
        { id: 11, code: 'approval:handle', name: '处理审批', module: 'approval' },
      ],
      selectedPermissionIds: [10],
    });

    expect(groups.map((group) => [group.label, group.selected, group.total])).toEqual([
      ['审批管理', 1, 2],
    ]);
  });

  it('多个报表子菜单共用同一批权限时只展示父级分组', () => {
    const groups = buildPermissionGroups({
      menus: [
        {
          id: 20,
          name: 'Report',
          title: '报表统计',
          sort: 1,
          type: 'menu',
          children: [
            { id: 21, name: 'ReportSummary', title: '资产汇总', sort: 1, type: 'menu' },
            { id: 22, name: 'ReportBorrow', title: '借用明细', sort: 2, type: 'menu' },
            { id: 23, name: 'ReportOverdue', title: '逾期资产', sort: 3, type: 'menu' },
          ],
        },
      ],
      permissions: [
        { id: 20, code: 'report:view', name: '查看报表', module: 'report' },
        { id: 21, code: 'report:export', name: '导出报表', module: 'report' },
        { id: 22, code: 'report:remind', name: '逾期提醒', module: 'report' },
      ],
      selectedPermissionIds: [20],
    });

    expect(groups.map((group) => [group.label, group.selected, group.total])).toEqual([
      ['报表统计', 1, 3],
    ]);
  });
});
