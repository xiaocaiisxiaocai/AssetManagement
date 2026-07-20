import { describe, expect, it } from 'vitest';

import { buildProjectPageQuery } from './project-page-query';

describe('测试项目服务端分页查询', () => {
  it('传递筛选条件并清理空白文本', () => {
    expect(
      buildProjectPageQuery(
        {
          code: ' TP-1 ',
          name: ' ',
          ownerId: 7,
          progressCode: 'testing',
          projectTypeCode: '',
        },
        'active',
        3,
        20,
      ),
    ).toEqual({
      code: 'TP-1',
      deleteStatus: 'active',
      name: undefined,
      ownerId: 7,
      page: 3,
      pageSize: 20,
      progressCode: 'testing',
      projectTypeCode: undefined,
    });
  });
});
