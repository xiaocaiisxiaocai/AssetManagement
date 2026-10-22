import { reactive, ref } from 'vue';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createLatestRequestGuard } from '#/utils/latest-request';

import { bindProjectFollowupActions } from './project-followup-actions';

const mocks = vi.hoisted(() => ({
  create: vi.fn(),
  list: vi.fn(),
  success: vi.fn(),
}));

vi.mock('element-plus', () => ({
  ElMessage: { success: mocks.success, warning: vi.fn() },
  ElMessageBox: { confirm: vi.fn() },
}));

vi.mock('#/api/test-project', () => ({
  createTestProjectFollowupApi: mocks.create,
  deleteTestProjectFollowupApi: vi.fn(),
  listTestProjectFollowupsApi: mocks.list,
  updateTestProjectFollowupApi: vi.fn(),
}));

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

function setup() {
  const page = {
    activeProjectTab: ref('followups'),
    currentProject: ref({
      canWriteFollowUp: true,
      id: 1,
      isDeleted: false,
    }),
    editingFollowupId: ref<null | number>(null),
    followupDrawerVisible: ref(true),
    followupForm: reactive({ content: '旧草稿' }),
    followupLoading: ref(false),
    followupOperationGuard: createLatestRequestGuard(),
    followupRequestGuard: createLatestRequestGuard(),
    followupSaving: ref(false),
    followups: ref([]),
    loadData: vi.fn(async () => {}),
  };
  const actions = bindProjectFollowupActions(page as any, {
    loadProjectMaterials: vi.fn(async () => {}),
    resetMaterialQuery: vi.fn(),
  });
  return { actions, page };
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.list.mockResolvedValue([]);
});

describe('项目跟进保存时序', () => {
  it('保存未完成时重复点击只提交一次', async () => {
    const pending = deferred<unknown>();
    mocks.create.mockReturnValueOnce(pending.promise);
    const { actions, page } = setup();

    const first = actions.saveFollowup();
    await actions.saveFollowup();

    expect(mocks.create).toHaveBeenCalledOnce();
    pending.resolve({});
    await first;
    expect(page.followupSaving.value).toBe(false);
  });

  it('关闭并重新打开同一项目后旧响应不会清空新草稿', async () => {
    const pending = deferred<unknown>();
    mocks.create.mockReturnValueOnce(pending.promise);
    const { actions, page } = setup();

    const save = actions.saveFollowup();
    page.followupDrawerVisible.value = false;
    page.followupOperationGuard.invalidate();
    page.followupSaving.value = false;
    page.followupDrawerVisible.value = true;
    page.followupForm.content = '重新打开后的新草稿';
    pending.resolve({});
    await save;

    expect(page.followupForm.content).toBe('重新打开后的新草稿');
    expect(mocks.success).not.toHaveBeenCalled();
  });
});
