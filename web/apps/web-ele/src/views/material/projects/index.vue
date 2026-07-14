<script lang="ts" setup>
import type {
  MaterialDetail,
  MaterialFlowItem,
  MaterialItem,
  MaterialQuery,
  MaterialStatus,
} from '#/api/material';
import type { DepartmentOptionNode, LocationNode } from '#/api/base-data';
import type {
  SaveTestProjectOptionPayload,
  SaveTestProjectPayload,
  TestProjectFollowup,
  TestProjectItem,
  TestProjectOption,
} from '#/api/test-project';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { ElDrawer, ElMessage, ElMessageBox, ElTabs, ElTag } from 'element-plus';

import { getDepartmentOptionsApi, getLocationTreeApi } from '#/api/base-data';
import { flattenActiveDepartments } from '#/utils/department-options';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { formatWorkflowNode } from '#/utils/workflow-action-nodes';
import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';
import {
  approveFlowApi,
  deleteMaterialApi,
  getMaterialDetailApi,
  listMaterialsApi,
  listMyFlowsApi,
  listPendingFlowsApi,
  purgeMaterialApi,
  rejectFlowApi,
  restoreMaterialApi,
  returnMaterialApi,
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
  listTestProjectsApi,
  purgeTestProjectApi,
  restoreTestProjectApi,
  updateTestProjectApi,
  updateTestProjectFollowupApi,
  updateTestProjectOptionApi,
} from '#/api/test-project';
import { getUserListApi, getUserOptionsApi } from '#/api/user';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';
import TransferDialog from '../components/TransferDialog.vue';
import ProjectFormDialog from './ProjectFormDialog.vue';
import ProjectFlowsTab from './ProjectFlowsTab.vue';
import ProjectFollowupsTab from './ProjectFollowupsTab.vue';
import ProjectMaterialsTab from './ProjectMaterialsTab.vue';
import ProjectOptionDialog from './ProjectOptionDialog.vue';
import ProjectTable from './ProjectTable.vue';
import type {
  DeleteStatus,
  FlatOption,
  OptionKind,
  ProjectFormState,
} from './project-workspace-types';
import {
  buildMaterialActionAccess,
  buildProjectActionAccess,
} from '#/views/permissions/action-access';
import { filterProjects, type ProjectFilter } from './project-filter';
import { validateProjectForm } from './project-form-rules';

defineOptions({ name: 'MaterialProjects' });

const followUpStatusMap: Record<
  string,
  { label: string; type: 'danger' | 'success' | 'warning' }
> = {
  due: { label: '今日到期', type: 'warning' },
  overdue: { label: '已超期', type: 'danger' },
  upcoming: { label: '未到期', type: 'success' },
};
const defaultFollowUpStatus: {
  label: string;
  type: 'danger' | 'success' | 'warning';
} = {
  label: '未到期',
  type: 'success',
};

const { hasAccessByCodes } = useAccess();
const userStore = useUserStore();
const projectActionAccess = computed(() =>
  buildProjectActionAccess(hasAccessByCodes),
);
const materialActionAccess = computed(() =>
  buildMaterialActionAccess(hasAccessByCodes),
);
const isCurrentProjectOwner = computed(
  () =>
    !!currentProject.value?.ownerId &&
    String(currentProject.value.ownerId) ===
      String(userStore.userInfo?.userId ?? ''),
);
const canWriteCurrentProjectMaterial = computed(
  () => materialActionAccess.value.canCreate || isCurrentProjectOwner.value,
);
const canEditCurrentProjectMaterial = computed(
  () => materialActionAccess.value.canEdit || isCurrentProjectOwner.value,
);

const loading = ref(false);
const projects = ref<TestProjectItem[]>([]);
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

const options = ref<TestProjectOption[]>([]);
const users = ref<UserOptionDto[]>([]);
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

const followupDrawerVisible = ref(false);
const currentProject = ref<null | TestProjectItem>(null);
const activeProjectTab = ref('materials');
const followups = ref<TestProjectFollowup[]>([]);
const followupLoading = ref(false);
const followupSaving = ref(false);
const editingFollowupId = ref<null | number>(null);
const followupForm = reactive({
  content: '',
  dueDate: '',
});

