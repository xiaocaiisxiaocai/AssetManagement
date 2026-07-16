import { describe, expect, it } from 'vitest';

import {
  loadAssigneeSelection,
  roleAssigneeIdentity,
  serializeAssigneeSelection,
  userAssigneeIdentity,
} from './assignee-identities';

describe('工作流审批人标识', () => {
  it('新选择的用户和角色使用带类型的稳定标识', () => {
    expect(userAssigneeIdentity(12)).toBe('user:12');
    expect(roleAssigneeIdentity('supervisor')).toBe('role:supervisor');
  });

  it('加载和保存旧数字值时不猜测其含义', () => {
    const loaded = loadAssigneeSelection('1001', null, null);

    expect(loaded).toEqual({ type: 'username', value: '1001' });
    expect(serializeAssigneeSelection(loaded.type, loaded.value)).toEqual({
      assignee: '1001',
      candidateGroups: '',
      candidateUsers: '',
    });
  });

  it('多人旧值逐项原样保留，新值也可混合加载', () => {
    const loaded = loadAssigneeSelection(null, '1001,user:12, 张三', null);

    expect(loaded).toEqual({
      type: 'usernames',
      value: ['1001', 'user:12', '张三'],
    });
    expect(serializeAssigneeSelection(loaded.type, loaded.value)).toMatchObject(
      {
        candidateUsers: '1001,user:12,张三',
      },
    );
  });

  it('组织层级负责人作为可编辑的动态审批人来源保存', () => {
    expect(loadAssigneeSelection('sectionManager')).toEqual({
      type: 'sectionManager',
      value: '',
    });
    expect(loadAssigneeSelection('departmentManager')).toEqual({
      type: 'departmentManager',
      value: '',
    });
    expect(serializeAssigneeSelection('sectionManager', '')).toMatchObject({
      assignee: 'sectionManager',
    });
    expect(serializeAssigneeSelection('departmentManager', '')).toMatchObject({
      assignee: 'departmentManager',
    });
    expect(loadAssigneeSelection('orgManager:division')).toEqual({
      type: 'organizationManager',
      value: 'division',
    });
    expect(
      serializeAssigneeSelection('organizationManager', 'division'),
    ).toMatchObject({ assignee: 'orgManager:division' });
  });
});
