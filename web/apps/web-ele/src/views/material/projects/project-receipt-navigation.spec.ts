import type { TestProjectItem } from '#/api/test-project';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  bindProjectReceiptNavigation,
  findProjectForReceipt,
  receiptNotificationFlowId,
  receiptProjectUnavailableMessage,
} from './project-receipt-navigation';

const mocks = vi.hoisted(() => ({
  getFlow: vi.fn(),
  listProjects: vi.fn(),
  warning: vi.fn(),
}));

vi.mock('element-plus', () => ({
  ElMessage: { warning: mocks.warning },
}));

vi.mock('#/api/material', () => ({
  getMaterialFlowApi: mocks.getFlow,
}));

vi.mock('#/api/test-project', () => ({
  listTestProjectsPageApi: mocks.listProjects,
}));

const project = { id: 4, name: '测评项目' } as TestProjectItem;

beforeEach(() => {
  vi.clearAllMocks();
});

describe('待接收通知定位', () => {
  it('只接受料件待接收通知', () => {
    expect(
      receiptNotificationFlowId({
        flowId: '18',
        intent: 'receive',
        source: 'material',
      }),
    ).toBe(18);
    expect(
      receiptNotificationFlowId({
        flowId: '18',
        intent: 'receive',
        source: 'asset',
      }),
    ).toBe(0);
  });

  it('当前页已有项目时不再翻页', async () => {
    const loadPage = vi.fn();

    await expect(findProjectForReceipt(4, [project], loadPage)).resolves.toBe(
      project,
    );
    expect(loadPage).not.toHaveBeenCalled();
  });

  it('当前页没有项目时按编号继续查找', async () => {
    const loadPage = vi
      .fn()
      .mockResolvedValueOnce({ items: [{ id: 1 }], total: 2 })
      .mockResolvedValueOnce({ items: [project], total: 2 });

    await expect(findProjectForReceipt(4, [], loadPage)).resolves.toBe(project);
    expect(loadPage).toHaveBeenCalledTimes(2);
  });

  it('打开工作台并定位待我接收', async () => {
    mocks.getFlow.mockResolvedValue({ projectId: 4 });
    const openFollowups = vi.fn(async () => {});
    const route = {
      path: '/material/projects',
      query: { flowId: '18', intent: 'receive', source: 'material' },
    };
    const router = { replace: vi.fn() };
    const { openReceiptNotification } = bindProjectReceiptNavigation(
      { projects: { value: [project] }, route, router },
      { openFollowups },
    );

    await openReceiptNotification();

    expect(openFollowups).toHaveBeenCalledWith(project, {
      flowTab: 'receiving',
    });
    expect(router.replace).toHaveBeenCalledWith({
      path: '/material/projects',
      query: {},
    });
    expect(mocks.warning).not.toHaveBeenCalled();
  });

  it('项目打不开时提示联系申请人撤回', async () => {
    mocks.getFlow.mockResolvedValue({ projectId: 99 });
    mocks.listProjects.mockResolvedValue({ items: [], total: 0 });
    const openFollowups = vi.fn();
    const route = {
      path: '/material/projects',
      query: { flowId: '18', intent: 'receive', source: 'material' },
    };
    const router = { replace: vi.fn() };
    const { openReceiptNotification } = bindProjectReceiptNavigation(
      { projects: { value: [] }, route, router },
      { openFollowups },
    );

    await openReceiptNotification();

    expect(openFollowups).not.toHaveBeenCalled();
    expect(mocks.warning).toHaveBeenCalledWith(
      receiptProjectUnavailableMessage,
    );
    expect(router.replace).toHaveBeenCalled();
  });
});
