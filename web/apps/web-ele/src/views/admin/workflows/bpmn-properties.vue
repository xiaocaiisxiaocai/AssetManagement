<script lang="ts" setup>
import type { RoleDto } from '#/api/role';
import type { UserDto } from '#/api/user';
import type { DepartmentNode } from '#/api/base-data';

import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { getDepartmentTreeApi } from '#/api/base-data';
import { getRoleListApi } from '#/api/role';
import { getUserListApi } from '#/api/user';
import {
  ElForm,
  ElFormItem,
  ElInput,
  ElSelect,
  ElOption,
  ElDivider,
  ElText,
  ElRadioGroup,
  ElRadioButton,
} from 'element-plus';

defineOptions({ name: 'BpmnProperties' });

interface Props {
  element: any; // BPMN 元素
  modeler: any; // BPMN Modeler 实例
}

const props = defineProps<Props>();

const elementType = ref('');
const elementName = ref('');
const elementId = ref('');

// 审批人类型
const assigneeType = ref('');
const assigneeValue = ref<string | string[]>('');
const approvalMode = ref<'all' | 'any'>('any');
const userOptions = ref<UserDto[]>([]);
const roleOptions = ref<RoleDto[]>([]);
const departmentOptions = ref<{ label: string; value: string }[]>([]);

// 条件表达式
const conditionExpression = ref('');
const conditionField = ref('applicantDept');
const conditionOperator = ref('==');
const conditionValue = ref('');
const gatewayConditions = ref<GatewayCondition[]>([]);
const isLoadingElement = ref(false);

interface ParsedCondition {
  expression: string;
  field: string;
  operator: string;
  value: string;
}

interface GatewayCondition extends ParsedCondition {
  id: string;
  label: string;
  targetName: string;
  flow: any;
}

// 审批人类型选项
const assigneeTypes = [
  { label: '直属主管', value: 'supervisor' },
  { label: '部门经理', value: 'deptManager' },
  { label: '指定用户名', value: 'username' },
  { label: '多个指定用户', value: 'usernames' },
  { label: '角色名称', value: 'roleName' },
];
const conditionFields = [
  { label: '申请部门', value: 'applicantDept' },
];
const conditionOperators = computed(() => [{ label: '等于', value: '==' }]);

// 判断元素类型
const isUserTask = computed(() => elementType.value === 'bpmn:UserTask');
const isSequenceFlow = computed(() => elementType.value === 'bpmn:SequenceFlow');
const isGateway = computed(() =>
  elementType.value === 'bpmn:ExclusiveGateway' ||
  elementType.value === 'bpmn:ParallelGateway' ||
  elementType.value === 'bpmn:InclusiveGateway'
);
const isExclusiveGateway = computed(() => elementType.value === 'bpmn:ExclusiveGateway');
const assigneeValueOptions = computed(() => {
  if (assigneeType.value === 'username') {
    return userOptions.value.map((user) => ({
      label: `${user.name}（${user.employeeNo}）`,
      value: user.name,
    }));
  }

  if (assigneeType.value === 'usernames') {
    return userOptions.value.map((user) => ({
      label: `${user.name}（${user.employeeNo}）`,
      value: user.name,
    }));
  }

  if (assigneeType.value === 'roleName') {
    return roleOptions.value.map((role) => ({
      label: `${role.name}（${role.code}）`,
      value: role.name,
    }));
  }

  return [];
});
const assigneeValuePlaceholder = computed(() =>
  assigneeType.value === 'roleName' ? '选择角色名称' : '选择用户名'
);

async function loadAssigneeOptions() {
  const [users, roles, departments] = await Promise.all([
    getUserListApi('', 1, 200),
    getRoleListApi('', 1, 200),
    getDepartmentTreeApi(),
  ]);
  userOptions.value = users.items.filter((user) => user.isActive);
  roleOptions.value = roles.items.filter((role) => role.isActive);
  departmentOptions.value = flattenDepartments(departments);
}

