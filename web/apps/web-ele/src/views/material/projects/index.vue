<script lang="ts" setup>
import type {
  MaterialDetail,
  MaterialFlowItem,
  MaterialItem,
  MaterialQuery,
  MaterialStatus,
} from '#/api/material';
import type { DepartmentNode, LocationNode } from '#/api/base-data';
import type {
  SaveTestProjectOptionPayload,
  SaveTestProjectPayload,
  TestProjectFollowup,
  TestProjectItem,
  TestProjectOption,
} from '#/api/test-project';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElDrawer,
  ElEmpty,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElSwitch,
  ElTabPane,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { getDepartmentTreeApi, getLocationTreeApi } from '#/api/base-data';
import { getDefaultPageSize } from '#/utils/runtime-settings';
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
import { getUserListApi } from '#/api/user';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';
import TransferDialog from '../components/TransferDialog.vue';

defineOptions({ name: 'MaterialProjects' });

type DeleteStatus = 'active' | 'all' | 'deleted';
type FlatOption = { id: number; label: string };
type OptionKind = 'project_progress' | 'project_type';
type ProjectFormState = {
  closedDate: string;
  code: string;
  followUpIntervalDays: number;
  name: string;
  ownerId?: number;
  plannedFinishDate: string;
  progressCode: string;
  projectTypeCode: string;
  startDate: string;
  testStatus: string;
};

const optionKindLabels: Record<OptionKind, string> = {
  project_progress: '项目进度',
  project_type: '项目类型',
};

const followUpStatusMap: Record<string, { label: string; type: 'danger' | 'success' | 'warning' }> = {
  due: { label: '今日到期', type: 'warning' },
  overdue: { label: '已超期', type: 'danger' },
  upcoming: { label: '未到期', type: 'success' },
};
const defaultFollowUpStatus: { label: string; type: 'danger' | 'success' | 'warning' } = {
  label: '未到期',
  type: 'success',
};

const materialStatusOptions: Array<{
  label: string;
  tag: 'info' | 'success';
  value: MaterialStatus;
}> = [
  { label: '在用', tag: 'success', value: 0 },
  { label: '已退回厂商', tag: 'info', value: 1 },
];

const flowStatusMeta: Record<string, { label: string; tag: 'info' | 'success' | 'warning' }> = {
  approved: { label: '已通过', tag: 'success' },
  pending: { label: '审批中', tag: 'warning' },
  rejected: { label: '已驳回', tag: 'info' },
};

const { hasAccessByCodes } = useAccess();
const canManage = computed(() => hasAccessByCodes(['project:manage']));
const canPurge = computed(() => hasAccessByCodes(['material:purge']));
const canCreateMaterial = computed(() => hasAccessByCodes(['material:create']));
const canEditMaterial = computed(() => hasAccessByCodes(['material:edit']));
const canDeleteMaterial = computed(() => hasAccessByCodes(['material:delete']));
const canTransferMaterial = computed(() => hasAccessByCodes(['material:transfer']));
const canRestoreMaterial = computed(() => hasAccessByCodes(['material:restore']));
const canApproveMaterial = computed(() => hasAccessByCodes(['material:approve']));

const loading = ref(false);
const projects = ref<TestProjectItem[]>([]);
const deleteStatus = ref<DeleteStatus>('all');

const options = ref<TestProjectOption[]>([]);
const users = ref<UserDto[]>([]);
const departments = ref<DepartmentNode[]>([]);
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
const activeProjectTab = ref('followups');
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
const transferMaterial = ref<MaterialItem | null>(null);
const materialQuery = reactive({
  deleteStatus: 'all' as DeleteStatus,
  materialNo: '',
  name: '',
  page: 1,
  pageSize: 10,
  status: undefined as MaterialStatus | undefined,
});

const flowActiveTab = ref('pending');
const pendingFlowLoading = ref(false);
const myFlowLoading = ref(false);
const pendingFlows = ref<MaterialFlowItem[]>([]);
const myFlows = ref<MaterialFlowItem[]>([]);

