<script lang="ts" setup>
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

import {
  createTestProjectApi,
  createTestProjectFollowupApi,
  createTestProjectOptionApi,
  deleteTestProjectApi,
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

defineOptions({ name: 'MaterialProjects' });

type DeleteStatus = 'active' | 'all' | 'deleted';
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

const { hasAccessByCodes } = useAccess();
const canManage = computed(() => hasAccessByCodes(['project:manage']));
const canPurge = computed(() => hasAccessByCodes(['material:purge']));

const loading = ref(false);
const projects = ref<TestProjectItem[]>([]);
const deleteStatus = ref<DeleteStatus>('all');

const options = ref<TestProjectOption[]>([]);
const users = ref<UserDto[]>([]);

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
const followups = ref<TestProjectFollowup[]>([]);
const followupLoading = ref(false);
const followupSaving = ref(false);
const editingFollowupId = ref<null | number>(null);
const followupForm = reactive({
  content: '',
  dueDate: '',
});

const projectTypeOptions = computed(() => activeOptions('project_type'));
const progressOptions = computed(() => activeOptions('project_progress'));
const displayedOptions = computed(() =>
  options.value.filter((item) => item.kind === activeOptionKind.value),
);

function activeOptions(kind: OptionKind) {
  return options.value.filter((item) => item.kind === kind && item.isActive);
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
  if (!canManage.value) return;
  const result = await getUserListApi('', 1, 500);
  users.value = result.items.filter((user) => user.isActive);
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
  saving.value = true;
  try {
    const payload = buildProjectPayload();
    await (editingId.value
      ? updateTestProjectApi(editingId.value, payload)
      : createTestProjectApi(payload));
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
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
  await deleteTestProjectApi(row.id);
  ElMessage.success('已删除');
  await loadData();
}

async function restore(row: TestProjectItem) {
  await restoreTestProjectApi(row.id);
  ElMessage.success('已恢复');
  await loadData();
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
  await purgeTestProjectApi(row.id);
  ElMessage.success('已彻底删除');
  await loadData();
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
  await deleteTestProjectOptionApi(row.id);
  ElMessage.success('配置已删除');
  if (optionEditingId.value === row.id) resetOptionForm(row.kind);
  await loadOptions();
  await loadData();
}

async function openFollowups(row: TestProjectItem) {
  currentProject.value = row;
  editingFollowupId.value = null;
  Object.assign(followupForm, {
    content: '',
    dueDate: row.nextFollowUpDueDate ? row.nextFollowUpDueDate.slice(0, 10) : '',
  });
  followupDrawerVisible.value = true;
  await loadFollowups(row.id);
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
  } finally {
    followupSaving.value = false;
  }
}

function tableRowClassName({ row }: { row: TestProjectItem }) {
  return row.isDeleted ? 'project-row-deleted' : '';
}

onMounted(async () => {
  await Promise.all([loadOptions(), loadUsers(), loadData()]);
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
        <ElTableColumn fixed label="项目名称" min-width="180" prop="name" show-overflow-tooltip />
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
        <ElTableColumn align="center" fixed="right" label="操作" width="250">
          <template #default="{ row }">
            <template v-if="!row.isDeleted">
              <ElButton link size="small" type="primary" @click="openFollowups(row)">
                跟进
              </ElButton>
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
        :title="currentProject ? `落地情况跟进 - ${currentProject.name}` : '落地情况跟进'"
        size="520px"
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
              </ElTimelineItem>
            </ElTimeline>
          </div>
        </div>
      </ElDrawer>
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

:deep(.project-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: #f3f4f6 !important;
}

@media (max-width: 768px) {
  .form-grid,
  .option-form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