function flattenDepartments(nodes: DepartmentNode[], level = 0): { label: string; value: string }[] {
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
    approvalMode.value = businessObject.get('camunda:approvalMode') === 'all' ? 'all' : 'any';
    if (assignee) {
      // 解析审批人类型
      if (assignee === 'supervisor') {
        assigneeType.value = 'supervisor';
      } else if (assignee === 'deptManager') {
        assigneeType.value = 'deptManager';
      } else {
        assigneeType.value = 'username';
        assigneeValue.value = assignee;
      }
    } else if (candidateUsers) {
      assigneeType.value = 'usernames';
      assigneeValue.value = candidateUsers
        .split(',')
        .map((item: string) => item.trim())
        .filter(Boolean);
    } else if (candidateGroups) {
      assigneeType.value = 'roleName';
      assigneeValue.value = candidateGroups;
    } else {
      assigneeType.value = '';
      assigneeValue.value = '';
    }
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

  if (isExclusiveGateway.value) {
    gatewayConditions.value = loadGatewayConditions();
  } else {
    gatewayConditions.value = [];
  }

  nextTick(() => {
    isLoadingElement.value = false;
  });
}

function parseCondition(expression: string): ParsedCondition {
  const deptMatch = expression.match(/^\$\{applicantDept\}\s*==\s*["'](.+)["']$/);
  if (deptMatch) {
    return {
      expression,
      field: 'applicantDept',
      operator: '==',
      value: deptMatch[1] || '',
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
  return `\${applicantDept} == "${trimmed}"`;
}

function buildConditionExpression() {
  conditionExpression.value = buildExpression(conditionValue.value);
}

function loadGatewayConditions(): GatewayCondition[] {
  const outgoing = props.element?.outgoing || [];
  return outgoing.map((flow: any, index: number) => {
    const businessObject = flow.businessObject;
    const expression = businessObject.conditionExpression?.body || '';
    const parsed = parseCondition(expression);
    const targetName = flow.target?.businessObject?.name || flow.target?.id || '未连接节点';
    return {
      ...parsed,
      flow,
      id: flow.id,
      label: businessObject.name || `分支 ${index + 1}`,
      targetName,
    };
  });
}

function updateGatewayCondition(item: GatewayCondition) {
  if (isLoadingElement.value || !props.modeler) return;

  item.expression = buildExpression(item.value);

  const modeling = props.modeler.get('modeling');
  const moddle = props.modeler.get('moddle');
  const businessObject = item.flow.businessObject;
  const updates: Record<string, any> = {};

  if ((businessObject.name || '') !== item.label) {
    updates.name = item.label || undefined;
  }

  if (businessObject.conditionExpression?.body !== item.expression) {
    updates.conditionExpression = item.expression
      ? moddle.create('bpmn:FormalExpression', { body: item.expression })
      : undefined;
  }

  if (Object.keys(updates).length > 0) {
    modeling.updateProperties(item.flow, updates);
  }
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
    let assigneeVal = '';
    let candidateUsersVal = '';
    let candidateGroupsVal = '';

    if (assigneeType.value === 'supervisor' || assigneeType.value === 'deptManager') {
      assigneeVal = assigneeType.value;
    } else if (assigneeType.value === 'username') {
      assigneeVal = typeof assigneeValue.value === 'string' ? assigneeValue.value : '';
    } else if (assigneeType.value === 'usernames') {
      candidateUsersVal = Array.isArray(assigneeValue.value)
        ? assigneeValue.value.join(',')
        : assigneeValue.value;
    } else if (assigneeType.value === 'roleName') {
      candidateGroupsVal = typeof assigneeValue.value === 'string' ? assigneeValue.value : '';
    }

    if ((businessObject.get('camunda:assignee') || '') !== assigneeVal) {
      updates['camunda:assignee'] = assigneeVal || undefined;
    }

    if ((businessObject.get('camunda:candidateUsers') || '') !== candidateUsersVal) {
      updates['camunda:candidateUsers'] = candidateUsersVal || undefined;
    }

    if ((businessObject.get('camunda:candidateGroups') || '') !== candidateGroupsVal) {
      updates['camunda:candidateGroups'] = candidateGroupsVal || undefined;
    }

    const approvalModeVal = approvalMode.value === 'all' ? 'all' : '';
    if ((businessObject.get('camunda:approvalMode') || '') !== approvalModeVal) {
      updates['camunda:approvalMode'] = approvalModeVal || undefined;
    }
  }

  // 更新条件表达式（SequenceFlow）
  if (isSequenceFlow.value && businessObject.conditionExpression?.body !== conditionExpression.value) {
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

// 监听属性变化，实时更新
watch([elementName, assigneeType, assigneeValue, approvalMode, conditionExpression], updateElement);

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
</script>

<template>
  <div class="bpmn-properties">
    <div v-if="!element" class="empty-state">
      <ElText type="info">请选择一个元素查看属性</ElText>
    </div>

    <div v-else class="properties-form">
      <ElForm label-width="100px" label-position="top" size="small">
        <!-- 基础信息 -->
        <ElDivider content-position="left">基础信息</ElDivider>

        <ElFormItem label="元素类型">
          <ElText>{{ elementType }}</ElText>
        </ElFormItem>

        <ElFormItem label="元素 ID">
          <ElText>{{ elementId }}</ElText>
        </ElFormItem>

        <ElFormItem label="名称">
          <ElInput v-model="elementName" placeholder="请输入名称" />
        </ElFormItem>

        <!-- UserTask 属性 -->
        <template v-if="isUserTask">
          <ElDivider content-position="left">审批人配置</ElDivider>

          <ElFormItem label="审批人类型">
            <ElSelect v-model="assigneeType" placeholder="选择审批人类型" style="width: 100%">
              <ElOption
                v-for="item in assigneeTypes"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>

          <ElFormItem
            v-if="assigneeType === 'username' || assigneeType === 'usernames' || assigneeType === 'roleName'"
            :label="assigneeType === 'roleName' ? '角色名称' : '用户名'"
          >
            <ElSelect
              v-model="assigneeValue"
              allow-create
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

          <ElFormItem label="签核方式">
            <ElRadioGroup v-model="approvalMode" size="small">
              <ElRadioButton label="any">任一人通过</ElRadioButton>
              <ElRadioButton :disabled="assigneeType !== 'usernames'" label="all">
                全部人通过
              </ElRadioButton>
            </ElRadioGroup>
          </ElFormItem>

          <ElFormItem label="说明">
            <ElText type="info" size="small">
              <div v-if="assigneeType === 'supervisor'">直属主管：自动解析为申请人的上级</div>
              <div v-else-if="assigneeType === 'deptManager'">部门经理：自动解析为申请人所在部门的管理员</div>
              <div v-else-if="assigneeType === 'username'">指定用户：填写用户姓名</div>
              <div v-else-if="assigneeType === 'usernames'">多个指定用户：可选择多人；“全部人通过”即会签</div>
              <div v-else-if="assigneeType === 'roleName'">角色：填写角色名称（如 系统管理员、仓库管理员）</div>
              <div v-else>请选择审批人类型</div>
            </ElText>
          </ElFormItem>
        </template>

        <!-- SequenceFlow 属性 -->
        <template v-if="isSequenceFlow">
          <ElDivider content-position="left">条件表达式</ElDivider>

          <ElFormItem label="条件">
            <div class="condition-builder">
              <ElSelect v-model="conditionField" style="width: 100px">
                <ElOption
                  v-for="item in conditionFields"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </ElSelect>
              <ElSelect v-model="conditionOperator" style="width: 96px">
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
                placeholder="选择申请部门"
                style="width: 100%"
              >
                <ElOption
                  v-for="item in departmentOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </ElSelect>
            </div>
            <ElInput
              v-model="conditionExpression"
              type="textarea"
              :rows="3"
              placeholder='如: ${applicantDept} == "信息部"'
              style="margin-top: 8px"
            />
          </ElFormItem>

          <ElFormItem label="说明">
            <ElText type="info" size="small">
              <div>支持表达式：</div>
              <div>• ${`${applicantDept}`} == "信息部" - 申请部门判断</div>
              <div>• 留空表示默认流向</div>
            </ElText>
          </ElFormItem>
        </template>

        <!-- Gateway 属性 -->
        <template v-if="isGateway">
          <ElDivider content-position="left">网关说明</ElDivider>

          <ElFormItem label="类型">
            <ElText type="info" size="small">
              <div v-if="elementType === 'bpmn:ExclusiveGateway'">
                <strong>排他网关：</strong>根据条件选择一条分支执行
              </div>
              <div v-else-if="elementType === 'bpmn:ParallelGateway'">
                <strong>并行网关：</strong>所有分支同时执行
              </div>
              <div v-else-if="elementType === 'bpmn:InclusiveGateway'">
                <strong>包容网关：</strong>执行所有满足条件的分支
              </div>
            </ElText>
          </ElFormItem>

          <template v-if="isExclusiveGateway">
            <ElDivider content-position="left">分支条件</ElDivider>

            <div v-if="gatewayConditions.length === 0" class="empty-branch">
              <ElText type="info" size="small">请先从条件网关连出分支</ElText>
            </div>

            <div
              v-for="item in gatewayConditions"
              :key="item.id"
              class="branch-condition"
            >
              <ElFormItem label="分支名称">
                <ElInput
                  v-model="item.label"
                  placeholder="如：信息部分支"
                  @change="updateGatewayCondition(item)"
                />
              </ElFormItem>

              <ElFormItem label="流向">
                <ElText type="info">{{ item.targetName }}</ElText>
              </ElFormItem>

              <ElFormItem label="条件">
                <div class="condition-builder">
                  <ElSelect
                    v-model="item.field"
                    style="width: 100px"
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
                    style="width: 96px"
                    @change="updateGatewayCondition(item)"
                  >
                    <ElOption
                      v-for="operator in [{ label: '等于', value: '==' }]"
                      :key="operator.value"
                      :label="operator.label"
                      :value="operator.value"
                    />
                  </ElSelect>
                  <ElSelect
                    v-model="item.value"
                    clearable
                    filterable
                    placeholder="选择申请部门"
                    style="width: 100%"
                    @change="updateGatewayCondition(item)"
                  >
                    <ElOption
                      v-for="department in departmentOptions"
                      :key="department.value"
                      :label="department.label"
                      :value="department.value"
                    />
                  </ElSelect>
                </div>
              </ElFormItem>

              <ElFormItem label="表达式">
                <ElInput
                  v-model="item.expression"
                  :rows="2"
                  readonly
                  type="textarea"
                />
              </ElFormItem>
            </div>
          </template>
        </template>
      </ElForm>
    </div>
  </div>
</template>

<style scoped>
.bpmn-properties {
  height: 100%;
  overflow-y: auto;
  padding: 10px;
  background: #fafafa;
  border-left: 1px solid #ddd;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
}

.properties-form {
  background: #fff;
  padding: 15px;
  border-radius: 4px;
}

:deep(.el-divider__text) {
  font-weight: 600;
  font-size: 13px;
}

.condition-builder {
  display: flex;
  width: 100%;
  gap: 6px;
}

.branch-condition {
  padding: 10px;
  margin-bottom: 10px;
  background: #f7f8fa;
  border: 1px solid #e5e7eb;
  border-radius: 4px;
}

.empty-branch {
  padding: 10px;
  background: #f7f8fa;
  border-radius: 4px;
}
</style>
