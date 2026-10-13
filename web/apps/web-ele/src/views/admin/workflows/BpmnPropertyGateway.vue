<script lang="ts" setup>
import {
  ElButton,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
  ElTag,
} from 'element-plus';

export type GatewayBranch = {
  expression: string;
  field: string;
  flow: any;
  id: string;
  index: number;
  isDefault: boolean;
  isExplicitDefault: boolean;
  label: string;
  operator: string;
  suggestedLabel: string;
  supportsCondition: boolean;
  targetName: string;
  value: string;
};

defineProps<{
  conditionFields: { label: string; value: string }[];
  defaultBranchCount: number;
  elementName: string;
  elementType: string;
  gatewayConditions: GatewayBranch[];
  getConditionValueOptions: (
    field: string,
  ) => { label: string; value: string }[];
  conditionValuePlaceholder: (field: string) => string;
  isConditionGateway: boolean;
}>();

const emit = defineEmits<{
  focusBranch: [item: GatewayBranch];
  updateCondition: [item: GatewayBranch];
}>();
</script>

<template>
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
    <div v-else-if="isConditionGateway" class="branch-health is-warning">
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
            <span>
              {{ elementName || '当前网关' }} →
              {{ item.targetName }}
            </span>
          </div>
        </div>
        <ElButton
          link
          size="small"
          type="primary"
          @click="emit('focusBranch', item)"
        >
          定位连线
        </ElButton>
      </div>

      <div class="branch-tags">
        <ElTag v-if="item.isDefault" effect="light" size="small" type="success">
          默认分支
        </ElTag>
        <ElTag v-else-if="item.supportsCondition" effect="plain" size="small">
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
          @change="emit('updateCondition', item)"
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
              @change="emit('updateCondition', item)"
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
              @change="emit('updateCondition', item)"
            >
              <ElOption label="等于" value="==" />
            </ElSelect>
            <ElSelect
              v-model="item.value"
              :placeholder="conditionValuePlaceholder(item.field)"
              clearable
              filterable
              style="width: 100%"
              @change="emit('updateCondition', item)"
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

<style scoped src="./bpmn-properties.css"></style>
