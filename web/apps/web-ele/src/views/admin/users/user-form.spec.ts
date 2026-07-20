import { describe, expect, it } from 'vitest';

import {
  buildUserPayload,
  resolveDefaultEmployeeRoleId,
  resolveDepartmentSupervisor,
  userToForm,
} from './user-form';

describe('用户编辑表单契约', () => {
  it('新增用户默认选择启用的普通员工角色', () => {
    expect(
      resolveDefaultEmployeeRoleId([
        { code: 'admin', id: 1, isActive: true },
        { code: 'employee', id: 4, isActive: true },
      ]),
    ).toBe(4);
  });

  it('普通员工角色缺失或停用时不默认选择其他角色', () => {
    expect(
      resolveDefaultEmployeeRoleId([
        { code: 'admin', id: 1, isActive: true },
        { code: 'employee', id: 4, isActive: false },
      ]),
    ).toBeUndefined();
  });

  it('往返编辑时保留手机号和直属主管', () => {
    const form = userToForm({
      departmentId: 3,
      email: 'tester@example.com',
      employeeNo: '1002',
      id: 2,
      isActive: true,
      name: '测试用户',
      phone: '13800000000',
      roleIds: [4],
      supervisorId: 8,
      supervisorName: '主管甲',
    });

    form.name = '修改后的用户';

    expect(buildUserPayload(form)).toMatchObject({
      name: '修改后的用户',
      phone: '13800000000',
      supervisorId: 8,
    });
  });

  it('选择存在负责人的部门时自动带出直属主管', () => {
    expect(
      resolveDepartmentSupervisor(
        [
          {
            id: 3,
            managerId: 8,
            managerName: '主管甲',
          },
        ],
        3,
      ),
    ).toEqual({ supervisorId: 8, supervisorName: '主管甲' });
  });

  it('选择无负责人的部门或清空部门时清空旧主管', () => {
    const departments = [
      { id: 3, managerId: undefined, managerName: undefined },
    ];

    expect(resolveDepartmentSupervisor(departments, 3)).toEqual({
      supervisorId: undefined,
      supervisorName: undefined,
    });
    expect(resolveDepartmentSupervisor(departments, undefined)).toEqual({
      supervisorId: undefined,
      supervisorName: undefined,
    });
  });

  it('编辑用户时不会把本人自动设为直属主管', () => {
    expect(
      resolveDepartmentSupervisor(
        [{ id: 3, managerId: 8, managerName: '当前用户' }],
        3,
        8,
      ),
    ).toEqual({ supervisorId: undefined, supervisorName: undefined });
  });
});
