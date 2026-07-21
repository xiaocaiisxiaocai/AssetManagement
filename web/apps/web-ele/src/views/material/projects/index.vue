<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';
import type {
  DeleteStatus,
  FlatOption,
  OptionKind,
  ProjectFormState,
  ProjectProgressFormState,
} from './project-workspace-types';

import type { DepartmentOptionNode, LocationNode } from '#/api/base-data';
import type {
  MaterialDetail,
  MaterialFlowItem,
  MaterialItem,
  MaterialQuery,
  MaterialStatus,
} from '#/api/material';
import type {
  SaveTestProjectOptionPayload,
  SaveTestProjectPayload,
  TestProjectFollowup,
  TestProjectItem,
  TestProjectOption,
} from '#/api/test-project';
import type { UserDto, UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { ElDrawer, ElMessage, ElMessageBox, ElTabs, ElTag } from 'element-plus';

import { getDepartmentOptionsApi, getLocationTreeApi } from '#/api/base-data';
import {
  approveFlowApi,
  deleteMaterialApi,
  getMaterialDetailApi,
  listHandledFlowsPageApi,
  listMaterialsApi,
  listMyFlowsPageApi,
  listPendingFlowsPageApi,
  purgeMaterialApi,
  rejectFlowApi,
  restoreMaterialApi,
  returnMaterialApi,
  withdrawMaterialFlowApi,
} from '#/api/material';
import {
  createTestProjectApi,
  createTestProjectFollowupApi,
  createTestProjectOptionApi,
  deleteTestProjectApi,
  deleteTestProjectFollowupApi,
  deleteTestProjectOptionApi,
  listTestProjectFollowupsApi,
  listTestProjectOptionsApi,
  listTestProjectsPageApi,
  purgeTestProjectApi,
  restoreTestProjectApi,
  updateTestProjectApi,
  updateTestProjectProgressApi,
  updateTestProjectFollowupApi,
  updateTestProjectOptionApi,
} from '#/api/test-project';
import { getUserListApi, getUserOptionsPageApi } from '#/api/user';
import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';
import { formatDate } from '#/utils/date-format';
import { flattenActiveDepartments } from '#/utils/department-options';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { normalizedPage } from '#/utils/pagination';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import {
  mergeSelectedUserOption,
  mergeUserOptions,
} from '#/utils/user-options';
import { formatWorkflowNode } from '#/utils/workflow-action-nodes';
import {
  buildMaterialActionAccess,
  buildProjectActionAccess,
} from '#/views/permissions/action-access';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';
import TransferDialog from '../components/TransferDialog.vue';
import { validateProjectForm } from './project-form-rules';
import { buildProjectPageQuery } from './project-page-query';
import { projectFollowUpStatusMeta } from './project-workspace-rules';
import ProjectFlowsTab from './ProjectFlowsTab.vue';
import ProjectFollowupsTab from './ProjectFollowupsTab.vue';
import ProjectFormDialog from './ProjectFormDialog.vue';
import ProjectMaterialsTab from './ProjectMaterialsTab.vue';
import ProjectOptionDialog from './ProjectOptionDialog.vue';
import ProjectProgressDialog from './ProjectProgressDialog.vue';
import ProjectTable from './ProjectTable.vue';

defineOptions({ name: 'MaterialProjects' });

function reactiveObjectModel<T extends object>(state: T) {
  return computed<T>({
    get: () => state,
    set: (value) => Object.assign(state, value),
  });
}

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const currentProject = ref<null | TestProjectItem>(null);
const projectActionAccess = computed(() =>
  buildProjectActionAccess(hasAccessByCodes),
);
const materialActionAccess = computed(() =>
  buildMaterialActionAccess(hasAccessByCodes),
);
const isCurrentProjectReadOnly = computed(
  () => currentProject.value?.isDeleted === true,
);
const currentMaterialActionAccess = computed(() => {
  if (!isCurrentProjectReadOnly.value) return materialActionAccess.value;
  return Object.fromEntries(
    Object.keys(materialActionAccess.value).map((key) => [key, false]),
  ) as typeof materialActionAccess.value;
});
const isCurrentProjectOwner = computed(
  () =>
    !!currentProject.value?.ownerId &&
    String(currentProject.value.ownerId) ===
      String(userStore.userInfo?.userId ?? ''),
);
const currentUserId = computed(() => Number(userStore.userInfo?.userId ?? 0));
const isCurrentUserSupervisor = computed(() =>
  userStore.userRoles.includes('supervisor'),
);
const canWriteCurrentProjectMaterial = computed(
  () =>
    !isCurrentProjectReadOnly.value &&
    (materialActionAccess.value.canCreate || isCurrentProjectOwner.value),
);
const canEditCurrentProjectMaterial = computed(
  () =>
    !isCurrentProjectReadOnly.value &&
    (materialActionAccess.value.canEdit || isCurrentProjectOwner.value),
);

const loading = ref(false);
const projects = ref<TestProjectItem[]>([]);
const projectTotal = ref(0);
const deleteStatus = ref<DeleteStatus>('all');
const pageSizeOptions = ref(createPageSizeOptions(20));
const projectQuery = reactive({
  page: 1,
  pageSize: 20,
});
const projectFilter = reactive<ProjectFilter>({
  code: '',
  name: '',
  ownerId: undefined,
  progressCode: '',
  projectTypeCode: '',
});
const projectQueryModel = reactiveObjectModel(projectQuery);
const projectFilterModel = reactiveObjectModel(projectFilter);

const options = ref<TestProjectOption[]>([]);
const users = ref<UserOptionDto[]>([]);
const userOptionsLoading = ref(false);
const userOptionsRequestGuard = createLatestRequestGuard();
const departments = ref<DepartmentOptionNode[]>([]);
const locations = ref<LocationNode[]>([]);

const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const saving = ref(false);
const form = reactive<ProjectFormState>({
  closedDate: '',
  code: '',
  followUpIntervalDays: 14,
  name: '',
  ownerId: undefined,
  plannedFinishDate: '',
  progressCode: '',
  projectTypeCode: '',
  startDate: '',
  testStatus: '',
});
const formModel = reactiveObjectModel(form);

const progressDialogVisible = ref(false);
const progressEditingProject = ref<null | TestProjectItem>(null);
const progressSaving = ref(false);
const progressForm = reactive<ProjectProgressFormState>({
  closedDate: '',
  progressCode: '',
  testStatus: '',
});
const progressFormModel = reactiveObjectModel(progressForm);

const optionDialogVisible = ref(false);
const optionSaving = ref(false);
const optionEditingId = ref<null | number>(null);
const activeOptionKind = ref<OptionKind>('project_type');
const optionForm = reactive<SaveTestProjectOptionPayload>({
  code: '',
  isActive: true,
  kind: 'project_type',
  label: '',
  sort: 0,
});
const optionFormModel = reactiveObjectModel(optionForm);

const followupDrawerVisible = ref(false);
const activeProjectTab = ref('materials');
const followups = ref<TestProjectFollowup[]>([]);
const followupLoading = ref(false);
const followupSaving = ref(false);
const editingFollowupId = ref<null | number>(null);
const followupForm = reactive({
  content: '',
  dueDate: '',
});
const followupFormModel = reactiveObjectModel(followupForm);

const materialLoading = ref(false);
const materials = ref<MaterialItem[]>([]);
const materialTotal = ref(0);
const materialFormVisible = ref(false);
const editingMaterial = ref<MaterialItem | null>(null);
const materialDetailVisible = ref(false);
const materialDetailLoading = ref(false);
const materialDetail = ref<MaterialDetail | null>(null);
const followupRequestGuard = createLatestRequestGuard();
const projectListRequestGuard = createLatestRequestGuard();
const materialRequestGuard = createLatestRequestGuard();
const pendingFlowRequestGuard = createLatestRequestGuard();
const handledFlowRequestGuard = createLatestRequestGuard();
const myFlowRequestGuard = createLatestRequestGuard();
const materialDetailRequestGuard = createLatestRequestGuard();
const transferVisible = ref(false);
const workflowNodeSelector = ref<InstanceType<
  typeof WorkflowNodeSelectDialog
> | null>(null);
const transferMaterial = ref<MaterialItem | null>(null);
const materialQuery = reactive({
  deleteStatus: 'all' as DeleteStatus,
  materialNo: '',
  name: '',
  page: 1,
  pageSize: 10,
  status: undefined as MaterialStatus | undefined,
});
const materialQueryModel = reactiveObjectModel(materialQuery);
const materialPageSizeOptions = ref(createPageSizeOptions(20));

const flowActiveTab = ref('mine');
const pendingFlowLoading = ref(false);
const handledFlowLoading = ref(false);
const myFlowLoading = ref(false);
const pendingFlows = ref<MaterialFlowItem[]>([]);
const handledFlows = ref<MaterialFlowItem[]>([]);
const myFlows = ref<MaterialFlowItem[]>([]);
const pendingFlowTotal = ref(0);
const handledFlowTotal = ref(0);
const myFlowTotal = ref(0);
const pendingFlowQuery = reactive({
  page: 1,
  pageSize: 10,
});
const myFlowQuery = reactive({
  page: 1,
  pageSize: 10,
});
const handledFlowQuery = reactive({
  page: 1,
  pageSize: 10,
});
const pendingFlowQueryModel = reactiveObjectModel(pendingFlowQuery);
const handledFlowQueryModel = reactiveObjectModel(handledFlowQuery);
const myFlowQueryModel = reactiveObjectModel(myFlowQuery);
const flowPageSizeOptions = ref(createPageSizeOptions(20));

const projectTypeOptions = computed(() => activeOptions('project_type'));
const progressOptions = computed(() => activeOptions('project_progress'));
const displayedOptions = computed(() =>
  options.value.filter((item) => item.kind === activeOptionKind.value),
);
const activeDepartmentOptions = computed(() =>
  flattenActiveDepartments(departments.value),
);
const locationOptions = computed<FlatOption[]>(() =>
  locations.value.map((node) => ({ id: node.id, label: node.name })),
);
const currentProjectList = computed(() =>
  currentProject.value ? [currentProject.value] : projects.value,
);
const currentProjectCodeText = computed(
  () => currentProject.value?.code || '未设置编号',
);
const currentProjectTypeText = computed(
  () =>
    currentProject.value?.projectTypeLabel ||
    currentProject.value?.projectTypeCode ||
    '未分类',
);
const currentProjectProgressText = computed(
  () =>
    currentProject.value?.progressLabel ||
    currentProject.value?.progressCode ||
    '未设置进度',
);
const projectOwnerOptions = computed(() => {
  const ownerMap = new Map<number, { id: number; name: string }>();
  for (const user of users.value) {
    ownerMap.set(user.id, { id: user.id, name: user.name });
  }
  for (const project of projects.value) {
    if (project.ownerId && project.ownerName) {
      ownerMap.set(project.ownerId, {
        id: project.ownerId,
        name: project.ownerName,
      });
    }
  }
  return [...ownerMap.values()];
});
function activeOptions(kind: OptionKind) {
  return options.value.filter((item) => item.kind === kind && item.isActive);
}

function normalizeText(value?: null | string) {
  const text = value?.trim();
  return text || null;
}

function statusMeta(project: TestProjectItem) {
  return projectFollowUpStatusMeta(project);
}

function generateOptionCode(kind: OptionKind) {
  const prefix = kind === 'project_type' ? 'type' : 'progress';
  return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;
}

async function loadData() {
  const requestGeneration = projectListRequestGuard.next();
  loading.value = true;
  try {
    const result = await listTestProjectsPageApi(
      buildProjectPageQuery(
        projectFilter,
        deleteStatus.value,
        projectQuery.page,
        projectQuery.pageSize,
      ),
    );
    if (!projectListRequestGuard.isLatest(requestGeneration)) return;
    const validPage = normalizedPage(
      projectQuery.page,
      result.total,
      projectQuery.pageSize,
    );
    if (projectQuery.page !== validPage) {
      projectQuery.page = validPage;
      await loadData();
      return;
    }
    projects.value = result.items;
    projectTotal.value = result.total;
    if (currentProject.value) {
      const refreshed = projects.value.find(
        (project) => project.id === currentProject.value?.id,
      );
      if (refreshed) currentProject.value = refreshed;
    }
  } finally {
    if (projectListRequestGuard.isLatest(requestGeneration))
      loading.value = false;
  }
}

function searchProjects() {
  projectQuery.page = 1;
  runHandled(loadData());
}

function resetProjectFilter() {
  Object.assign(projectFilter, {
    code: '',
    name: '',
    ownerId: undefined,
    progressCode: '',
    projectTypeCode: '',
  });
  projectQuery.page = 1;
  runHandled(loadData());
}

async function loadOptions() {
  options.value = await listTestProjectOptionsApi();
}

async function loadUsers() {
  if (users.value.length > 0) return;
  await searchUsers('');
}

async function searchUsers(keyword = '') {
  const requestGeneration = userOptionsRequestGuard.next();
  const canUseBusinessOptions =
    hasAccessByCodes(['approval:create']) ||
    hasAccessByCodes(['material-flow:transfer']) ||
    hasAccessByCodes(['project:view']) ||
    hasAccessByCodes(['project:create']) ||
    hasAccessByCodes(['project:edit']) ||
    hasAccessByCodes(['material:create']) ||
    hasAccessByCodes(['material:edit']);
  userOptionsLoading.value = true;
  try {
    let incoming: (UserDto | UserOptionDto)[] = [];
    if (canUseBusinessOptions) {
      const result = await getUserOptionsPageApi(keyword, 1, 50);
      incoming = result.items;
    } else if (hasAccessByCodes(['user:view'])) {
      const result = await getUserListApi(keyword, 1, 50);
      incoming = result.items.filter((user) => user.isActive);
    }
    if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
    users.value = mergeUserOptions(users.value, incoming);
  } catch {
    // 请求层已提示，保留已回填选项。
  } finally {
    if (userOptionsRequestGuard.isLatest(requestGeneration))
      userOptionsLoading.value = false;
  }
}

async function loadBaseOptions() {
  const [departmentTree, locationTree] = await Promise.allSettled([
    getDepartmentOptionsApi(),
    hasAccessByCodes(['location:view'])
      ? getLocationTreeApi()
      : Promise.resolve([]),
  ]);
  if (departmentTree.status === 'fulfilled')
    departments.value = departmentTree.value;
  if (locationTree.status === 'fulfilled') locations.value = locationTree.value;
}

function resetProjectForm() {
  Object.assign(form, {
    closedDate: '',
    code: '',
    followUpIntervalDays: 14,
    name: '',
    ownerId: undefined,
    plannedFinishDate: '',
    progressCode: '',
    projectTypeCode: '',
    startDate: '',
    testStatus: '',
  });
}

function openCreate() {
  editingId.value = null;
  resetProjectForm();
  dialogVisible.value = true;
  runHandled(Promise.all([loadUsers(), loadBaseOptions()]));
}

function openEdit(row: TestProjectItem) {
  users.value = mergeSelectedUserOption(users.value, {
    id: row.ownerId,
    name: row.ownerName,
  });
  editingId.value = row.id;
  Object.assign(form, {
    closedDate: row.closedDate ? row.closedDate.slice(0, 10) : '',
    code: row.code ?? '',
    followUpIntervalDays: row.followUpIntervalDays || 14,
    name: row.name,
    ownerId: row.ownerId ?? undefined,
    plannedFinishDate: row.plannedFinishDate
      ? row.plannedFinishDate.slice(0, 10)
      : '',
    progressCode: row.progressCode ?? '',
    projectTypeCode: row.projectTypeCode ?? '',
    startDate: row.startDate ? row.startDate.slice(0, 10) : '',
    testStatus: row.testStatus ?? '',
  });
  dialogVisible.value = true;
  runHandled(Promise.all([loadUsers(), loadBaseOptions()]));
}

function openProgress(row: TestProjectItem) {
  progressEditingProject.value = row;
  Object.assign(progressForm, {
    closedDate: row.closedDate ? row.closedDate.slice(0, 10) : '',
    progressCode: row.progressCode ?? '',
    testStatus: row.testStatus ?? '',
  });
  progressDialogVisible.value = true;
}

function buildProjectPayload(): SaveTestProjectPayload {
  return {
    closedDate: form.closedDate || null,
    code: normalizeText(form.code),
    followUpIntervalDays: form.followUpIntervalDays || 14,
    name: form.name.trim(),
    ownerId: form.ownerId ?? null,
    plannedFinishDate: form.plannedFinishDate || null,
    progressCode: form.progressCode || null,
    projectTypeCode: form.projectTypeCode || null,
    startDate: form.startDate || null,
    testStatus: normalizeText(form.testStatus),
  };
}

async function save() {
  const validationMessage = validateProjectForm(form);
  if (validationMessage) {
    ElMessage.warning(validationMessage);
    return;
  }
  if (saving.value) return;
  saving.value = true;
  try {
    const payload = buildProjectPayload();
    await (editingId.value
      ? updateTestProjectApi(editingId.value, payload)
      : createTestProjectApi(payload));
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    saving.value = false;
  }
}

async function saveProgress() {
  const project = progressEditingProject.value;
  if (!project) return;
  if (!progressForm.progressCode) {
    ElMessage.warning('请选择项目进度');
    return;
  }
  if (progressForm.progressCode === 'closed' && !progressForm.closedDate) {
    ElMessage.warning('已结案项目必须填写结案时间');
    return;
  }
  if (
    progressForm.closedDate &&
    project.startDate &&
    progressForm.closedDate < project.startDate.slice(0, 10)
  ) {
    ElMessage.warning('结案时间不能早于开始时间');
    return;
  }
  if (progressSaving.value) return;
  progressSaving.value = true;
  try {
    await updateTestProjectProgressApi(project.id, {
      closedDate: progressForm.closedDate || null,
      progressCode: progressForm.progressCode,
      testStatus: normalizeText(progressForm.testStatus),
    });
    ElMessage.success('项目进展已更新');
    progressDialogVisible.value = false;
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    progressSaving.value = false;
  }
}

async function remove(row: TestProjectItem) {
  try {
    await ElMessageBox.confirm(`确认删除项目「${row.name}」？`, '删除确认', {
      type: 'warning',
    });
  } catch {
    return;
  }
  try {
    await deleteTestProjectApi(row.id);
    ElMessage.success('已删除');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function restore(row: TestProjectItem) {
  try {
    await ElMessageBox.confirm(
      `确认撤销删除项目「${row.name}」？`,
      '撤销删除',
      {
        type: 'warning',
      },
    );
  } catch {
    return;
  }
  try {
    await restoreTestProjectApi(row.id);
    ElMessage.success('已恢复');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function purge(row: TestProjectItem) {
  try {
    await ElMessageBox.confirm(
      `彻底删除项目「${row.name}」后不可恢复，确认继续？`,
      '彻底删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await purgeTestProjectApi(row.id);
    ElMessage.success('已彻底删除');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

function resetOptionForm(kind: OptionKind = activeOptionKind.value) {
  Object.assign(optionForm, {
    code: '',
    isActive: true,
    kind,
    label: '',
    sort: 0,
  });
  optionEditingId.value = null;
}

function openOptionDialog() {
  resetOptionForm();
  optionDialogVisible.value = true;
}

function openOptionCreate(kind: OptionKind) {
  activeOptionKind.value = kind;
  resetOptionForm(kind);
}

function openOptionEdit(row: TestProjectOption) {
  activeOptionKind.value = row.kind;
  optionEditingId.value = row.id;
  Object.assign(optionForm, {
    code: row.code,
    isActive: row.isActive,
    kind: row.kind,
    label: row.label,
    sort: row.sort,
  });
}

async function saveOption() {
  if (!optionForm.label.trim()) {
    ElMessage.warning('请填写名称');
    return;
  }
  optionSaving.value = true;
  try {
    const payload: SaveTestProjectOptionPayload = {
      code: optionForm.code.trim() || generateOptionCode(optionForm.kind),
      isActive: optionForm.isActive,
      kind: optionForm.kind,
      label: optionForm.label.trim(),
      sort: optionForm.sort || 0,
    };
    await (optionEditingId.value
      ? updateTestProjectOptionApi(optionEditingId.value, payload)
      : createTestProjectOptionApi(payload));
    ElMessage.success('配置已保存');
    resetOptionForm(optionForm.kind);
    await loadOptions();
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    optionSaving.value = false;
  }
}

async function removeOption(row: TestProjectOption) {
  try {
    await ElMessageBox.confirm(
      `确认删除配置「${row.label}」？正在使用的配置不能删除。`,
      '删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await deleteTestProjectOptionApi(row.id);
    ElMessage.success('配置已删除');
    if (optionEditingId.value === row.id) resetOptionForm(row.kind);
    await loadOptions();
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function openFollowups(row: TestProjectItem) {
  followupRequestGuard.invalidate();
  materialRequestGuard.invalidate();
  pendingFlowRequestGuard.invalidate();
  handledFlowRequestGuard.invalidate();
  myFlowRequestGuard.invalidate();
  currentProject.value = row;
  pendingFlows.value = [];
  handledFlows.value = [];
  myFlows.value = [];
  flowActiveTab.value = currentMaterialActionAccess.value.canApprove
    ? 'pending'
    : 'mine';
  pendingFlowQuery.page = 1;
  handledFlowQuery.page = 1;
  myFlowQuery.page = 1;
  resetMaterialQuery();
  activeProjectTab.value = 'materials';
  editingFollowupId.value = null;
  Object.assign(followupForm, {
    content: '',
    dueDate: row.nextFollowUpDueDate
      ? row.nextFollowUpDueDate.slice(0, 10)
      : '',
  });
  followupDrawerVisible.value = true;
  await Promise.allSettled([
    loadFollowups(row.id),
    loadProjectMaterials(row.id),
  ]);
}

async function loadFollowups(projectId: number) {
  const requestGeneration = followupRequestGuard.next();
  followupLoading.value = true;
  try {
    const response = await listTestProjectFollowupsApi(projectId);
    if (
      followupRequestGuard.isLatest(requestGeneration) &&
      followupDrawerVisible.value &&
      currentProject.value?.id === projectId
    ) {
      followups.value = response;
    }
  } finally {
    if (followupRequestGuard.isLatest(requestGeneration)) {
      followupLoading.value = false;
    }
  }
}

function editFollowup(row: TestProjectFollowup) {
  editingFollowupId.value = row.id;
  Object.assign(followupForm, {
    content: row.content,
    dueDate: row.dueDate ? row.dueDate.slice(0, 10) : '',
  });
}

function cancelFollowupEdit() {
  editingFollowupId.value = null;
  Object.assign(followupForm, {
    content: '',
    dueDate: currentProject.value?.nextFollowUpDueDate
      ? currentProject.value.nextFollowUpDueDate.slice(0, 10)
      : '',
  });
}

async function saveFollowup() {
  const project = currentProject.value;
  if (!project) return;
  if (project.isDeleted || !project.canWriteFollowUp) {
    ElMessage.warning('项目进入落地跟进后，负责人或管理员才能填写');
    return;
  }
  if (!followupForm.content.trim()) {
    ElMessage.warning('请填写落地情况');
    return;
  }
  followupSaving.value = true;
  try {
    const payload = {
      content: followupForm.content.trim(),
      dueDate: followupForm.dueDate || null,
    };
    await (editingFollowupId.value
      ? updateTestProjectFollowupApi(
          project.id,
          editingFollowupId.value,
          payload,
        )
      : createTestProjectFollowupApi(project.id, payload));
    ElMessage.success('跟进已保存');
    await Promise.all([loadFollowups(project.id), loadData()]);
    cancelFollowupEdit();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    followupSaving.value = false;
  }
}

async function deleteFollowup(row: TestProjectFollowup) {
  const project = currentProject.value;
  if (!project) return;
  if (project.isDeleted || !project.canWriteFollowUp) {
    ElMessage.warning('当前项目为只读状态');
    return;
  }
  try {
    await ElMessageBox.confirm('确认删除该跟进记录？', '删除确认', {
      type: 'warning',
    });
  } catch {
    return;
  }
  try {
    await deleteTestProjectFollowupApi(project.id, row.id);
    ElMessage.success('已删除');
    await Promise.all([loadFollowups(project.id), loadData()]);
    if (editingFollowupId.value === row.id) cancelFollowupEdit();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

function buildMaterialQuery(projectId: number): MaterialQuery {
  return {
    deleteStatus: materialQuery.deleteStatus,
    materialNo: materialQuery.materialNo || undefined,
    name: materialQuery.name || undefined,
    page: materialQuery.page,
    pageSize: materialQuery.pageSize,
    projectId,
    status: materialQuery.status,
  };
}

async function loadProjectMaterials(projectId = currentProject.value?.id) {
  if (!projectId) return;
  const requestGeneration = materialRequestGuard.next();
  materialLoading.value = true;
  try {
    const result = await listMaterialsApi(buildMaterialQuery(projectId));
    if (
      materialRequestGuard.isLatest(requestGeneration) &&
      followupDrawerVisible.value &&
      currentProject.value?.id === projectId
    ) {
      const validPage = normalizedPage(
        materialQuery.page,
        result.total,
        materialQuery.pageSize,
      );
      if (materialQuery.page !== validPage) {
        materialQuery.page = validPage;
        await loadProjectMaterials(projectId);
        return;
      }
      materials.value = result.items;
      materialTotal.value = result.total;
    }
  } finally {
    if (materialRequestGuard.isLatest(requestGeneration)) {
      materialLoading.value = false;
    }
  }
}

function searchMaterials() {
  materialQuery.page = 1;
  runHandled(loadProjectMaterials());
}

function resetMaterialQuery() {
  Object.assign(materialQuery, {
    deleteStatus: 'all',
    materialNo: '',
    name: '',
    page: 1,
    status: undefined,
  });
  runHandled(loadProjectMaterials());
}

function onProjectPageSizeChange() {
  projectQuery.page = 1;
  runHandled(loadData());
}

function onProjectDeleteStatusChange() {
  projectQuery.page = 1;
  runHandled(loadData());
}

function onMaterialPageSizeChange() {
  materialQuery.page = 1;
  runHandled(loadProjectMaterials());
}

function loadMaterialFormOptions() {
  runHandled(Promise.all([loadUsers(), loadBaseOptions()]));
}

function openCreateMaterial() {
  if (isCurrentProjectReadOnly.value) return;
  editingMaterial.value = null;
  loadMaterialFormOptions();
  materialFormVisible.value = true;
}

function onMaterialRowCommand(command: number | string, row: MaterialItem) {
  if (isCurrentProjectReadOnly.value) return;
  switch (command) {
    case 'delete': {
      runHandled(removeMaterial(row));
      break;
    }
    case 'purge': {
      runHandled(purgeMaterial(row));
      break;
    }
    case 'restore': {
      runHandled(restoreMaterial(row));
      break;
    }
    case 'return': {
      runHandled(onReturnMaterial(row));
      break;
    }
    case 'transfer': {
      openTransfer(row);
      break;
    }
    // no default
  }
}

function openEditMaterial(row: MaterialItem) {
  if (isCurrentProjectReadOnly.value) return;
  users.value = mergeSelectedUserOption(users.value, {
    id: row.custodianId,
    name: row.custodianName,
  });
  editingMaterial.value = row;
  loadMaterialFormOptions();
  materialFormVisible.value = true;
}

async function openMaterialDetail(row: MaterialItem) {
  const requestGeneration = materialDetailRequestGuard.next();
  materialDetailVisible.value = true;
  materialDetailLoading.value = true;
  materialDetail.value = null;
  try {
    const response = await getMaterialDetailApi(row.id);
    if (
      materialDetailRequestGuard.isLatest(requestGeneration) &&
      materialDetailVisible.value
    ) {
      materialDetail.value = response;
    }
  } finally {
    if (materialDetailRequestGuard.isLatest(requestGeneration)) {
      materialDetailLoading.value = false;
    }
  }
}

function openTransfer(row: MaterialItem) {
  if (isCurrentProjectReadOnly.value) return;
  transferMaterial.value = row;
  transferVisible.value = true;
}

async function onReturnMaterial(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `确认将料件「${row.name}」标记为已退回厂商？`,
      '退回厂商',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await returnMaterialApi(row.id);
    ElMessage.success('已标记为退回厂商');
    await afterMaterialChanged();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function removeMaterial(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `确认删除料件「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`,
      '删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await deleteMaterialApi(row.id);
    ElMessage.success('已删除');
    await afterMaterialChanged();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function restoreMaterial(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `确认撤销删除料件「${row.name}」？`,
      '撤销删除',
      {
        type: 'warning',
      },
    );
  } catch {
    return;
  }
  try {
    await restoreMaterialApi(row.id);
    ElMessage.success('已恢复');
    await afterMaterialChanged();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function purgeMaterial(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `彻底删除料件「${row.name}」后不可恢复，确认继续？`,
      '彻底删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await purgeMaterialApi(row.id);
    ElMessage.success('已彻底删除');
    await afterMaterialChanged();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function afterMaterialChanged() {
  await Promise.all([loadProjectMaterials(), loadProjectFlows()]);
}

async function loadProjectFlows(projectId = currentProject.value?.id) {
  if (!projectId) return;
  const loadMine = flowActiveTab.value === 'mine';
  const loadHandled = flowActiveTab.value === 'handled';
  if (!loadMine && !materialActionAccess.value.canApprove) return;
  if (loadMine) myFlowLoading.value = true;
  else if (loadHandled) handledFlowLoading.value = true;
  else pendingFlowLoading.value = true;
  const requestGuard = loadMine
    ? myFlowRequestGuard
    : loadHandled
      ? handledFlowRequestGuard
      : pendingFlowRequestGuard;
  const requestGeneration = requestGuard.next();
  try {
    const query = loadMine
      ? myFlowQuery
      : loadHandled
        ? handledFlowQuery
        : pendingFlowQuery;
    const result = await (loadMine
      ? listMyFlowsPageApi({ ...query, projectId })
      : loadHandled
        ? listHandledFlowsPageApi({ ...query, projectId })
        : listPendingFlowsPageApi({ ...query, projectId }));
    if (
      !requestGuard.isLatest(requestGeneration) ||
      !followupDrawerVisible.value ||
      currentProject.value?.id !== projectId
    ) {
      return;
    }
    const lastPage = Math.max(1, Math.ceil(result.total / query.pageSize));
    if (query.page > lastPage) {
      query.page = lastPage;
      await loadProjectFlows(projectId);
      return;
    }
    if (loadMine) {
      myFlows.value = result.items;
      myFlowTotal.value = result.total;
    } else if (loadHandled) {
      handledFlows.value = result.items;
      handledFlowTotal.value = result.total;
    } else {
      pendingFlows.value = result.items;
      pendingFlowTotal.value = result.total;
    }
  } finally {
    if (requestGuard.isLatest(requestGeneration)) {
      if (loadMine) myFlowLoading.value = false;
      else if (loadHandled) handledFlowLoading.value = false;
      else pendingFlowLoading.value = false;
    }
  }
}

async function approveFlow(row: MaterialFlowItem) {
  if (isCurrentProjectReadOnly.value) return;
  const node = await workflowNodeSelector.value?.selectNode(row, '通过');
  if (!node) return;
  try {
    await ElMessageBox.confirm(
      `确认通过料件「${row.materialName}」的流转申请？处理节点：${formatWorkflowNode(node)}`,
      '审批通过',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await approveFlowApi(row.id, '同意', node.id);
    ElMessage.success('已通过');
    await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function rejectFlow(row: MaterialFlowItem) {
  if (isCurrentProjectReadOnly.value) return;
  const node = await workflowNodeSelector.value?.selectNode(row, '驳回');
  if (!node) return;
  let reason = '不同意';
  try {
    const result = await ElMessageBox.prompt(
      `请输入驳回原因。处理节点：${formatWorkflowNode(node)}`,
      '驳回',
      { inputPlaceholder: '驳回原因' },
    );
    reason = result.value || reason;
  } catch {
    return;
  }
  try {
    await rejectFlowApi(row.id, reason, node.id);
    ElMessage.success('已驳回');
    await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function withdrawFlow(row: MaterialFlowItem) {
  if (isCurrentProjectReadOnly.value || row.canWithdraw !== true) return;
  try {
    await ElMessageBox.confirm(
      `确认撤回料件「${row.materialName}」的流转申请？`,
      '撤回确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await withdrawMaterialFlowApi(row.id);
    ElMessage.success('已撤回');
    await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

function onProjectTabChange(name: number | string) {
  if (name === 'materials') {
    runHandled(loadProjectMaterials());
  }
  if (name === 'flows') {
    runHandled(loadProjectFlows());
  }
}

function onFlowTabChange() {
  runHandled(loadProjectFlows());
}

function onPendingFlowPageSizeChange() {
  pendingFlowQuery.page = 1;
  runHandled(loadProjectFlows());
}

function onHandledFlowPageSizeChange() {
  handledFlowQuery.page = 1;
  runHandled(loadProjectFlows());
}

function onMyFlowPageSizeChange() {
  myFlowQuery.page = 1;
  runHandled(loadProjectFlows());
}

onMounted(async () => {
  const defaultPageSize = await getDefaultPageSize();
  projectQuery.pageSize = defaultPageSize;
  materialQuery.pageSize = defaultPageSize;
  pendingFlowQuery.pageSize = defaultPageSize;
  handledFlowQuery.pageSize = defaultPageSize;
  myFlowQuery.pageSize = defaultPageSize;
  pageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  materialPageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  flowPageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  await Promise.all([loadOptions(), loadData()]);
});

watch(followupDrawerVisible, (opened) => {
  if (opened) return;
  followupRequestGuard.invalidate();
  materialRequestGuard.invalidate();
  pendingFlowRequestGuard.invalidate();
  handledFlowRequestGuard.invalidate();
  myFlowRequestGuard.invalidate();
  followupLoading.value = false;
  materialLoading.value = false;
  pendingFlowLoading.value = false;
  handledFlowLoading.value = false;
  myFlowLoading.value = false;
});

watch(materialDetailVisible, (opened) => {
  if (opened) return;
  materialDetailRequestGuard.invalidate();
  materialDetailLoading.value = false;
  materialDetail.value = null;
});
</script>

<template>
  <re-page>
    <div class="material-projects-page p-5">
      <ProjectTable
        v-model:delete-status="deleteStatus"
        v-model:filter="projectFilterModel"
        v-model:query="projectQueryModel"
        :access="projectActionAccess"
        :current-user-id="currentUserId"
        :filtered-total="projectTotal"
        :loading="loading"
        :owner-options="projectOwnerOptions"
        :page-size-options="pageSizeOptions"
        :paged-projects="projects"
        :progress-options="progressOptions"
        :project-type-options="projectTypeOptions"
        :user-options-loading="userOptionsLoading"
        @create="openCreate"
        @edit="openEdit"
        @open="openFollowups"
        @options="openOptionDialog"
        @page-change="loadData"
        @page-size-change="onProjectPageSizeChange"
        @purge="purge"
        @progress="openProgress"
        @remove="remove"
        @reset="resetProjectFilter"
        @restore="restore"
        @search="searchProjects"
        @status-change="onProjectDeleteStatusChange"
        @user-search="searchUsers"
      />

      <ProjectFormDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :editing="editingId !== null"
        :progress-options="progressOptions"
        :project-type-options="projectTypeOptions"
        :saving="saving"
        :search-users="searchUsers"
        :user-options-loading="userOptionsLoading"
        :users="users"
        @save="save"
      />

      <ProjectProgressDialog
        v-model:form="progressFormModel"
        v-model:visible="progressDialogVisible"
        :progress-options="progressOptions"
        :saving="progressSaving"
        @save="saveProgress"
      />

      <ProjectOptionDialog
        v-model:active-kind="activeOptionKind"
        v-model:form="optionFormModel"
        v-model:visible="optionDialogVisible"
        :can-manage="projectActionAccess.canOption"
        :displayed-options="displayedOptions"
        :editing-id="optionEditingId"
        :saving="optionSaving"
        @edit="openOptionEdit"
        @remove="removeOption"
        @reset="openOptionCreate"
        @save="saveOption"
      />

      <ElDrawer v-model="followupDrawerVisible" size="78%" title="项目跟进">
        <div v-if="currentProject" class="followup-panel">
          <section class="project-brief">
            <div class="project-brief-main">
              <div class="project-kicker">项目跟进工作台</div>
              <h2 class="project-title">{{ currentProject.name }}</h2>
              <div class="project-meta-line">
                <span>{{ currentProjectCodeText }}</span>
                <span>{{ currentProjectTypeText }}</span>
                <span>{{ currentProjectProgressText }}</span>
              </div>
            </div>
            <div class="project-brief-stats">
              <div class="brief-stat">
                <span class="brief-stat-label">负责人</span>
                <strong>{{ currentProject.ownerName || '-' }}</strong>
              </div>
              <div class="brief-stat">
                <span class="brief-stat-label">下次跟进</span>
                <div class="brief-stat-value">
                  <strong>{{
                    formatDate(currentProject.nextFollowUpDueDate)
                  }}</strong>
                  <ElTag :type="statusMeta(currentProject).type" size="small">
                    {{ statusMeta(currentProject).label }}
                  </ElTag>
                </div>
              </div>
              <div class="brief-stat">
                <span class="brief-stat-label">跟进间隔</span>
                <strong>{{ currentProject.followUpIntervalDays }} 天</strong>
              </div>
              <div class="brief-stat">
                <span class="brief-stat-label">料件数</span>
                <strong>{{ currentProject.materialCount }} 件</strong>
              </div>
            </div>
          </section>

          <ElTabs
            v-model="activeProjectTab"
            class="project-work-tabs"
            @tab-change="onProjectTabChange"
          >
            <ProjectMaterialsTab
              v-model:query="materialQueryModel"
              :access="currentMaterialActionAccess"
              :can-create="canWriteCurrentProjectMaterial"
              :can-edit="canEditCurrentProjectMaterial"
              :current-user-id="currentUserId"
              :is-supervisor="isCurrentUserSupervisor"
              :loading="materialLoading"
              :materials="materials"
              :page-size-options="materialPageSizeOptions"
              :project-owner-id="currentProject.ownerId"
              :total="materialTotal"
              @command="onMaterialRowCommand"
              @create="openCreateMaterial"
              @detail="openMaterialDetail"
              @edit="openEditMaterial"
              @page-change="() => loadProjectMaterials(currentProject?.id)"
              @page-size-change="onMaterialPageSizeChange"
              @reset="resetMaterialQuery"
              @search="searchMaterials"
            />

            <ProjectFlowsTab
              v-model:active-tab="flowActiveTab"
              v-model:handled-query="handledFlowQueryModel"
              v-model:my-query="myFlowQueryModel"
              v-model:pending-query="pendingFlowQueryModel"
              :can-approve="currentMaterialActionAccess.canApprove"
              :handled-count="handledFlowTotal"
              :handled-flows="handledFlows"
              :handled-loading="handledFlowLoading"
              :my-count="myFlowTotal"
              :my-flows="myFlows"
              :my-loading="myFlowLoading"
              :page-size-options="flowPageSizeOptions"
              :pending-count="pendingFlowTotal"
              :pending-flows="pendingFlows"
              :pending-loading="pendingFlowLoading"
              :read-only="isCurrentProjectReadOnly"
              @approve="approveFlow"
              @handled-page-size-change="onHandledFlowPageSizeChange"
              @my-page-size-change="onMyFlowPageSizeChange"
              @page-change="loadProjectFlows"
              @pending-page-size-change="onPendingFlowPageSizeChange"
              @reject="rejectFlow"
              @tab-change="onFlowTabChange"
              @withdraw="withdrawFlow"
            />

            <ProjectFollowupsTab
              v-model:form="followupFormModel"
              :editing-id="editingFollowupId"
              :followups="followups"
              :loading="followupLoading"
              :project="currentProject"
              :saving="followupSaving"
              @cancel-edit="cancelFollowupEdit"
              @edit="editFollowup"
              @remove="deleteFollowup"
              @save="saveFollowup"
            />
          </ElTabs>
        </div>
      </ElDrawer>

      <MaterialFormDialog
        v-model:visible="materialFormVisible"
        :default-project-id="currentProject?.id"
        :department-options="activeDepartmentOptions"
        :location-options="locationOptions"
        :material="editingMaterial"
        :project-locked="true"
        :projects="currentProjectList"
        :search-users="searchUsers"
        :user-options-loading="userOptionsLoading"
        :users="users"
        @saved="afterMaterialChanged"
      />

      <MaterialDetailDialog
        v-model:visible="materialDetailVisible"
        :detail="materialDetail"
        :loading="materialDetailLoading"
      />

      <TransferDialog
        v-model:visible="transferVisible"
        :material="transferMaterial"
        :search-users="searchUsers"
        :user-options-loading="userOptionsLoading"
        :users="users"
        @done="afterMaterialChanged"
      />

      <WorkflowNodeSelectDialog ref="workflowNodeSelector" />
    </div>
  </re-page>
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 16px;
}

.material-projects-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.project-toolbar {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.project-toolbar-left,
.project-toolbar-right {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
}

.project-toolbar-right {
  justify-content: flex-end;
}

.project-table-panel {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.project-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.option-editor {
  padding: 12px;
  margin-bottom: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.option-form-grid {
  display: grid;
  grid-template-columns: 1.4fr 0.8fr 0.6fr;
  column-gap: 12px;
}

.option-actions,
.followup-actions {
  display: flex;
  flex-shrink: 0;
  justify-content: flex-end;
  gap: 8px;
}

.followup-actions {
  margin-top: 12px;
}

.table-sub-text {
  margin-top: 2px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.followup-panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
  height: 100%;
  min-height: 0;
}

:deep(.el-drawer__body) {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  padding-bottom: 16px;
}

:deep(.el-drawer__body > *) {
  flex: 1;
  min-height: 0;
}

.project-brief {
  display: grid;
  grid-template-columns: minmax(260px, 1fr) minmax(520px, 1.4fr);
  gap: 16px;
  padding: 18px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  background: linear-gradient(
    180deg,
    var(--el-fill-color-blank),
    var(--el-fill-color-light)
  );
}

.project-brief-main {
  min-width: 0;
}

.project-kicker {
  margin-bottom: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-color-primary);
}

.project-title {
  margin: 0;
  overflow: hidden;
  color: var(--el-text-color-primary);
  font-size: 18px;
  font-weight: 650;
  line-height: 1.4;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-meta-line {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 16px;
  margin-top: 8px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.project-meta-line span:not(:last-child)::after {
  position: relative;
  left: 8px;
  color: var(--el-border-color);
  content: '/';
}

.project-brief-stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.brief-stat {
  min-width: 0;
  padding: 12px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-blank);
}

.brief-stat-label {
  display: block;
  margin-bottom: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.brief-stat strong {
  display: block;
  overflow: hidden;
  color: var(--el-text-color-primary);
  font-size: 15px;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.brief-stat-value {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.project-work-tabs {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.project-work-tabs :deep(.el-tabs__content) {
  order: 1;
  flex: 1;
  min-height: 0;
}

.project-work-tabs :deep(.el-tabs__header) {
  order: 0;
  flex-shrink: 0;
  margin-bottom: 12px;
}

.project-work-tabs :deep(.el-tab-pane) {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.tab-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 18px;
  padding: 0 6px;
  margin-left: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  background: var(--el-fill-color-light);
  border-radius: 999px;
}

.followup-workspace {
  display: grid;
  grid-template-columns: minmax(320px, 0.48fr) minmax(0, 1fr);
  gap: 16px;
  height: 100%;
  min-height: 0;
  align-items: stretch;
}

.followup-editor-panel,
.followup-history-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-blank);
}

.followup-editor-panel {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: sticky;
  top: 0;
}

.followup-editor-panel :deep(.el-form) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}

.followup-editor-panel :deep(.el-form-item:last-child) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}

.followup-editor-panel :deep(.el-form-item:last-child .el-form-item__content) {
  min-height: 0;
  flex: 1;
}

.followup-textarea {
  height: 100%;
}

.followup-textarea :deep(.el-textarea__inner) {
  height: 100%;
  max-height: 100%;
  resize: none;
}

.followup-history-panel {
  min-height: 0;
  overflow: auto;
}

.panel-heading {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 14px;
}

.panel-heading h3 {
  margin: 0;
  color: var(--el-text-color-primary);
  font-size: 15px;
  font-weight: 650;
}

.panel-heading p {
  margin: 4px 0 0;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.5;
}

.readonly-note {
  padding: 14px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
}

.compact-empty {
  padding: 54px 0;
}

.followup-timeline {
  padding-right: 6px;
}

.followup-record {
  padding: 12px 14px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-light);
}

.followup-record-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  margin-bottom: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.followup-content {
  color: var(--el-text-color-primary);
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
}

.record-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
}

.drawer-table-panel {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface);
}

.drawer-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.material-row-actions {
  display: flex;
  gap: 4px;
  align-items: center;
  justify-content: center;
}

.material-row-actions :deep(.el-button + .el-button) {
  margin-left: 0;
}

.material-table-panel,
.flow-table-panel {
  flex: 1;
  height: auto;
  min-height: 0;
}

.inner-flow-tabs {
  display: flex;
  flex: 1;
  height: auto;
  min-height: 0;
  flex-direction: column;
}

.inner-flow-tabs :deep(.el-tabs__content) {
  order: 1;
  flex: 1;
  min-height: 0;
}

.inner-flow-tabs :deep(.el-tabs__header) {
  order: 0;
  flex-shrink: 0;
  margin-bottom: 12px;
}

.inner-flow-tabs :deep(.el-tab-pane) {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.table-bottom-pager {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--asset-page-border);
  background: var(--asset-page-surface);
}

.table-bottom-pager-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  color: var(--asset-page-muted);
  font-size: 14px;
  line-height: 20px;
}

.table-bottom-pager-divider {
  color: var(--asset-page-border);
}

.material-filter {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  margin-bottom: 14px;
}

:deep(.project-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light) !important;
}

:deep(.material-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light) !important;
}

:deep(.material-row-returned td.el-table__cell) {
  color: var(--el-text-color-secondary);
}

@media (max-width: 768px) {
  .material-projects-page {
    min-height: 100%;
    overflow-y: auto;
  }

  :deep(.el-drawer) {
    width: 100% !important;
  }

  .form-grid,
  .option-form-grid,
  .project-brief,
  .project-brief-stats,
  .followup-workspace {
    grid-template-columns: 1fr;
  }

  .followup-editor-panel {
    position: static;
  }

  .project-toolbar {
    align-items: stretch;
  }

  .material-table-panel,
  .flow-table-panel,
  .inner-flow-tabs {
    height: auto;
  }
}
</style>
