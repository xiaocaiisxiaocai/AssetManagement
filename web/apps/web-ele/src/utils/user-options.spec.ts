import { describe, expect, it } from 'vitest';

import { mergeSelectedUserOption, mergeUserOptions } from './user-options';

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
});
