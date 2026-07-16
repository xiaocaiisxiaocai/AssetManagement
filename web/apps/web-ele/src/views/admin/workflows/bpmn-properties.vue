<script lang="ts" setup>
import type { RoleDto } from '#/api/role';
import type { UserOptionDto } from '#/api/user';
import type { DepartmentNode, OrganizationLevel } from '#/api/base-data';
import type { AssigneeType } from './assignee-identities';

import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { useAccess } from '@vben/access';

import {
  getDepartmentTreeApi,
  getOrganizationLevelsApi,
} from '#/api/base-data';
import { getRoleListApi } from '#/api/role';
import { getUserListApi, getUserOptionsApi } from '#/api/user';
import {
  loadAssigneeSelection,
  roleAssigneeIdentity,
  serializeAssigneeSelection,
  userAssigneeIdentity,
} from './assignee-identities';
import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElRadioGroup,
  ElRadioButton,
  ElSelect,
  ElTag,
} from 'element-plus';
import {
  gatewaySupportsConditions,
  getSuggestedBranchName,
  getTargetNodeTitle,
  isDefaultGatewayBranch,
} from './gateway-branches';

defineOptions({ name: 'BpmnProperties' });

interface Props {
  element: any; // BPMN 元素
  modeler: any; // BPMN Modeler 实例
}

const props = defineProps<Props>();
const { hasAccessByCodes } = useAccess();

const elementType = ref('');
const elementName = ref('');
const elementId = ref('');

// 审批人类型
const assigneeType = ref<AssigneeType>('');
const assigneeValue = ref<string | string[]>('');
const approvalMode = ref<'all' | 'any'>('any');
const userOptions = ref<UserOptionDto[]>([]);
const roleOptions = ref<RoleDto[]>([]);
const departmentOptions = ref<{ label: string; value: string }[]>([]);
const organizationLevels = ref<OrganizationLevel[]>([]);

// 条件表达式
const conditionExpression = ref('');
const conditionField = ref('applicantDept');
const conditionOperator = ref('==');
const conditionValue = ref('');
const gatewayConditions = ref<GatewayCondition[]>([]);
const isLoadingElement = ref(false);
let boundEventBus: any;
let gatewayRefreshQueued = false;

interface ParsedCondition {
  expression: string;
  field: string;
  operator: string;
  value: string;
}

interface GatewayCondition extends ParsedCondition {
  id: string;
  index: number;
  isDefault: boolean;
  isExplicitDefault: boolean;
  label: string;
  suggestedLabel: string;
  supportsCondition: boolean;
  targetName: string;
  flow: any;
}

// 审批人类型选项
const assigneeTypes = [
  { label: '指定组织层级负责人', value: 'organizationManager' },
  { label: '所属课负责人', value: 'sectionManager' },
  { label: '上级部门负责人', value: 'departmentManager' },
  { label: '所属组织负责人（兼容）', value: 'supervisor' },
  { label: '当前组织负责人（兼容）', value: 'deptManager' },
  { label: '指定人员', value: 'username' },
  { label: '多人审批', value: 'usernames' },
  { label: '按角色审批', value: 'roleName' },
];
const conditionFields = computed(() => [
  { label: '申请部门', value: 'applicantDept' },
  { label: '申请人角色', value: 'applicantRole' },
  { label: '是否项目负责人', value: 'isProjectOwner' },
  ...organizationLevels.value.map((level) => ({
    label: `是否需要${level.name}审批`,
    value: `requiresApproval_${level.code}`,
  })),
  { label: '是否需要课级审批（兼容）', value: 'requiresSectionApproval' },
  {
    label: '是否需要部门级审批（兼容）',
    value: 'requiresDepartmentApproval',
  },
]);
const conditionOperators = computed(() => [{ label: '等于', value: '==' }]);
const conditionValueOptions = computed(() =>
  getConditionValueOptions(conditionField.value),
);

