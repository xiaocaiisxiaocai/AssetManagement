import type {
  ProjectStageDto,
  ProjectWorkspaceDto,
} from '#/api/project-workspace';

import { defineComponent, h, reactive } from 'vue';

import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useProjectTestingWorkspace } from './use-project-testing-workspace';

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  create: vi.fn(),
  download: vi.fn(),
  get: vi.fn(),
  remove: vi.fn(),
  success: vi.fn(),
}));
vi.mock('@vben/access', () => ({
  useAccess: () => ({ hasAccessByCodes: () => false }),
}));
vi.mock('element-plus', () => ({
  ElMessage: { success: mocks.success, warning: vi.fn() },
  ElMessageBox: { confirm: mocks.confirm },
}));
vi.mock('#/utils/download', () => ({ downloadBlob: mocks.download }));
vi.mock('#/api/project-workspace', () => ({
  createProjectStageApi: mocks.create,
  deleteProjectDocumentApi: vi.fn(),
  deleteProjectStageApi: mocks.remove,
  downloadProjectDocumentApi: vi.fn(),
  getProjectWorkspaceApi: mocks.get,
  updateProjectStageApi: vi.fn(),
  uploadProjectDocumentApi: vi.fn(),
}));

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

const wrappers: ReturnType<typeof mount>[] = [];
function workspace(projectId: number): ProjectWorkspaceDto {
  return {
    canExport: true,
    canManage: true,
    documents: [],
    projectId,
    stages: [],
  };
}
function setup(progressCode = 'testing') {
  const props = reactive({
    enabled: true,
    projectId: 1,
    projectProgressCode: progressCode,
  });
  let state!: ReturnType<typeof useProjectTestingWorkspace>;
  const wrapper = mount(
    defineComponent({
      setup() {
        state = useProjectTestingWorkspace(props);
        return () => h('div');
      },
    }),
  );
  wrappers.push(wrapper);
  return { props, state };
}
beforeEach(() => {
  vi.clearAllMocks();
  mocks.get.mockImplementation(async (id: number) => workspace(id));
  mocks.create.mockResolvedValue({ id: 1 });
  mocks.confirm.mockResolvedValue('confirm');
});
afterEach(() => wrappers.splice(0).forEach((wrapper) => wrapper.unmount()));

describe('项目测试工作台交互', () => {
  it('计划中的项目不能打开新增阶段弹窗，测试中才可以', async () => {
    const { state } = setup('planning');
    await flushPromises();
    state.openCreateStage();
    expect(state.canCreateStage.value).toBe(false);
    expect(state.stageDialogVisible.value).toBe(false);

    const testing = setup('testing');
    await flushPromises();
    testing.state.openCreateStage();
    expect(testing.state.canCreateStage.value).toBe(true);
    expect(testing.state.stageDialogVisible.value).toBe(true);
  });

  it('保存失败保留完整阶段草稿与编辑窗口', async () => {
    const { state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '耐久测试';
    state.stageForm.testContent = '运行 48 小时\n记录温升';
    mocks.create.mockRejectedValueOnce(new Error('版本冲突'));
    await state.saveStage();
    expect(state.stageDialogVisible.value).toBe(true);
    expect(state.stageForm.testContent).toBe('运行 48 小时\n记录温升');
    expect(state.savingStage.value).toBe(false);
  });

  it('保存未完成时重复点击只提交一次', async () => {
    const pending = deferred<unknown>();
    const { state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '温升测试';
    mocks.create.mockReturnValueOnce(pending.promise);
    const save = state.saveStage();
    await state.saveStage();
    expect(mocks.create).toHaveBeenCalledOnce();
    pending.resolve({ id: 1 });
    await save;
    expect(state.stageDialogVisible.value).toBe(false);
  });

  it('关闭并重新打开同一项目后，旧保存响应不会关闭新草稿', async () => {
    const pending = deferred<unknown>();
    const { props, state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '旧草稿';
    mocks.create.mockReturnValueOnce(pending.promise);
    const save = state.saveStage();
    props.enabled = false;
    await flushPromises();
    props.enabled = true;
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '重新打开的新草稿';
    pending.resolve({ id: 1 });
    await save;
    expect(state.stageDialogVisible.value).toBe(true);
    expect(state.stageForm.name).toBe('重新打开的新草稿');
    expect(mocks.success).not.toHaveBeenCalled();
  });

  it('删除确认期间切换项目，不会对新项目发送旧阶段删除', async () => {
    const confirmation = deferred<string>();
    const { props, state } = setup();
    await flushPromises();
    mocks.confirm.mockReturnValueOnce(confirmation.promise);
    const deletion = state.removeStage({
      id: 9,
      name: '旧项目阶段',
      projectId: 1,
      rowVersion: 1,
    } as ProjectStageDto);
    props.projectId = 2;
    await flushPromises();
    confirmation.resolve('confirm');
    await deletion;
    expect(mocks.remove).not.toHaveBeenCalled();
    expect(state.workspace.value?.projectId).toBe(2);
  });
});
