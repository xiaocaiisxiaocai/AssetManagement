import { beforeEach, describe, expect, it, vi } from 'vitest';

import { getRoleAccessOptionsApi } from './role';

const requestClientMock = vi.hoisted(() => ({
  get: vi.fn(),
}));

vi.mock('#/api/request', () => ({
  requestClient: requestClientMock,
}));

describe('角色授权目录接口', () => {
  beforeEach(() => {
    requestClientMock.get.mockReset();
  });

  it('通过角色域只读端点一次获取权限与菜单目录', async () => {
    const data = {
      menus: [{ id: 2, name: 'Asset', sort: 1, type: 'menu' }],
      permissions: [
        { id: 1, code: 'asset:view', module: 'asset', name: '查看资产' },
      ],
    };
    requestClientMock.get.mockResolvedValue({
      code: 0,
      data,
      message: 'ok',
    });

    await expect(getRoleAccessOptionsApi()).resolves.toEqual(data);
    expect(requestClientMock.get).toHaveBeenCalledOnce();
    expect(requestClientMock.get).toHaveBeenCalledWith('/roles/access-options');
  });
});