// 判断元素类型
const isUserTask = computed(() => elementType.value === 'bpmn:UserTask');
const isSequenceFlow = computed(
  () => elementType.value === 'bpmn:SequenceFlow',
);
const isGateway = computed(
  () =>
    elementType.value === 'bpmn:ExclusiveGateway' ||
    elementType.value === 'bpmn:ParallelGateway' ||
    elementType.value === 'bpmn:InclusiveGateway',
);
const isConditionGateway = computed(() =>
  gatewaySupportsConditions(elementType.value),
);
const defaultBranchCount = computed(
  () => gatewayConditions.value.filter((item) => item.isDefault).length,
);
const assigneeValueOptions = computed(() => {
  if (assigneeType.value === 'username') {
    return userOptions.value.map((user) => ({
      label: `${user.name}（${user.employeeNo}）`,
      value: userAssigneeIdentity(user.id),
    }));
  }

  if (assigneeType.value === 'usernames') {
    return userOptions.value.map((user) => ({
      label: `${user.name}（${user.employeeNo}）`,
      value: userAssigneeIdentity(user.id),
    }));
  }

  if (assigneeType.value === 'roleName') {
    return roleOptions.value.map((role) => ({
      label: `${role.name}（${role.code}）`,
      value: roleAssigneeIdentity(role.code),
    }));
  }

  if (assigneeType.value === 'organizationManager') {
    return organizationLevels.value.map((level) => ({
      label: `${level.name}负责人`,
      value: level.code,
    }));
  }

  return [];
});
const assigneeValuePlaceholder = computed(() =>
  assigneeType.value === 'roleName'
    ? '选择角色'
    : assigneeType.value === 'organizationManager'
      ? '选择目标组织层级'
      : '选择审批人员',
);

async function loadAssigneeOptions() {
  const [users, roles, departments, levels] = await Promise.all([
    hasAccessByCodes(['approval:create']) ||
    hasAccessByCodes(['material-flow:transfer'])
      ? getUserOptionsApi()
      : getUserListApi('', 1, 200).then((result) =>
          result.items.filter((user) => user.isActive),
        ),
    getRoleListApi('', 1, 200),
    getDepartmentTreeApi(),
    getOrganizationLevelsApi(),
  ]);
  userOptions.value = users;
  roleOptions.value = roles.items.filter((role) => role.isActive);
  departmentOptions.value = flattenDepartments(departments);
  organizationLevels.value = levels;
}

function flattenDepartments(
  nodes: DepartmentNode[],
  level = 0,
): { label: string; value: string }[] {
  return nodes
    .filter((node) => node.isActive)
    .flatMap((node) => [
      { label: `${'　'.repeat(level)}${node.name}`, value: node.name },
      ...flattenDepartments(node.children, level + 1),
    ]);
}

// 加载元素属性
function loadElement() {
  isLoadingElement.value = true;

  if (!props.element) {
    elementType.value = '';
    elementName.value = '';
    elementId.value = '';
    assigneeType.value = '';
    assigneeValue.value = '';
    approvalMode.value = 'any';
    conditionExpression.value = '';
    conditionField.value = 'applicantDept';
    conditionOperator.value = '==';
    conditionValue.value = '';
    gatewayConditions.value = [];
    nextTick(() => {
      isLoadingElement.value = false;
    });
    return;
  }

  const businessObject = props.element.businessObject;
  elementType.value = businessObject.$type;
  elementName.value = businessObject.name || '';
  elementId.value = businessObject.id || '';

  // 加载审批人配置（UserTask）
  if (isUserTask.value) {
    const assignee = businessObject.get('camunda:assignee');
    const candidateUsers = businessObject.get('camunda:candidateUsers');
    const candidateGroups = businessObject.get('camunda:candidateGroups');
    approvalMode.value =
      businessObject.get('camunda:approvalMode') === 'all' ? 'all' : 'any';
    const selection = loadAssigneeSelection(
      assignee,
      candidateUsers,
      candidateGroups,
    );
    assigneeType.value = selection.type;
    assigneeValue.value = selection.value;
  } else {
    assigneeType.value = '';
    assigneeValue.value = '';
    approvalMode.value = 'any';
  }

  // 加载条件表达式（SequenceFlow）
  if (isSequenceFlow.value) {
    const conditionExpr = businessObject.conditionExpression;
    if (conditionExpr) {
      conditionExpression.value = conditionExpr.body || '';
      parseConditionExpression(conditionExpression.value);
    } else {
      conditionExpression.value = '';
      conditionField.value = 'applicantDept';
      conditionOperator.value = '==';
      conditionValue.value = '';
    }
  } else {
    conditionExpression.value = '';
    conditionValue.value = '';
  }

  if (isGateway.value) {
    gatewayConditions.value = loadGatewayConditions();
  } else {
    gatewayConditions.value = [];
  }

  nextTick(() => {
    isLoadingElement.value = false;
  });
}