const projectTypeOptions = computed(() => activeOptions('project_type'));
const progressOptions = computed(() => activeOptions('project_progress'));
const displayedOptions = computed(() =>
  options.value.filter((item) => item.kind === activeOptionKind.value),
);
const departmentOptions = computed(() => flattenDepartments(departments.value));
const locationOptions = computed<FlatOption[]>(() =>
  locations.value.map((node) => ({ id: node.id, label: node.name })),
);
const currentProjectList = computed(() =>
  currentProject.value ? [currentProject.value] : projects.value,
);

function activeOptions(kind: OptionKind) {
  return options.value.filter((item) => item.kind === kind && item.isActive);
}

function flattenDepartments(nodes: DepartmentNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'　'.repeat(level)}${node.name}` },
    ...flattenDepartments(node.children, level + 1),
  ]);
}

function dateText(value?: null | string) {
  return value ? value.slice(0, 10) : '-';
}

function dateTimeText(value?: null | string) {
  if (!value) return '-';
  return value.replace('T', ' ').slice(0, 16);
}

function optionalText(value?: null | string) {
  return value && value.trim() ? value : '-';
}

function normalizeText(value?: null | string) {
  const text = value?.trim();
  return text ? text : null;
}

function statusMeta(status: string) {
  return followUpStatusMap[status] ?? defaultFollowUpStatus;
}

function materialStatusMeta(status: MaterialStatus) {
  return materialStatusOptions.find((item) => item.value === status) ?? materialStatusOptions[0]!;
}

function flowMetaOf(status: string) {
  return flowStatusMeta[status] ?? { label: status, tag: 'info' as const };
}

function optionKindLabel(kind: OptionKind) {
  return optionKindLabels[kind];
}

function generateOptionCode(kind: OptionKind) {
  const prefix = kind === 'project_type' ? 'type' : 'progress';
  return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;
}

async function loadData() {
  loading.value = true;
  try {
    projects.value = await listTestProjectsApi(deleteStatus.value);
  } finally {
    loading.value = false;
  }
}

async function loadOptions() {
  options.value = await listTestProjectOptionsApi();
}

async function loadUsers() {
  const result = await getUserListApi('', 1, 500);
  users.value = result.items.filter((user) => user.isActive);
}

async function loadBaseOptions() {
  const [departmentTree, locationTree] = await Promise.all([
    getDepartmentTreeApi(),
    getLocationTreeApi(),
  ]);
  departments.value = departmentTree;
  locations.value = locationTree;
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
  if (!form.name.trim()) {
    ElMessage.warning('请填写项目名称');
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
    await ElMessageBox.confirm(`确认撤销删除项目「${row.name}」？`, '撤销删除', {
      type: 'warning',
    });
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
  flowActiveTab.value = 'pending';
  resetMaterialQuery();
  activeProjectTab.value = 'followups';
  editingFollowupId.value = null;
  Object.assign(followupForm, {
    content: '',
    dueDate: row.nextFollowUpDueDate ? row.nextFollowUpDueDate.slice(0, 10) : '',
  });
  followupDrawerVisible.value = true;
  await Promise.allSettled([loadFollowups(row.id), loadProjectMaterials(row.id)]);
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
    ElMessage.warning('只有项目负责人或管理员可以填写落地跟进');
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
      ? updateTestProjectFollowupApi(project.id, editingFollowupId.value, payload)
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

function openCreateMaterial() {
  editingMaterial.value = null;
  materialFormVisible.value = true;
}

function openEditMaterial(row: MaterialItem) {
  editingMaterial.value = row;
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
    await ElMessageBox.confirm(`确认撤销删除料件「${row.name}」？`, '撤销删除', {
      type: 'warning',
    });
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

function isReturned(row: MaterialItem) {
  return row.status === 1;
}

function canOperateMaterial(row: MaterialItem) {
  return !row.isDeleted && !isReturned(row);
}

function materialRowClassName({ row }: { row: MaterialItem }) {
  if (row.isDeleted) return 'material-row-deleted';
  return isReturned(row) ? 'material-row-returned' : '';
}

async function afterMaterialChanged() {
  await loadProjectMaterials();
  if (currentProject.value?.id) {
    const id = currentProject.value.id;
    void listPendingFlowsApi(id).then((v) => { pendingFlows.value = v; }).catch(() => {});
    void listMyFlowsApi(id).then((v) => { myFlows.value = v; }).catch(() => {});
  }
}

async function loadProjectFlows(projectId = currentProject.value?.id) {
  if (!projectId) return;
  const loadMine = flowActiveTab.value === 'mine';
  if (loadMine) myFlowLoading.value = true;
  else pendingFlowLoading.value = true;
  try {
    const flows = await (loadMine
      ? listMyFlowsApi(projectId)
      : listPendingFlowsApi(projectId));
    if (loadMine) myFlows.value = flows;
    else pendingFlows.value = flows;
  } finally {
    if (loadMine) myFlowLoading.value = false;
    else pendingFlowLoading.value = false;
  }
}

async function approveFlow(row: MaterialFlowItem) {
  try {
    await ElMessageBox.confirm(
      `确认通过料件「${row.materialName}」的流转申请？`,
      '审批通过',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await approveFlowApi(row.id, '同意');
    ElMessage.success('已通过');
    await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function rejectFlow(row: MaterialFlowItem) {
  let reason = '不同意';
  try {
    const result = await ElMessageBox.prompt('请输入驳回原因', '驳回', {
      inputPlaceholder: '驳回原因',
    });
    reason = result.value || reason;
  } catch {
    return;
  }
  try {
    await rejectFlowApi(row.id, reason);
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

function tableRowClassName({ row }: { row: TestProjectItem }) {
  return row.isDeleted ? 'project-row-deleted' : '';
}

onMounted(async () => {
  materialQuery.pageSize = await getDefaultPageSize();
  await Promise.all([loadOptions(), loadData()]);
});
</script>

<template>
  <re-page>
    <div class="p-5">
      <div class="mb-4 flex flex-wrap items-center gap-3">
        <ElSelect
          v-model="deleteStatus"
          placeholder="删除状态"
          style="width: 130px"
          @change="loadData"
        >
          <ElOption label="全部" value="all" />
          <ElOption label="未删除" value="active" />
          <ElOption label="已删除" value="deleted" />
        </ElSelect>
        <ElButton v-if="canManage" type="primary" @click="openCreate">
          新增项目
        </ElButton>
        <ElButton v-if="canManage" @click="openOptionDialog">配置</ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="projects"
        :row-class-name="tableRowClassName"
        border
        stripe
      >
        <ElTableColumn fixed label="项目名称" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <ElButton link type="primary" @click="openFollowups(row)">
              {{ row.name }}
            </ElButton>
          </template>
        </ElTableColumn>
        <ElTableColumn label="项目编号" min-width="130" prop="code" show-overflow-tooltip />
        <ElTableColumn label="项目类型" min-width="120">
          <template #default="{ row }">
            {{ row.projectTypeLabel || row.projectTypeCode || '-' }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="负责人" min-width="110">
          <template #default="{ row }">
            {{ row.ownerName || '-' }}
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="开始时间" width="110">
          <template #default="{ row }">{{ dateText(row.startDate) }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="计划完成" width="110">
          <template #default="{ row }">{{ dateText(row.plannedFinishDate) }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="结案时间" width="110">
          <template #default="{ row }">{{ dateText(row.closedDate) }}</template>
        </ElTableColumn>
        <ElTableColumn label="进度" min-width="110">
          <template #default="{ row }">
            <ElTag size="small" type="info">
              {{ row.progressLabel || row.progressCode || '-' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="测试情况" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            {{ optionalText(row.testStatus) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="最新落地跟进" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">
            <div>{{ optionalText(row.latestFollowUpContent) }}</div>
            <div v-if="row.latestFollowUpAt" class="table-sub-text">
              {{ dateTimeText(row.latestFollowUpAt) }}
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="下次跟进" width="130">
          <template #default="{ row }">
            <div>{{ dateText(row.nextFollowUpDueDate) }}</div>
            <ElTag :type="statusMeta(row.followUpStatus).type" size="small">
              {{ statusMeta(row.followUpStatus).label }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="间隔" width="80">
          <template #default="{ row }">{{ row.followUpIntervalDays }}天</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="料件数" prop="materialCount" width="80" />
        <ElTableColumn align="center" label="状态" width="90">
          <template #default="{ row }">
            <ElTag v-if="row.isDeleted" size="small" type="danger">已删除</ElTag>
            <ElTag v-else size="small" type="success">正常</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" fixed="right" label="操作" width="160">
          <template #default="{ row }">
            <template v-if="!row.isDeleted">
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="danger"
                @click="remove(row)"
              >
                删除
              </ElButton>
            </template>
            <template v-else>
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="success"
                @click="restore(row)"
              >
                撤销删除
              </ElButton>
              <ElButton
                v-if="canPurge"
                link
                size="small"
                type="danger"
                @click="purge(row)"
              >
                彻底删除
              </ElButton>
            </template>
          </template>
        </ElTableColumn>
      </ElTable>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑测试项目' : '新增测试项目'"
        width="720px"
      >
        <ElForm label-width="96px">
          <div class="form-grid">
            <ElFormItem label="项目名称">
              <ElInput v-model="form.name" placeholder="必填" />
            </ElFormItem>
            <ElFormItem label="项目编号">
              <ElInput v-model="form.code" placeholder="可选" />
            </ElFormItem>
            <ElFormItem label="项目类型">
              <ElSelect v-model="form.projectTypeCode" clearable placeholder="请选择">
                <ElOption
                  v-for="item in projectTypeOptions"
                  :key="item.id"
                  :label="item.label"
                  :value="item.code"
                />
              </ElSelect>
            </ElFormItem>
            <ElFormItem label="进度">
              <ElSelect v-model="form.progressCode" clearable placeholder="请选择">
                <ElOption
                  v-for="item in progressOptions"
                  :key="item.id"
                  :label="item.label"
                  :value="item.code"
                />
              </ElSelect>
            </ElFormItem>
            <ElFormItem label="负责人">
              <ElSelect v-model="form.ownerId" clearable filterable placeholder="请选择">
                <ElOption
                  v-for="user in users"
                  :key="user.id"
                  :label="`${user.name}（${user.employeeNo}）`"
                  :value="user.id"
                />
              </ElSelect>
            </ElFormItem>
            <ElFormItem label="跟进间隔">
              <ElInputNumber
                v-model="form.followUpIntervalDays"
                :max="365"
                :min="1"
                controls-position="right"
                style="width: 100%"
              />
            </ElFormItem>
            <ElFormItem label="开始时间">
              <ElDatePicker
                v-model="form.startDate"
                placeholder="选择日期"
                style="width: 100%"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </ElFormItem>
            <ElFormItem label="计划完成">
              <ElDatePicker
                v-model="form.plannedFinishDate"
                placeholder="选择日期"
                style="width: 100%"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </ElFormItem>
            <ElFormItem label="结案时间">
              <ElDatePicker
                v-model="form.closedDate"
                placeholder="选择日期"
                style="width: 100%"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </ElFormItem>
          </div>
          <ElFormItem label="测试情况">
            <ElInput
              v-model="form.testStatus"
              :rows="4"
              maxlength="1000"
              placeholder="记录当前测试结论、阻塞点或风险"
              show-word-limit
              type="textarea"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>

      <ElDialog v-model="optionDialogVisible" title="项目配置" width="760px">
        <ElTabs v-model="activeOptionKind" @tab-change="resetOptionForm(activeOptionKind)">
          <ElTabPane label="项目类型" name="project_type" />
          <ElTabPane label="项目进度" name="project_progress" />
        </ElTabs>
        <div class="option-editor">
          <ElForm label-width="70px">
            <div class="option-form-grid">
              <ElFormItem label="名称">
                <ElInput v-model="optionForm.label" placeholder="中文名称" />
              </ElFormItem>
              <ElFormItem label="排序">
                <ElInputNumber
                  v-model="optionForm.sort"
                  controls-position="right"
                  style="width: 100%"
                />
              </ElFormItem>
              <ElFormItem label="启用">
                <ElSwitch v-model="optionForm.isActive" />
              </ElFormItem>
            </div>
          </ElForm>
          <div class="option-actions">
            <ElButton v-if="optionEditingId" @click="openOptionCreate(activeOptionKind)">
              取消编辑
            </ElButton>
            <ElButton :loading="optionSaving" type="primary" @click="saveOption">
              {{ optionEditingId ? '保存修改' : '新增' }}
            </ElButton>
          </div>
        </div>
        <ElTable :data="displayedOptions" border>
          <ElTableColumn label="类型" width="110">
            <template #default="{ row }">{{ optionKindLabel(row.kind as OptionKind) }}</template>
          </ElTableColumn>
          <ElTableColumn label="名称" prop="label" />
          <ElTableColumn align="center" label="排序" prop="sort" width="80" />
          <ElTableColumn align="center" label="启用" width="80">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '启用' : '停用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" label="操作" width="130">
            <template #default="{ row }">
              <ElButton link size="small" type="primary" @click="openOptionEdit(row)">
                编辑
              </ElButton>
              <ElButton link size="small" type="danger" @click="removeOption(row)">
                删除
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </ElDialog>

      <ElDrawer
        v-model="followupDrawerVisible"
        :title="currentProject ? `项目跟进 - ${currentProject.name}` : '项目跟进'"
        size="78%"
      >
        <div v-if="currentProject" class="followup-panel">
          <div class="followup-summary">
            <div>
              <span class="summary-label">负责人</span>
              <span>{{ currentProject.ownerName || '-' }}</span>
            </div>
            <div>
              <span class="summary-label">下次跟进</span>
              <span>{{ dateText(currentProject.nextFollowUpDueDate) }}</span>
              <ElTag :type="statusMeta(currentProject.followUpStatus).type" size="small">
                {{ statusMeta(currentProject.followUpStatus).label }}
              </ElTag>
            </div>
            <div>
              <span class="summary-label">间隔</span>
              <span>{{ currentProject.followUpIntervalDays }}天</span>
            </div>
          </div>

          <ElTabs v-model="activeProjectTab" @tab-change="onProjectTabChange">
            <ElTabPane label="落地跟进" name="followups">
              <div v-if="currentProject.canWriteFollowUp" class="followup-editor">
                <ElForm label-width="86px">
                  <ElFormItem label="跟进日期">
                    <ElDatePicker
                      v-model="followupForm.dueDate"
                      placeholder="选择对应周期日期"
                      style="width: 100%"
                      type="date"
                      value-format="YYYY-MM-DD"
                    />
                  </ElFormItem>
                  <ElFormItem label="落地情况">
                    <ElInput
                      v-model="followupForm.content"
                      :rows="4"
                      maxlength="2000"
                      placeholder="填写本周期落地进展、问题和下一步"
                      show-word-limit
                      type="textarea"
                    />
                  </ElFormItem>
                </ElForm>
                <div class="followup-actions">
                  <ElButton v-if="editingFollowupId" @click="cancelFollowupEdit">
                    取消编辑
                  </ElButton>
                  <ElButton :loading="followupSaving" type="primary" @click="saveFollowup">
                    {{ editingFollowupId ? '保存修改' : '新增跟进' }}
                  </ElButton>
                </div>
              </div>
              <ElTag v-else type="info">
                只有项目负责人或管理员可以填写，其他人只读
              </ElTag>

              <div v-loading="followupLoading" class="followup-list">
                <ElEmpty v-if="!followups.length && !followupLoading" description="暂无跟进记录" />
                <ElTimeline v-else>
                  <ElTimelineItem
                    v-for="item in followups"
                    :key="item.id"
                    :timestamp="`${dateText(item.dueDate)} · ${item.filledByName || '-'} · ${dateTimeText(item.filledAt)}`"
                    placement="top"
                  >
                    <div class="followup-content">{{ item.content }}</div>
                    <ElButton
                      v-if="currentProject.canWriteFollowUp"
                      link
                      size="small"
                      type="primary"
                      @click="editFollowup(item)"
                    >
                      编辑
                    </ElButton>
                    <ElButton
                      v-if="currentProject.canWriteFollowUp"
                      link
                      size="small"
                      type="danger"
                      @click="deleteFollowup(item)"
                    >
                      删除
                    </ElButton>
                  </ElTimelineItem>
                </ElTimeline>
              </div>
            </ElTabPane>

            <ElTabPane label="料件清单" name="materials">
              <div class="material-filter">
                <ElInput
                  v-model="materialQuery.materialNo"
                  clearable
                  placeholder="料件编号"
                  style="width: 150px"
                  @keyup.enter="searchMaterials"
                />
                <ElInput
                  v-model="materialQuery.name"
                  clearable
                  placeholder="料件名称"
                  style="width: 160px"
                  @keyup.enter="searchMaterials"
                />
                <ElSelect
                  v-model="materialQuery.status"
                  clearable
                  placeholder="状态"
                  style="width: 120px"
                >
                  <ElOption
                    v-for="item in materialStatusOptions"
                    :key="item.value"
                    :label="item.label"
                    :value="item.value"
                  />
                </ElSelect>
                <ElSelect
                  v-model="materialQuery.deleteStatus"
                  placeholder="删除状态"
                  style="width: 120px"
                  @change="searchMaterials"
                >
                  <ElOption label="全部" value="all" />
                  <ElOption label="未删除" value="active" />
                  <ElOption label="已删除" value="deleted" />
                </ElSelect>
                <ElButton type="primary" @click="searchMaterials">查询</ElButton>
                <ElButton @click="resetMaterialQuery">重置</ElButton>
                <ElButton v-if="canCreateMaterial" type="primary" @click="openCreateMaterial">
                  新增料件
                </ElButton>
              </div>

              <ElTable
                v-loading="materialLoading"
                :data="materials"
                :row-class-name="materialRowClassName"
                border
                class="mt-4"
                stripe
              >
                <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
                <ElTableColumn label="名称" min-width="140" prop="name" show-overflow-tooltip />
                <ElTableColumn label="厂商" min-width="120" prop="vendorName" show-overflow-tooltip />
                <ElTableColumn label="型号品牌" min-width="140" show-overflow-tooltip>
                  <template #default="{ row }">
                    <span v-if="row.model || row.brand">{{ row.model }} {{ row.brand }}</span>
                    <span v-else>-</span>
                  </template>
                </ElTableColumn>
                <ElTableColumn align="center" label="数量" prop="quantity" width="70" />
                <ElTableColumn label="部门" min-width="110" prop="departmentName" show-overflow-tooltip />
                <ElTableColumn label="保管人" min-width="100" prop="custodianName" show-overflow-tooltip />
                <ElTableColumn align="center" label="状态" width="130">
                  <template #default="{ row }">
                    <ElTag :type="materialStatusMeta(row.status).tag" size="small">
                      {{ materialStatusMeta(row.status).label }}
                    </ElTag>
                    <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
                      已删除
                    </ElTag>
                  </template>
                </ElTableColumn>
                <ElTableColumn align="center" label="操作" width="280">
                  <template #default="{ row }">
                    <template v-if="!row.isDeleted">
                      <ElButton link size="small" type="primary" @click="openMaterialDetail(row)">
                        详情
                      </ElButton>
                      <ElButton
                        v-if="canEditMaterial && canOperateMaterial(row)"
                        link
                        size="small"
                        type="primary"
                        @click="openEditMaterial(row)"
                      >
                        编辑
                      </ElButton>
                      <ElButton
                        v-if="canTransferMaterial && canOperateMaterial(row) && !row.hasPendingFlow"
                        link
                        size="small"
                        type="info"
                        @click="openTransfer(row)"
                      >
                        转移
                      </ElButton>
                      <ElButton
                        v-if="canEditMaterial && canOperateMaterial(row) && !row.hasPendingFlow"
                        link
                        size="small"
                        type="warning"
                        @click="onReturnMaterial(row)"
                      >
                        退回厂商
                      </ElButton>
                      <ElButton
                        v-if="canDeleteMaterial && canOperateMaterial(row)"
                        link
                        size="small"
                        type="danger"
                        @click="removeMaterial(row)"
                      >
                        删除
                      </ElButton>
                    </template>
                    <template v-else>
                      <ElButton link size="small" type="primary" @click="openMaterialDetail(row)">
                        详情
                      </ElButton>
                      <ElButton
                        v-if="canRestoreMaterial"
                        link
                        size="small"
                        type="success"
                        @click="restoreMaterial(row)"
                      >
                        撤销删除
                      </ElButton>
                      <ElButton
                        v-if="canPurge"
                        link
                        size="small"
                        type="danger"
                        @click="purgeMaterial(row)"
                      >
                        彻底删除
                      </ElButton>
                    </template>
                  </template>
                </ElTableColumn>
              </ElTable>

              <div class="mt-4 flex justify-end">
                <ElPagination
                  v-model:current-page="materialQuery.page"
                  :page-size="materialQuery.pageSize"
                  :total="materialTotal"
                  background
                  layout="total, prev, pager, next"
                  @current-change="() => loadProjectMaterials(currentProject?.id)"
                />
              </div>
            </ElTabPane>

            <ElTabPane label="流转审批" name="flows">
              <ElTabs v-model="flowActiveTab" @tab-change="onFlowTabChange">
                <ElTabPane label="待我审批" name="pending">
                  <ElTable v-loading="pendingFlowLoading" :data="pendingFlows" border stripe>
                    <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
                    <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
                    <ElTableColumn label="料件名称" min-width="140" prop="materialName" show-overflow-tooltip />
                    <ElTableColumn label="发起人" min-width="90" prop="applicant" />
                    <ElTableColumn label="受让人" min-width="90" prop="transferee" />
                    <ElTableColumn label="原因" min-width="150" prop="reason" show-overflow-tooltip />
                    <ElTableColumn align="center" label="操作" width="140">
                      <template #default="{ row }">
                        <ElButton v-if="canApproveMaterial" link size="small" type="success" @click="approveFlow(row)">
                          通过
                        </ElButton>
                        <ElButton v-if="canApproveMaterial" link size="small" type="danger" @click="rejectFlow(row)">
                          驳回
                        </ElButton>
                      </template>
                    </ElTableColumn>
                  </ElTable>
                </ElTabPane>
                <ElTabPane label="我的发起" name="mine">
                  <ElTable v-loading="myFlowLoading" :data="myFlows" border stripe>
                    <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
                    <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
                    <ElTableColumn label="料件名称" min-width="140" prop="materialName" show-overflow-tooltip />
                    <ElTableColumn label="受让人" min-width="90" prop="transferee" />
                    <ElTableColumn label="原因" min-width="150" prop="reason" show-overflow-tooltip />
                    <ElTableColumn align="center" label="状态" width="110">
                      <template #default="{ row }">
                        <ElTag :type="flowMetaOf(row.status).tag" size="small">
                          {{ flowMetaOf(row.status).label }}
                        </ElTag>
                      </template>
                    </ElTableColumn>
                  </ElTable>
                </ElTabPane>
              </ElTabs>
            </ElTabPane>
          </ElTabs>
        </div>
      </ElDrawer>

      <MaterialFormDialog
        v-model:visible="materialFormVisible"
        :default-project-id="currentProject?.id"
        :department-options="departmentOptions"
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
    </div>
  </re-page>
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 16px;
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
  justify-content: flex-end;
  gap: 8px;
}

.table-sub-text {
  margin-top: 2px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.followup-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.followup-summary,
.followup-editor {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.followup-summary {
  display: grid;
  gap: 8px;
  font-size: 13px;
}

.summary-label {
  display: inline-block;
  width: 72px;
  color: var(--el-text-color-secondary);
}

.followup-list {
  min-height: 160px;
}

.followup-content {
  margin-bottom: 4px;
  line-height: 1.6;
  white-space: pre-wrap;
}

.material-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
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
  .form-grid,
  .option-form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
