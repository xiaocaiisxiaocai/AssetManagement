import type {
  ProjectStageDto,
  ProjectWorkspaceDto,
} from '#/api/project-workspace';

import { defineComponent, h, reactive } from 'vue';

import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { businessDateText } from '#/utils/business-date';

import { useProjectTestingWorkspace } from './use-project-testing-workspace';

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  create: vi.fn(),
  download: vi.fn(),
  get: vi.fn(),
  remove: vi.fn(),
  success: vi.fn(),
  update: vi.fn(),
  warning: vi.fn(),
}));
vi.mock('@vben/access', () => ({
  useAccess: () => ({ hasAccessByCodes: () => false }),
}));
vi.mock('element-plus', () => ({
  ElMessage: { success: mocks.success, warning: mocks.warning },
  ElMessageBox: { confirm: mocks.confirm },
}));
vi.mock('#/utils/download', () => ({ downloadBlob: mocks.download }));
vi.mock('#/api/project-workspace', () => ({
  createProjectStageApi: mocks.create,
  deleteProjectDocumentApi: vi.fn(),
  deleteProjectStageApi: mocks.remove,
  downloadProjectDocumentApi: vi.fn(),
  getProjectWorkspaceApi: mocks.get,
  updateProjectStageApi: mocks.update,
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
  mocks.update.mockResolvedValue({ id: 7, rowVersion: 4 });
  mocks.confirm.mockResolvedValue('confirm');
});

function shiftDate(value: string, days: number) {
  const year = Number(value.slice(0, 4));
  const month = Number(value.slice(5, 7));
  const day = Number(value.slice(8, 10));
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() + days);
  const nextMonth = String(date.getUTCMonth() + 1).padStart(2, '0');
  const nextDay = String(date.getUTCDate()).padStart(2, '0');
  return `${date.getUTCFullYear()}-${nextMonth}-${nextDay}`;
}
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

  it('确认后先结束上一阶段，再新增本阶段', async () => {
    const today = businessDateText();
    const previousStart = shiftDate(today, -10);
    const currentStart = shiftDate(today, -3);
    mocks.get.mockImplementation(async (id: number) => ({
      ...workspace(id),
      stages: [
        {
          endDate: null,
          id: 7,
          name: '第一阶段',
          objective: '目标',
          projectId: id,
          rowVersion: 3,
          sequence: 1,
          startDate: previousStart,
          status: 'in_progress',
          testContent: '已填写内容',
          testResult: '已填写结果',
          updatedAt: '2026-10-01T00:00:00Z',
        } satisfies ProjectStageDto,
      ],
    }));
    const { state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '第二阶段';
    state.stageForm.startDate = currentStart;
    const save = state.saveStage();
    await flushPromises();
    expect(mocks.create).not.toHaveBeenCalled();
    expect(mocks.update).not.toHaveBeenCalled();
    expect(state.previousStageClose.value?.endDate).toBe(
      shiftDate(currentStart, -1),
    );
    state.confirmPreviousStageClose();
    await save;
    expect(mocks.update).toHaveBeenCalledWith(1, 7, {
      endDate: shiftDate(currentStart, -1),
      name: '第一阶段',
      objective: '目标',
      rowVersion: 3,
      sequence: 1,
      startDate: previousStart,
      testContent: '已填写内容',
      testResult: '已填写结果',
    });
    expect(mocks.update.mock.invocationCallOrder[0]).toBeLessThan(
      mocks.create.mock.invocationCallOrder[0] ?? 0,
    );
    expect(mocks.create).toHaveBeenCalledWith(
      1,
      expect.objectContaining({
        name: '第二阶段',
        startDate: currentStart,
      }),
    );
    expect(state.stageDialogVisible.value).toBe(false);
  });

  it('取消结束上一阶段时不调用接口并保留输入', async () => {
    const today = businessDateText();
    mocks.get.mockImplementation(async (id: number) => ({
      ...workspace(id),
      stages: [
        {
          endDate: null,
          id: 7,
          name: '第一阶段',
          projectId: id,
          rowVersion: 3,
          sequence: 1,
          startDate: shiftDate(today, -10),
          status: 'in_progress',
          testContent: '已填写内容',
          testResult: '已填写结果',
          updatedAt: '2026-10-01T00:00:00Z',
        } satisfies ProjectStageDto,
      ],
    }));
    const { state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '第二阶段';
    state.stageForm.startDate = shiftDate(today, -3);
    const save = state.saveStage();
    await flushPromises();
    state.cancelPreviousStageClose();
    await save;
    expect(mocks.update).not.toHaveBeenCalled();
    expect(mocks.create).not.toHaveBeenCalled();
    expect(state.stageDialogVisible.value).toBe(true);
    expect(state.stageForm.name).toBe('第二阶段');
    expect(state.savingStage.value).toBe(false);
  });

  it('上一阶段缺少测试结果时提示且不调用接口', async () => {
    const today = businessDateText();
    mocks.get.mockImplementation(async (id: number) => ({
      ...workspace(id),
      stages: [
        {
          endDate: null,
          id: 7,
          name: '第一阶段',
          projectId: id,
          rowVersion: 3,
          sequence: 1,
          startDate: shiftDate(today, -10),
          status: 'in_progress',
          testContent: '已填写内容',
          testResult: '',
          updatedAt: '2026-10-01T00:00:00Z',
        } satisfies ProjectStageDto,
      ],
    }));
    const { state } = setup();
    await flushPromises();
    state.openCreateStage();
    state.stageForm.name = '第二阶段';
    state.stageForm.startDate = shiftDate(today, -3);
    await state.saveStage();
    expect(mocks.warning).toHaveBeenCalledWith(
      '第1阶段「第一阶段」尚未填写测试内容或测试结果，请先补齐',
    );
    expect(mocks.update).not.toHaveBeenCalled();
    expect(mocks.create).not.toHaveBeenCalled();
    expect(state.stageDialogVisible.value).toBe(true);
    expect(state.stageForm.name).toBe('第二阶段');
    expect(state.previousStageClose.value).toBeNull();
  });
});
