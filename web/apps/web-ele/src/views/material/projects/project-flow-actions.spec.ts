import { reactive, ref } from 'vue';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createLatestRequestGuard } from '#/utils/latest-request';

import { bindProjectFlowActions } from './project-flow-actions';

const mocks = vi.hoisted(() => ({
  handled: vi.fn(),
  mine: vi.fn(),
  pending: vi.fn(),
}));

vi.mock('element-plus', () => ({
  ElMessage: { success: vi.fn(), warning: vi.fn() },
  ElMessageBox: { confirm: vi.fn(), prompt: vi.fn() },
}));

vi.mock('#/api/material', () => ({
  approveFlowApi: vi.fn(),
  listHandledFlowsPageApi: mocks.handled,
  listMyFlowsPageApi: mocks.mine,
  listPendingFlowsPageApi: mocks.pending,
  rejectFlowApi: vi.fn(),
  releaseMaterialFlowApi: vi.fn(),
  withdrawMaterialFlowApi: vi.fn(),
}));

const emptyPage = { items: [], total: 0 };

/** canApprove：账号审批权限；canHandle：项目是否处于可在工作台内处理的阶段。 */
function setup(canApprove: boolean, canHandle = canApprove) {
  const selectNode = vi.fn(async () => ({ id: 'node-1' }));
  const page = {
    currentMaterialActionAccess: ref({ canApprove: canHandle }),
    materialActionAccess: ref({ canApprove }),
    currentProject: ref({ id: 7 }),
    followupDrawerVisible: ref(true),
    handledFlowLoading: ref(false),
    handledFlowQuery: reactive({ page: 1, pageSize: 10 }),
    handledFlowRequestGuard: createLatestRequestGuard(),
    handledFlows: ref([]),
    handledFlowTotal: ref(0),
    isCurrentProjectReadOnly: ref(false),
    myFlowLoading: ref(false),
    myFlowQuery: reactive({ page: 1, pageSize: 10 }),
    myFlowRequestGuard: createLatestRequestGuard(),
    myFlows: ref([]),
    myFlowTotal: ref(0),
    pendingFlowLoading: ref(false),
    pendingFlowQuery: reactive({ page: 1, pageSize: 10 }),
    pendingFlowRequestGuard: createLatestRequestGuard(),
    pendingFlows: ref([]),
    pendingFlowTotal: ref(0),
    processingFlowIds: ref<number[]>([]),
    workflowNodeSelector: ref({ selectNode }),
  };
  const actions = bindProjectFlowActions(page as any, {
    loadProjectMaterials: vi.fn(async () => {}),
  });
  return { actions, selectNode };
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.handled.mockResolvedValue(emptyPage);
  mocks.mine.mockResolvedValue(emptyPage);
  mocks.pending.mockResolvedValue(emptyPage);
});

describe('项目流转列表加载', () => {
  it('有审批权限且未指定列表时同时加载三类记录', async () => {
    const { actions } = setup(true);

    await actions.loadProjectFlows();

    const query = { page: 1, pageSize: 10, projectId: 7 };
    expect(mocks.pending).toHaveBeenCalledOnce();
    expect(mocks.pending).toHaveBeenCalledWith(query);
    expect(mocks.handled).toHaveBeenCalledOnce();
    expect(mocks.handled).toHaveBeenCalledWith(query);
    expect(mocks.mine).toHaveBeenCalledOnce();
    expect(mocks.mine).toHaveBeenCalledWith(query);
  });

  it('没有审批权限且未指定列表时只加载我的发起', async () => {
    const { actions } = setup(false);

    await actions.loadProjectFlows();

    expect(mocks.mine).toHaveBeenCalledOnce();
    expect(mocks.pending).not.toHaveBeenCalled();
    expect(mocks.handled).not.toHaveBeenCalled();
  });

  it('项目已进入落地跟进等锁定阶段时仍按账号权限加载审批记录', async () => {
    const { actions } = setup(true, false);

    await actions.loadProjectFlows();

    expect(mocks.pending).toHaveBeenCalledOnce();
    expect(mocks.handled).toHaveBeenCalledOnce();
    expect(mocks.mine).toHaveBeenCalledOnce();
  });

  it('项目锁定时不在工作台内处理审批', async () => {
    const { actions, selectNode } = setup(true, false);
    const row = { id: 3, materialName: '料件' } as any;

    await actions.approveFlow(row);
    await actions.rejectFlow(row);

    expect(selectNode).not.toHaveBeenCalled();
  });

  it('指定已处理列表时只加载该列表', async () => {
    const { actions } = setup(true);

    await actions.loadProjectFlows(undefined, 'handled');

    expect(mocks.handled).toHaveBeenCalledOnce();
    expect(mocks.pending).not.toHaveBeenCalled();
    expect(mocks.mine).not.toHaveBeenCalled();
  });
});
