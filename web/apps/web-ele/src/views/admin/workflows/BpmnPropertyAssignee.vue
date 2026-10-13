<script lang="ts" setup>
import type { AssigneeType } from './assignee-identities';

import {
  ElFormItem,
  ElOption,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
} from 'element-plus';

defineProps<{
  assigneeOptionsLoading: boolean;
  assigneeTypeOptions: { label: string; value: string }[];
  assigneeValueOptions: { label: string; value: string }[];
  assigneeValuePlaceholder: string;
}>();

const emit = defineEmits<{
  loadOptions: [keyword: string];
}>();
const assigneeType = defineModel<AssigneeType>('assigneeType', {
  required: true,
});
const assigneeValue = defineModel<string | string[]>('assigneeValue', {
  required: true,
});
const approvalMode = defineModel<'all' | 'any'>('approvalMode', {
  required: true,
});
</script>

<template>
  <section class="property-section">
    <div class="section-title">审批设置</div>

    <ElFormItem label="审批人来源">
      <ElSelect
        v-model="assigneeType"
        placeholder="选择审批人来源"
        style="width: 100%"
      >
        <ElOption
          v-for="item in assigneeTypeOptions"
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
        :loading="assigneeOptionsLoading"
        :multiple="assigneeType === 'usernames'"
        :placeholder="assigneeValuePlaceholder"
        :remote="assigneeType === 'username' || assigneeType === 'usernames'"
        :remote-method="(keyword: string) => emit('loadOptions', keyword)"
        clearable
        collapse-tags
        collapse-tags-tooltip
        filterable
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
        <ElRadioButton :disabled="assigneeType !== 'usernames'" value="all">
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
      <div v-else-if="assigneeType === 'username'">指定一名固定审批人。</div>
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

<style scoped src="./bpmn-properties.css"></style>