const materialLoading = ref(false);
const materials = ref<MaterialItem[]>([]);
const materialTotal = ref(0);
const materialFormVisible = ref(false);
const editingMaterial = ref<MaterialItem | null>(null);
const materialDetailVisible = ref(false);
const materialDetailLoading = ref(false);
const materialDetail = ref<MaterialDetail | null>(null);
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
const materialPageSizeOptions = ref(createPageSizeOptions(20));

const flowActiveTab = ref('mine');
const pendingFlowLoading = ref(false);
const myFlowLoading = ref(false);
const pendingFlows = ref<MaterialFlowItem[]>([]);
const myFlows = ref<MaterialFlowItem[]>([]);
const pendingFlowQuery = reactive({
  page: 1,
  pageSize: 10,
});
const myFlowQuery = reactive({
  page: 1,
  pageSize: 10,
});
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
const pendingFlowCount = computed(() => pendingFlows.value.length);
const myFlowCount = computed(() => myFlows.value.length);
const pagedPendingFlows = computed(() => {
  const start = (pendingFlowQuery.page - 1) * pendingFlowQuery.pageSize;
  return pendingFlows.value.slice(start, start + pendingFlowQuery.pageSize);
});
const pagedMyFlows = computed(() => {
  const start = (myFlowQuery.page - 1) * myFlowQuery.pageSize;
  return myFlows.value.slice(start, start + myFlowQuery.pageSize);
});
const filteredProjects = computed(() =>
  filterProjects(projects.value, projectFilter),
);
const projectOwnerOptions = computed(() => {
  const ownerMap = new Map<number, { id: number; name: string }>();
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
const pagedProjects = computed(() => {
  const start = (projectQuery.page - 1) * projectQuery.pageSize;
  return filteredProjects.value.slice(start, start + projectQuery.pageSize);
});

function activeOptions(kind: OptionKind) {
  return options.value.filter((item) => item.kind === kind && item.isActive);
}

function dateText(value?: null | string) {
  return value ? value.slice(0, 10) : '-';
}

function normalizeText(value?: null | string) {
  const text = value?.trim();
  return text ? text : null;
}

function statusMeta(status: string) {
  return followUpStatusMap[status] ?? defaultFollowUpStatus;
}

function generateOptionCode(kind: OptionKind) {
  const prefix = kind === 'project_type' ? 'type' : 'progress';
  return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;
}

async function loadData() {
  loading.value = true;
  try {
    projects.value = await listTestProjectsApi(deleteStatus.value);
    normalizeProjectPage();
  } finally {
    loading.value = false;
  }
}

function normalizeProjectPage() {
  if (
    (projectQuery.page - 1) * projectQuery.pageSize >=
    filteredProjects.value.length
  ) {
    projectQuery.page = 1;
  }
}

function searchProjects() {
  projectQuery.page = 1;
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
}

async function loadOptions() {
  options.value = await listTestProjectOptionsApi();
}

async function loadUsers() {
  if (users.value.length > 0) return;
  if (
    hasAccessByCodes(['approval:create']) ||
    hasAccessByCodes(['material-flow:transfer'])
  ) {
    users.value = await getUserOptionsApi();
    return;
  }
  if (hasAccessByCodes(['user:view'])) {
    const result = await getUserListApi('', 1, 500);
    users.value = result.items.filter((user) => user.isActive);
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
  void Promise.all([loadUsers(), loadBaseOptions()]);
}

function openEdit(row: TestProjectItem) {
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
  void Promise.all([loadUsers(), loadBaseOptions()]);
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
      `确认删除配置「${row.label}」？已使用的项目会保留编码但不再显示名称。`,
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
  currentProject.value = row;
  pendingFlows.value = [];
  myFlows.value = [];
  flowActiveTab.value = materialActionAccess.value.canApprove
    ? 'pending'
    : 'mine';
  pendingFlowQuery.page = 1;
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
  followupLoading.value = true;
  try {
    followups.value = await listTestProjectFollowupsApi(projectId);
  } finally {
    followupLoading.value = false;
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
  if (!project.canWriteFollowUp) {
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
    cancelFollowupEdit();
    await loadFollowups(project.id);
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    followupSaving.value = false;
  }
}

async function deleteFollowup(row: TestProjectFollowup) {
  const project = currentProject.value;
  if (!project) return;
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
    if (editingFollowupId.value === row.id) cancelFollowupEdit();
    await loadFollowups(project.id);
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
  materialLoading.value = true;
  try {
    const result = await listMaterialsApi(buildMaterialQuery(projectId));
    materials.value = result.items;
    materialTotal.value = result.total;
  } finally {
    materialLoading.value = false;
  }
}

function searchMaterials() {
  materialQuery.page = 1;
  void loadProjectMaterials();
}

function resetMaterialQuery() {
  Object.assign(materialQuery, {
    deleteStatus: 'all',
    materialNo: '',
    name: '',
    page: 1,
    status: undefined,
  });
  void loadProjectMaterials();
}

function onProjectPageSizeChange() {
  projectQuery.page = 1;
}

function onMaterialPageSizeChange() {
  materialQuery.page = 1;
  void loadProjectMaterials();
}

function loadMaterialFormOptions() {
  void Promise.all([loadUsers(), loadBaseOptions()]);
}

function openCreateMaterial() {
  editingMaterial.value = null;
  loadMaterialFormOptions();
  materialFormVisible.value = true;
}

function onMaterialRowCommand(command: number | string, row: MaterialItem) {
  switch (command) {
    case 'delete': {
      void removeMaterial(row);
      break;
    }
    case 'purge': {
      void purgeMaterial(row);
      break;
    }
    case 'restore': {
      void restoreMaterial(row);
      break;
    }
    case 'return': {
      void onReturnMaterial(row);
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
  editingMaterial.value = row;
  loadMaterialFormOptions();
  materialFormVisible.value = true;
}

async function openMaterialDetail(row: MaterialItem) {
  materialDetailVisible.value = true;
  materialDetailLoading.value = true;
  materialDetail.value = null;
  try {
    materialDetail.value = await getMaterialDetailApi(row.id);
  } finally {
    materialDetailLoading.value = false;
  }
}

function openTransfer(row: MaterialItem) {
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
  await loadProjectMaterials();
  if (currentProject.value?.id) {
    const id = currentProject.value.id;
    if (materialActionAccess.value.canApprove) {
      void listPendingFlowsApi(id)
        .then((v) => {
          pendingFlows.value = v;
          normalizeFlowPage('pending');
        })
        .catch(() => {});
    }
    void listMyFlowsApi(id)
      .then((v) => {
        myFlows.value = v;
        normalizeFlowPage('mine');
      })
      .catch(() => {});
  }
}

async function loadProjectFlows(projectId = currentProject.value?.id) {
  if (!projectId) return;
  const loadMine = flowActiveTab.value === 'mine';
  if (!loadMine && !materialActionAccess.value.canApprove) return;
  if (loadMine) myFlowLoading.value = true;
  else pendingFlowLoading.value = true;
  try {
    const flows = await (loadMine
      ? listMyFlowsApi(projectId)
      : listPendingFlowsApi(projectId));
    if (loadMine) {
      myFlows.value = flows;
      normalizeFlowPage('mine');
    } else {
      pendingFlows.value = flows;
      normalizeFlowPage('pending');
    }
  } finally {
    if (loadMine) myFlowLoading.value = false;
    else pendingFlowLoading.value = false;
  }
}

async function approveFlow(row: MaterialFlowItem) {
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

function onProjectTabChange(name: number | string) {
  if (name === 'materials') {
    void loadProjectMaterials();
  }
  if (name === 'flows') {
    void loadProjectFlows();
  }
}

function onFlowTabChange() {
  void loadProjectFlows();
}

function normalizeFlowPage(type: 'mine' | 'pending') {
  const query = type === 'mine' ? myFlowQuery : pendingFlowQuery;
  const total =
    type === 'mine' ? myFlows.value.length : pendingFlows.value.length;
  if ((query.page - 1) * query.pageSize >= total) {
    query.page = 1;
  }
}

function onPendingFlowPageSizeChange() {
  pendingFlowQuery.page = 1;
}

function onMyFlowPageSizeChange() {
  myFlowQuery.page = 1;
}

onMounted(async () => {
  const defaultPageSize = await getDefaultPageSize();
  projectQuery.pageSize = defaultPageSize;
  materialQuery.pageSize = defaultPageSize;
  pendingFlowQuery.pageSize = defaultPageSize;
  myFlowQuery.pageSize = defaultPageSize;
  pageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  materialPageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  flowPageSizeOptions.value = createPageSizeOptions(defaultPageSize);
  await Promise.all([loadOptions(), loadData()]);
});
</script>

<template>
  <re-page>
    <div class="material-projects-page p-5">
      <ProjectTable
        v-model:delete-status="deleteStatus"
        :access="projectActionAccess"
        :filter="projectFilter"
        :filtered-total="filteredProjects.length"
        :loading="loading"
        :owner-options="projectOwnerOptions"
        :page-size-options="pageSizeOptions"
        :paged-projects="pagedProjects"
        :progress-options="progressOptions"
        :project-type-options="projectTypeOptions"
        :query="projectQuery"
        @create="openCreate"
        @edit="openEdit"
        @open="openFollowups"
        @options="openOptionDialog"
        @page-size-change="onProjectPageSizeChange"
        @purge="purge"
        @remove="remove"
        @reset="resetProjectFilter"
        @restore="restore"
        @search="searchProjects"
        @status-change="loadData"
      />

      <ProjectFormDialog
        v-model:visible="dialogVisible"
        :editing="editingId !== null"
        :form="form"
        :progress-options="progressOptions"
        :project-type-options="projectTypeOptions"
        :saving="saving"
        :users="users"
        @save="save"
      />

      <ProjectOptionDialog
        v-model:active-kind="activeOptionKind"
        v-model:visible="optionDialogVisible"
        :can-manage="projectActionAccess.canOption"
        :displayed-options="displayedOptions"
        :editing-id="optionEditingId"
        :form="optionForm"
        :saving="optionSaving"
        @edit="openOptionEdit"
        @remove="removeOption"
        @reset="openOptionCreate"
        @save="saveOption"
      />

      <ElDrawer v-model="followupDrawerVisible" title="项目跟进" size="78%">
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
                    dateText(currentProject.nextFollowUpDueDate)
                  }}</strong>
                  <ElTag
                    :type="statusMeta(currentProject.followUpStatus).type"
                    size="small"
                  >
                    {{ statusMeta(currentProject.followUpStatus).label }}
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
              :access="materialActionAccess"
              :can-create="canWriteCurrentProjectMaterial"
              :can-edit="canEditCurrentProjectMaterial"
              :loading="materialLoading"
              :materials="materials"
              :page-size-options="materialPageSizeOptions"
              :query="materialQuery"
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
              :can-approve="materialActionAccess.canApprove"
              :my-count="myFlowCount"
              :my-flows="pagedMyFlows"
              :my-loading="myFlowLoading"
              :my-query="myFlowQuery"
              :page-size-options="flowPageSizeOptions"
              :pending-count="pendingFlowCount"
              :pending-flows="pagedPendingFlows"
              :pending-loading="pendingFlowLoading"
              :pending-query="pendingFlowQuery"
              @approve="approveFlow"
              @my-page-size-change="onMyFlowPageSizeChange"
              @pending-page-size-change="onPendingFlowPageSizeChange"
              @reject="rejectFlow"
              @tab-change="onFlowTabChange"
            />

            <ProjectFollowupsTab
              :can-manage="projectActionAccess.canFollowup"
              :editing-id="editingFollowupId"
              :followups="followups"
              :form="followupForm"
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
