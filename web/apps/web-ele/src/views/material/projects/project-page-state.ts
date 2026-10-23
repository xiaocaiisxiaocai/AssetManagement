import type { ProjectFilter } from './project-filter';
import type {
  DeleteStatus,
  OptionKind,
  ProjectFormState,
  ProjectProgressFormState,
} from './project-workspace-types';

import type { DepartmentOptionNode } from '#/api/base-data';
import type {
  MaterialDetail,
  MaterialFlowItem,
  MaterialItem,
  MaterialStatus,
} from '#/api/material';
import type {
  SaveTestProjectOptionPayload,
  TestProjectFollowup,
  TestProjectItem,
  TestProjectOption,
} from '#/api/test-project';
import type { UserOptionDto } from '#/api/user';

import { reactive, ref } from 'vue';

import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import { createPageSizeOptions } from '#/utils/runtime-settings';

export function createProjectPageState() {
  const currentProject = ref<null | TestProjectItem>(null);
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
    ownerIds: [],
    progressCodes: [],
    projectTypeCodes: [],
  });
  const options = ref<TestProjectOption[]>([]);
  const users = ref<UserOptionDto[]>([]);
  const userOptionsLoading = ref(false);
  const userOptionsRequestGuard = createLatestRequestGuard();
  const departments = ref<DepartmentOptionNode[]>([]);
  const dialogVisible = ref(false);
  const importDialogVisible = ref(false);
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
    rowVersion: 0,
  });
  const progressBaseline = ref('');
  const progressTestingIncomplete = ref(false);
  const progressDialogVisible = ref(false);
  const progressEditingProject = ref<null | TestProjectItem>(null);
  const progressSaving = ref(false);
  const progressForm = reactive<ProjectProgressFormState>({
    closedDate: '',
    progressCode: '',
    testStatus: '',
    rowVersion: 0,
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
  const activeProjectTab = ref('stages');
  const followups = ref<TestProjectFollowup[]>([]);
  const followupLoading = ref(false);
  const followupSaving = ref(false);
  const followupOperationGuard = createLatestRequestGuard();
  const editingFollowupId = ref<null | number>(null);
  const followupForm = reactive({ content: '' });
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
    statuses: [] as MaterialStatus[],
  });
  const materialPageSizeOptions = ref(createPageSizeOptions(20));
  const flowActiveTab = ref('mine');
  const pendingFlowLoading = ref(false);
  const handledFlowLoading = ref(false);
  const myFlowLoading = ref(false);
  const pendingFlows = ref<MaterialFlowItem[]>([]);
  const handledFlows = ref<MaterialFlowItem[]>([]);
  const myFlows = ref<MaterialFlowItem[]>([]);
  const processingFlowIds = ref<number[]>([]);
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
  const flowPageSizeOptions = ref(createPageSizeOptions(20));

  return {
    currentProject,
    loading,
    projects,
    projectTotal,
    deleteStatus,
    pageSizeOptions,
    projectQuery,
    projectFilter,
    projectQueryModel: reactiveObjectModel(projectQuery),
    projectFilterModel: reactiveObjectModel(projectFilter),
    options,
    users,
    userOptionsLoading,
    userOptionsRequestGuard,
    departments,
    dialogVisible,
    importDialogVisible,
    editingId,
    saving,
    form,
    formModel: reactiveObjectModel(form),
    progressBaseline,
    progressTestingIncomplete,
    progressDialogVisible,
    progressEditingProject,
    progressSaving,
    progressForm,
    progressFormModel: reactiveObjectModel(progressForm),
    optionDialogVisible,
    optionSaving,
    optionEditingId,
    activeOptionKind,
    optionForm,
    optionFormModel: reactiveObjectModel(optionForm),
    followupDrawerVisible,
    activeProjectTab,
    followups,
    followupLoading,
    followupSaving,
    followupOperationGuard,
    editingFollowupId,
    followupForm,
    followupFormModel: reactiveObjectModel(followupForm),
    materialLoading,
    materials,
    materialTotal,
    materialFormVisible,
    editingMaterial,
    materialDetailVisible,
    materialDetailLoading,
    materialDetail,
    followupRequestGuard,
    projectListRequestGuard,
    materialRequestGuard,
    pendingFlowRequestGuard,
    handledFlowRequestGuard,
    myFlowRequestGuard,
    materialDetailRequestGuard,
    transferVisible,
    workflowNodeSelector,
    transferMaterial,
    materialQuery,
    materialQueryModel: reactiveObjectModel(materialQuery),
    materialPageSizeOptions,
    flowActiveTab,
    pendingFlowLoading,
    handledFlowLoading,
    myFlowLoading,
    pendingFlows,
    handledFlows,
    myFlows,
    processingFlowIds,
    pendingFlowTotal,
    handledFlowTotal,
    myFlowTotal,
    pendingFlowQuery,
    myFlowQuery,
    handledFlowQuery,
    pendingFlowQueryModel: reactiveObjectModel(pendingFlowQuery),
    handledFlowQueryModel: reactiveObjectModel(handledFlowQuery),
    myFlowQueryModel: reactiveObjectModel(myFlowQuery),
    flowPageSizeOptions,
  };
}
