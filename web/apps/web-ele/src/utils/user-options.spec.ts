import { describe, expect, it } from 'vitest';

import {
  mergeSelectedUserOption,
  mergeUserOptions,
  replaceUserOptions,
  selectableTransferees,
} from './user-options';

describe('远程用户选项回填', () => {
  it('新搜索结果按 id 合并，保留先前已选项', () => {
    expect(
      mergeUserOptions(
        [{ id: 1, employeeNo: '1001', name: '旧名' }],
        [
          { id: 1, employeeNo: '1001', name: '新名' },
          { id: 2, employeeNo: '1002', name: '用户二' },
        ],
      ),
    ).toEqual([
      { id: 1, employeeNo: '1001', name: '新名' },
      { id: 2, employeeNo: '1002', name: '用户二' },
    ]);
  });

  it('首屏没有已选用户时回填实体携带的姓名并保留后续搜索结果', () => {
    const withSelected = mergeSelectedUserOption(
      [{ id: 1, employeeNo: '1001', name: '首屏用户' }],
      { id: 99, name: '当前保管人' },
    );

    expect(
      mergeUserOptions(withSelected, [
        { id: 2, employeeNo: '1002', name: '搜索结果' },
      ]),
    ).toEqual([
      { id: 1, employeeNo: '1001', name: '首屏用户' },
      { id: 99, employeeNo: '', name: '当前保管人' },
      { id: 2, employeeNo: '1002', name: '搜索结果' },
    ]);
  });

  it('远程搜索只保留本次结果和当前已选项', () => {
    expect(
      replaceUserOptions(
        [
          { id: 1, employeeNo: '1001', name: '旧结果' },
          { departmentId: 8, id: 9, employeeNo: '1009', name: '已选' },
        ],
        [{ departmentId: 3, id: 2, employeeNo: '1002', name: '新结果' }],
        [9],
      ),
    ).toEqual([
      { departmentId: 8, id: 9, employeeNo: '1009', name: '已选' },
      { departmentId: 3, id: 2, employeeNo: '1002', name: '新结果' },
    ]);
  });

  it('转让候选人必须属于有效部门，并排除申请人和当前保管人', () => {
    expect(
      selectableTransferees(
        [
          { departmentId: 1, id: 1, employeeNo: '1001', name: '申请人' },
          { departmentId: null, id: 2, employeeNo: '1002', name: '无部门' },
          { departmentId: 2, id: 3, employeeNo: '1003', name: '接收人' },
        ],
        [1],
      ),
    ).toEqual([{ departmentId: 2, id: 3, employeeNo: '1003', name: '接收人' }]);
  });
});