function parseCondition(expression: string): ParsedCondition {
  const stringMatch = expression.match(
    /^\$\{(applicantDept|applicantRole|isProjectOwner|requiresSectionApproval|requiresDepartmentApproval|requiresApproval_[a-z][a-z0-9_]{0,49})\}\s*(==|!=)\s*["'](.+)["']$/,
  );
  if (stringMatch) {
    return {
      expression,
      field: stringMatch[1] || 'applicantDept',
      operator: stringMatch[2] || '==',
      value: stringMatch[3] || '',
    };
  }

  return {
    expression,
    field: 'applicantDept',
    operator: '==',
    value: '',
  };
}

function parseConditionExpression(expression: string) {
  const parsed = parseCondition(expression);
  conditionField.value = parsed.field;
  conditionOperator.value = parsed.operator;
  conditionValue.value = parsed.value;
}

function buildExpression(value: string) {
  if (!value.trim()) return '';

  const trimmed = value.trim();
  return `\${${conditionField.value}} ${conditionOperator.value} "${trimmed}"`;
}

function buildExpressionByField(
  field: string,
  operator: string,
  value: string,
) {
  if (!value.trim()) return '';

  const trimmed = value.trim();
  return `\${${field}} ${operator} "${trimmed}"`;
}

function buildConditionExpression() {
  conditionExpression.value = buildExpression(conditionValue.value);
}

function loadGatewayConditions(): GatewayCondition[] {
  const outgoing = props.element?.outgoing || [];
  const gatewayBusinessObject = props.element?.businessObject;
  return outgoing.map((flow: any, index: number) => {
    const businessObject = flow.businessObject;
    const expression = businessObject.conditionExpression?.body || '';
    const parsed = parseCondition(expression);
    const explicitDefault = gatewayBusinessObject?.default;
    const isExplicitDefault =
      explicitDefault === businessObject ||
      explicitDefault === businessObject.id ||
      explicitDefault?.id === businessObject.id;
    const source = {
      conditionExpression: expression,
      gatewayType: elementType.value,
      index,
      isExplicitDefault,
      name: businessObject.name,
      targetId: flow.target?.id,
      targetName: flow.target?.businessObject?.name,
      targetType: flow.target?.businessObject?.$type,
    };
    const suggestedLabel = getSuggestedBranchName(source);
    return {
      ...parsed,
      flow,
      id: flow.id,
      index,
      isDefault: isDefaultGatewayBranch(source),
      isExplicitDefault,
      label: businessObject.name?.trim() || suggestedLabel,
      suggestedLabel,
      supportsCondition: gatewaySupportsConditions(elementType.value),
      targetName: getTargetNodeTitle(source),
    };
  });
}

function updateGatewayCondition(item: GatewayCondition) {
  if (isLoadingElement.value || !props.modeler) return;

  const usedSuggestedLabel = item.label.trim() === item.suggestedLabel;
  item.expression = item.supportsCondition
    ? buildExpressionByField(item.field, item.operator, item.value)
    : '';
  item.isDefault = item.supportsCondition && !item.expression.trim();
  const nextSuggestedLabel = getSuggestedBranchName({
    conditionExpression: item.expression,
    gatewayType: elementType.value,
    index: item.index,
    isExplicitDefault: item.isExplicitDefault,
    targetId: item.flow.target?.id,
    targetName: item.flow.target?.businessObject?.name,
    targetType: item.flow.target?.businessObject?.$type,
  });
  if (usedSuggestedLabel) item.label = nextSuggestedLabel;
  item.suggestedLabel = nextSuggestedLabel;
  const normalizedLabel = item.label.trim() || item.suggestedLabel;
  item.label = normalizedLabel;

  const modeling = props.modeler.get('modeling');
  const moddle = props.modeler.get('moddle');
  const businessObject = item.flow.businessObject;
  const updates: Record<string, any> = {};

  if ((businessObject.name || '') !== normalizedLabel) {
    updates.name = normalizedLabel;
  }

  if (
    item.supportsCondition &&
    businessObject.conditionExpression?.body !== item.expression
  ) {
    updates.conditionExpression = item.expression
      ? moddle.create('bpmn:FormalExpression', { body: item.expression })
      : undefined;
  }

  if (Object.keys(updates).length > 0) {
    modeling.updateProperties(item.flow, updates);
  }
}

function focusGatewayBranch(item: GatewayCondition) {
  if (!props.modeler) return;
  props.modeler.get('selection').select(item.flow);
  props.modeler.get('canvas').scrollToElement(item.flow);
}

function handleCommandStackChanged() {
  if (!isGateway.value || gatewayRefreshQueued) return;

  gatewayRefreshQueued = true;
  queueMicrotask(() => {
    gatewayRefreshQueued = false;
    if (!isGateway.value) return;
    gatewayConditions.value = loadGatewayConditions();
  });
}

function bindModelerEvents(nextModeler: any, previousModeler?: any) {
  previousModeler
    ?.get?.('eventBus')
    ?.off?.('commandStack.changed', handleCommandStackChanged);
  boundEventBus = nextModeler?.get?.('eventBus');
  boundEventBus?.on('commandStack.changed', handleCommandStackChanged);
}

function getConditionValueOptions(field: string) {
  if (field === 'applicantRole') {
    return roleOptions.value.map((role) => ({
      label: `${role.name}（${role.code}）`,
      value: role.code,
    }));
  }

  if (
    field === 'isProjectOwner' ||
    field === 'requiresSectionApproval' ||
    field === 'requiresDepartmentApproval' ||
    field.startsWith('requiresApproval_')
  ) {
    return [
      { label: '是', value: 'true' },
      { label: '否', value: 'false' },
    ];
  }

  return departmentOptions.value;
}

function conditionValuePlaceholder(field: string) {
  if (field === 'isProjectOwner') return '选择是否项目负责人';
  if (field === 'requiresSectionApproval') return '选择是否需要课级审批';
  if (field === 'requiresDepartmentApproval') return '选择是否需要部门级审批';
  if (field.startsWith('requiresApproval_')) return '选择是否需要该层级审批';
  return field === 'applicantRole' ? '选择申请人角色' : '选择申请部门';
}

function elementTypeLabel(type: string) {
  const map: Record<string, string> = {
    'bpmn:EndEvent': '结束节点',
    'bpmn:ExclusiveGateway': '条件分支',
    'bpmn:InclusiveGateway': '包容分支',
    'bpmn:ParallelGateway': '并行审批',
    'bpmn:SequenceFlow': '流转线',
    'bpmn:StartEvent': '发起节点',
    'bpmn:UserTask': '审批节点',
  };
  return map[type] || type || '-';
}

// 更新元素属性
function updateElement() {
  if (isLoadingElement.value || !props.element || !props.modeler) return;

  const modeling = props.modeler.get('modeling');
  const businessObject = props.element.businessObject;
  const updates: Record<string, any> = {};

  // 更新名称
  if (elementName.value !== businessObject.name) {
    updates.name = elementName.value;
  }

  // 更新审批人（UserTask）
  if (isUserTask.value) {
    const {
      assignee: assigneeVal,
      candidateGroups: candidateGroupsVal,
      candidateUsers: candidateUsersVal,
    } = serializeAssigneeSelection(assigneeType.value, assigneeValue.value);

    if ((businessObject.get('camunda:assignee') || '') !== assigneeVal) {
      updates['camunda:assignee'] = assigneeVal || undefined;
    }

    if (
      (businessObject.get('camunda:candidateUsers') || '') !== candidateUsersVal
    ) {
      updates['camunda:candidateUsers'] = candidateUsersVal || undefined;
    }

    if (
      (businessObject.get('camunda:candidateGroups') || '') !==
      candidateGroupsVal
    ) {
      updates['camunda:candidateGroups'] = candidateGroupsVal || undefined;
    }

    const approvalModeVal = approvalMode.value === 'all' ? 'all' : '';
    if (
      (businessObject.get('camunda:approvalMode') || '') !== approvalModeVal
    ) {
      updates['camunda:approvalMode'] = approvalModeVal || undefined;
    }
  }

  // 更新条件表达式（SequenceFlow）
  if (
    isSequenceFlow.value &&
    businessObject.conditionExpression?.body !== conditionExpression.value
  ) {
    if (conditionExpression.value.trim()) {
      const moddle = props.modeler.get('moddle');
      const conditionExpr = moddle.create('bpmn:FormalExpression', {
        body: conditionExpression.value.trim(),
      });

      updates.conditionExpression = conditionExpr;
    } else {
      updates.conditionExpression = undefined;
    }
  }

  if (Object.keys(updates).length > 0) {
    modeling.updateProperties(props.element, updates);
  }
}

// 监听元素变化
watch(() => props.element, loadElement, { immediate: true });
watch(() => props.modeler, bindModelerEvents, { immediate: true });

// 监听属性变化，实时更新
watch(
  [elementName, assigneeType, assigneeValue, approvalMode, conditionExpression],
  updateElement,
);

watch(assigneeType, (newType, oldType) => {
  if (isLoadingElement.value || newType === oldType) return;
  assigneeValue.value = newType === 'usernames' ? [] : '';
  if (newType !== 'usernames' && approvalMode.value === 'all') {
    approvalMode.value = 'any';
  }
});

watch(conditionField, () => {
  if (isLoadingElement.value) return;
  conditionOperator.value = '==';
  buildConditionExpression();
});

watch([conditionOperator, conditionValue], () => {
  if (isLoadingElement.value) return;
  buildConditionExpression();
});

onMounted(() => {
  void loadAssigneeOptions();
});

onUnmounted(() => {
  boundEventBus?.off?.('commandStack.changed', handleCommandStackChanged);
});
</script>

<template>
  <div class="bpmn-properties">
    <div class="panel-header">
      <div>
        <div class="panel-title">属性配置</div>
        <div class="panel-subtitle">按业务语言维护流程规则</div>
      </div>
    </div>

    <div v-if="!element" class="empty-state">
      <div class="empty-box">
        <div class="empty-title">未选择元素</div>
        <div class="empty-desc">
          点击画布上的节点或连线后，在这里配置名称、审批人和分支条件。
        </div>
      </div>
    </div>

    <div v-else class="properties-form">
      <ElForm label-width="100px" label-position="top" size="small">
        <section class="property-section">
          <div class="section-title">基础信息</div>

          <ElFormItem label="节点类型">
            <div class="readonly-field">
              {{ elementTypeLabel(elementType) }}
            </div>
          </ElFormItem>

          <ElFormItem label="技术标识">
            <div class="readonly-field code-field">{{ elementId }}</div>
          </ElFormItem>

          <ElFormItem label="显示名称">
            <ElInput v-model="elementName" placeholder="请输入节点名称" />
          </ElFormItem>
        </section>

        <!-- UserTask 属性 -->
        <template v-if="isUserTask">
          <section class="property-section">
            <div class="section-title">审批设置</div>

            <ElFormItem label="审批人来源">
              <ElSelect
                v-model="assigneeType"
                placeholder="选择审批人来源"
                style="width: 100%"
              >
                <ElOption
                  v-for="item in assigneeTypes"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </ElSelect>
            </ElFormItem>

            <ElFormItem
              v-if="
                assigneeType === 'username' ||
                assigneeType === 'usernames' ||
                assigneeType === 'roleName' ||
                assigneeType === 'organizationManager'
              "
              :label="
                assigneeType === 'roleName'
                  ? '审批角色'
                  : assigneeType === 'organizationManager'
                    ? '目标组织层级'
                    : '审批人员'
              "
            >
              <ElSelect
                v-model="assigneeValue"
                clearable
                filterable
                :multiple="assigneeType === 'usernames'"
                collapse-tags
                collapse-tags-tooltip
                :placeholder="assigneeValuePlaceholder"
                style="width: 100%"
              >
                <ElOption
                  v-for="item in assigneeValueOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </ElSelect>
            </ElFormItem>

            <ElFormItem label="通过规则">
              <ElRadioGroup v-model="approvalMode" size="small">
                <ElRadioButton value="any">任一人通过</ElRadioButton>
                <ElRadioButton
                  :disabled="assigneeType !== 'usernames'"
                  value="all"
                >
                  全部人通过
                </ElRadioButton>
              </ElRadioGroup>
            </ElFormItem>

            <div class="tip-box">
              <div v-if="assigneeType === 'supervisor'">
                自动解析申请人所属组织节点负责人，未配置时兼容历史直属上级。
              </div>
              <div v-else-if="assigneeType === 'organizationManager'">
                沿申请人的组织链向上查找所选层级负责人，不依赖组织名称或树深度。
              </div>
              <div v-else-if="assigneeType === 'sectionManager'">
                自动解析申请人所属课的负责人；课负责人本人申请时由条件分支跳过此节点。
              </div>
              <div v-else-if="assigneeType === 'departmentManager'">
                自动解析申请人所在课的上级部门负责人，或直属部门的负责人。
              </div>
              <div v-else-if="assigneeType === 'deptManager'">
                自动解析申请人所在部门的管理员。
              </div>
              <div v-else-if="assigneeType === 'username'">
                指定一名固定审批人。
              </div>
              <div v-else-if="assigneeType === 'usernames'">
                可选择多人；“全部人通过”表示会签。
              </div>
              <div v-else-if="assigneeType === 'roleName'">
                按唯一角色编码匹配审批人。
              </div>
              <div v-else>请选择审批人来源。</div>
            </div>
          </section>
        </template>

        <!-- SequenceFlow 属性 -->
        <template v-if="isSequenceFlow">
          <section class="property-section">
            <div class="section-title">流转条件</div>

            <ElFormItem label="条件">
              <div class="condition-builder">
                <ElSelect v-model="conditionField" style="width: 132px">
                  <ElOption
                    v-for="item in conditionFields"
                    :key="item.value"
                    :label="item.label"
                    :value="item.value"
                  />
                </ElSelect>
                <ElSelect v-model="conditionOperator" style="width: 82px">
                  <ElOption
                    v-for="item in conditionOperators"
                    :key="item.value"
                    :label="item.label"
                    :value="item.value"
                  />
                </ElSelect>
                <ElSelect
                  v-model="conditionValue"
                  clearable
                  filterable
                  :placeholder="conditionValuePlaceholder(conditionField)"
                  style="width: 100%"
                >
                  <ElOption
                    v-for="item in conditionValueOptions"
                    :key="item.value"
                    :label="item.label"
                    :value="item.value"
                  />
                </ElSelect>
              </div>
              <ElInput
                v-model="conditionExpression"
                class="expression-input"
                type="textarea"
                :rows="3"
                placeholder='如: ${applicantDept} == "信息部"'
              />
            </ElFormItem>

            <div class="tip-box">
              条件为空时表示默认流向；支持申请部门、申请人角色和组织审批条件。
            </div>
          </section>
        </template>

        <!-- Gateway 属性 -->
        <template v-if="isGateway">
          <section class="property-section">
            <div class="section-title">分支说明</div>

            <div class="tip-box">
              <div v-if="elementType === 'bpmn:ExclusiveGateway'">
                根据条件只选择一条分支继续执行。
              </div>
              <div v-else-if="elementType === 'bpmn:ParallelGateway'">
                所有连出的分支会同时执行。
              </div>
              <div v-else-if="elementType === 'bpmn:InclusiveGateway'">
                执行所有满足条件的分支。
              </div>
            </div>
          </section>

          <section class="property-section branch-section">
            <div class="section-title-row">
              <div class="section-title">出线分支</div>
              <ElTag effect="plain" size="small">
                {{ gatewayConditions.length }} 条
              </ElTag>
            </div>

            <div
              v-if="isConditionGateway && defaultBranchCount === 1"
              class="branch-health is-ready"
            >
              已设置 1 条默认分支，其他条件均不满足时从默认分支继续。
            </div>
            <div
              v-else-if="isConditionGateway"
              class="branch-health is-warning"
            >
              当前有 {{ defaultBranchCount }} 条默认分支，应且只能保留 1
              条无条件分支。
            </div>
            <div v-else class="branch-health is-parallel">
              并行网关会同时进入全部分支，无需配置条件。
            </div>

            <div v-if="gatewayConditions.length === 0" class="empty-branch">
              请先从该网关连出审批节点或结束节点。
            </div>

            <div
              v-for="item in gatewayConditions"
              :key="item.id"
              class="branch-condition"
            >
              <div class="branch-card-header">
                <div class="branch-identity">
                  <span class="branch-number">{{ item.index + 1 }}</span>
                  <div>
                    <strong>{{ item.label }}</strong>
                    <span
                      >{{ elementName || '当前网关' }} →
                      {{ item.targetName }}</span
                    >
                  </div>
                </div>
                <ElButton
                  link
                  size="small"
                  type="primary"
                  @click="focusGatewayBranch(item)"
                >
                  定位连线
                </ElButton>
              </div>

              <div class="branch-tags">
                <ElTag
                  v-if="item.isDefault"
                  effect="light"
                  size="small"
                  type="success"
                >
                  默认分支
                </ElTag>
                <ElTag
                  v-else-if="item.supportsCondition"
                  effect="plain"
                  size="small"
                >
                  条件分支
                </ElTag>
                <ElTag v-else effect="plain" size="small" type="warning">
                  并行分支
                </ElTag>
                <span>流向：{{ item.targetName }}</span>
              </div>

              <ElFormItem label="画布标签">
                <ElInput
                  v-model="item.label"
                  placeholder="请输入连线上显示的分支名称"
                  @change="updateGatewayCondition(item)"
                />
                <div class="field-help">此名称会直接显示在画布连线上。</div>
              </ElFormItem>

              <template v-if="item.supportsCondition">
                <ElFormItem v-if="item.isDefault" label="流转规则">
                  <div class="default-rule">
                    无需设置条件，未命中其他分支时自动进入此分支。
                  </div>
                </ElFormItem>
                <ElFormItem label="分支条件（留空表示默认）">
                  <div class="condition-builder">
                    <ElSelect
                      v-model="item.field"
                      style="width: 132px"
                      @change="updateGatewayCondition(item)"
                    >
                      <ElOption
                        v-for="field in conditionFields"
                        :key="field.value"
                        :label="field.label"
                        :value="field.value"
                      />
                    </ElSelect>
                    <ElSelect
                      v-model="item.operator"
                      style="width: 82px"
                      @change="updateGatewayCondition(item)"
                    >
                      <ElOption label="等于" value="==" />
                    </ElSelect>
                    <ElSelect
                      v-model="item.value"
                      clearable
                      filterable
                      :placeholder="conditionValuePlaceholder(item.field)"
                      style="width: 100%"
                      @change="updateGatewayCondition(item)"
                    >
                      <ElOption
                        v-for="option in getConditionValueOptions(item.field)"
                        :key="option.value"
                        :label="option.label"
                        :value="option.value"
                      />
                    </ElSelect>
                  </div>
                </ElFormItem>

                <ElFormItem v-if="item.expression" label="表达式">
                  <div class="readonly-field code-field branch-expression">
                    {{ item.expression || '尚未设置条件' }}
                  </div>
                </ElFormItem>
              </template>
            </div>
          </section>
        </template>
      </ElForm>
    </div>
  </div>
</template>

<style scoped>
.bpmn-properties {
  height: 100%;
  overflow-y: auto;
  color: var(--workflow-text, var(--el-text-color-primary));
  background: var(--workflow-panel-bg, var(--el-bg-color));
}

.panel-header {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 52px;
  padding: 10px 14px;
  background: var(--workflow-panel-bg, var(--el-bg-color));
  border-bottom: 1px solid var(--workflow-border, var(--el-border-color));
}

.panel-title {
  color: var(--workflow-title, var(--el-text-color-primary));
  font-size: 14px;
  font-weight: 600;
}

.panel-subtitle {
  margin-top: 2px;
  color: var(--workflow-muted, var(--el-text-color-secondary));
  font-size: 12px;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 220px;
  padding: 16px;
}

.empty-box {
  width: 100%;
  padding: 18px;
  text-align: center;
  background: var(--workflow-panel-soft-bg, var(--el-fill-color-light));
  border: 1px dashed var(--workflow-border-dashed, var(--el-border-color));
  border-radius: 4px;
}

.empty-title {
  color: var(--workflow-title, var(--el-text-color-primary));
  font-weight: 600;
}

.empty-desc {
  margin-top: 6px;
  color: var(--workflow-muted, var(--el-text-color-secondary));
  font-size: 12px;
  line-height: 18px;
}

.properties-form {
  padding: 10px 12px 16px;
  background: var(--workflow-panel-bg, var(--el-bg-color));
}

.property-section {
  padding: 10px;
  margin-bottom: 10px;
  background: var(--workflow-section-bg, var(--el-fill-color-blank));
  border: 1px solid var(--workflow-border-soft, var(--el-border-color));
  border-radius: 4px;
}

.section-title {
  display: flex;
  align-items: center;
  margin-bottom: 10px;
  color: var(--workflow-title, var(--el-text-color-primary));
  font-size: 13px;
  font-weight: 600;
}

.section-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.section-title-row .section-title {
  margin-bottom: 0;
}

.section-title::before {
  width: 3px;
  height: 14px;
  margin-right: 6px;
  content: '';
  background: var(--workflow-primary, var(--el-color-primary));
  border-radius: 2px;
}

.readonly-field {
  min-height: 28px;
  padding: 5px 8px;
  color: var(--workflow-text, var(--el-text-color-primary));
  line-height: 18px;
  background: var(--workflow-card-bg, var(--el-fill-color-light));
  border: 1px solid var(--workflow-border, var(--el-border-color));
  border-radius: 4px;
}

.code-field {
  font-family: Consolas, 'Microsoft YaHei', monospace;
  font-size: 12px;
}

.condition-builder {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 78px;
  width: 100%;
  gap: 6px;
}

.condition-builder > :deep(.el-select) {
  width: 100% !important;
  min-width: 0;
}

.condition-builder > :deep(.el-select:nth-child(3)) {
  grid-column: 1 / -1;
}

.expression-input {
  margin-top: 8px;
}

.tip-box {
  padding: 8px 10px;
  color: var(--workflow-muted-strong, var(--el-text-color-regular));
  font-size: 12px;
  line-height: 18px;
  background: var(--workflow-card-bg, var(--el-fill-color-light));
  border: 1px solid var(--workflow-border, var(--el-border-color));
  border-radius: 4px;
}

.branch-condition {
  padding: 12px;
  margin-top: 10px;
  background: var(--workflow-panel-bg, var(--el-bg-color));
  border: 1px solid var(--workflow-border-soft, var(--el-border-color));
  border-radius: 6px;
}

.branch-card-header,
.branch-identity,
.branch-tags {
  display: flex;
  align-items: center;
}

.branch-card-header {
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.branch-identity {
  min-width: 0;
  gap: 8px;
}

.branch-identity > div {
  min-width: 0;
}

.branch-identity strong,
.branch-identity span {
  display: block;
}

.branch-identity strong {
  overflow: hidden;
  color: var(--workflow-title, var(--el-text-color-primary));
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.branch-identity span {
  margin-top: 2px;
  overflow: hidden;
  color: var(--workflow-muted, var(--el-text-color-secondary));
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.branch-number {
  flex: 0 0 26px;
  width: 26px;
  height: 26px;
  color: var(--workflow-primary, var(--el-color-primary));
  font-size: 12px;
  font-weight: 700;
  line-height: 26px;
  text-align: center;
  background: var(--workflow-card-icon-bg, var(--el-color-primary-light-9));
  border-radius: 50%;
}

.branch-tags {
  padding: 7px 8px;
  margin-bottom: 10px;
  gap: 6px;
  color: var(--workflow-muted-strong, var(--el-text-color-regular));
  font-size: 11px;
  background: var(--workflow-card-bg, var(--el-fill-color-light));
  border-radius: 4px;
}

.branch-health {
  padding: 8px 10px;
  margin-bottom: 10px;
  font-size: 12px;
  line-height: 18px;
  border: 1px solid;
  border-radius: 4px;
}

.branch-health.is-ready {
  color: var(--el-color-success-dark-2);
  background: var(--el-color-success-light-9);
  border-color: var(--el-color-success-light-7);
}

.branch-health.is-warning {
  color: var(--el-color-danger-dark-2);
  background: var(--el-color-danger-light-9);
  border-color: var(--el-color-danger-light-7);
}

.branch-health.is-parallel {
  color: var(--el-color-warning-dark-2);
  background: var(--el-color-warning-light-9);
  border-color: var(--el-color-warning-light-7);
}

.field-help {
  margin-top: 4px;
  color: var(--workflow-muted, var(--el-text-color-secondary));
  font-size: 11px;
  line-height: 16px;
}

.default-rule {
  width: 100%;
  padding: 8px;
  color: var(--el-color-success-dark-2);
  font-size: 12px;
  line-height: 18px;
  background: var(--el-color-success-light-9);
  border: 1px dashed var(--el-color-success-light-5);
  border-radius: 4px;
}

.branch-expression {
  width: 100%;
  word-break: break-all;
}

.empty-branch {
  padding: 10px;
  color: var(--workflow-muted-strong, var(--el-text-color-regular));
  font-size: 12px;
  background: var(--workflow-card-bg, var(--el-fill-color-light));
  border: 1px dashed var(--workflow-border-dashed, var(--el-border-color));
  border-radius: 4px;
}

:deep(.el-form-item) {
  margin-bottom: 12px;
}

:deep(.el-form-item__label) {
  color: var(--workflow-muted-strong, var(--el-text-color-regular));
  font-size: 12px;
}

:deep(.el-input__wrapper),
:deep(.el-select__wrapper),
:deep(.el-textarea__inner) {
  color: var(--workflow-text, var(--el-text-color-primary));
  background-color: var(--workflow-card-bg, var(--el-fill-color-light));
  border: 1px solid var(--workflow-border, var(--el-border-color));
  box-shadow: none;
}

:deep(.el-input__wrapper:hover),
:deep(.el-select__wrapper:hover),
:deep(.el-textarea__inner:hover) {
  border-color: var(--workflow-primary, var(--el-color-primary));
  box-shadow: none;
}

:deep(.el-input__wrapper.is-focus),
:deep(.el-select__wrapper.is-focused),
:deep(.el-textarea__inner:focus) {
  border-color: var(--workflow-primary, var(--el-color-primary));
  box-shadow: 0 0 0 1px var(--workflow-primary, var(--el-color-primary));
}

:deep(.el-input__inner),
:deep(.el-select__placeholder),
:deep(.el-textarea__inner) {
  color: var(--workflow-text, var(--el-text-color-primary));
}

:deep(.el-select__selected-item),
:deep(.el-select__placeholder) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.el-radio-button__inner) {
  color: var(--workflow-muted-strong, var(--el-text-color-regular));
  background: var(--workflow-card-bg, var(--el-fill-color-light));
  border-color: var(--workflow-border, var(--el-border-color));
}

:deep(.el-radio-button__original-radio:checked + .el-radio-button__inner) {
  color: #ffffff;
  background: var(--workflow-primary, var(--el-color-primary));
  border-color: var(--workflow-primary, var(--el-color-primary));
}

:global(.dark) .property-section,
:global(.dark) .branch-condition,
:global(.dark) .readonly-field,
:global(.dark) .tip-box,
:global(.dark) .empty-branch,
:global(.dark) :deep(.el-input__wrapper),
:global(.dark) :deep(.el-select__wrapper),
:global(.dark) :deep(.el-textarea__inner),
:global(.dark) :deep(.el-radio-button__inner) {
  border-color: var(--workflow-border, var(--el-border-color));
}
</style>
