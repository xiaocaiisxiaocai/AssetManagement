<script lang="ts" setup>
import type { AuditListQuery } from './audit-query';

import type { AuditLogRow } from '#/api/report';

import {
  ElDescriptions,
  ElDescriptionsItem,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTooltip,
} from 'element-plus';

import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import { formatDateTime } from '#/utils/date-format';

import {
  actionType,
  actionTypeLabel,
  formatDuration,
  formatValue,
  moduleLabel,
  parseDetail,
} from './audit-table-format';

defineProps<{
  loading: boolean;
  pageSizeOptions: number[];
  rows: AuditLogRow[];
  total: number;
}>();

const emit = defineEmits<{
  pageChange: [];
  search: [];
}>();
const query = defineModel<AuditListQuery>('query', { required: true });

const actionFilterOptions = [
  { label: '新增', value: 'POST' },
  { label: '修改', value: 'PUT' },
  { label: '软删除', value: 'soft_delete' },
  { label: '彻底删除', value: 'purge' },
  { label: '其他删除', value: 'DELETE' },
  { label: '催办', value: 'remind' },
];
</script>

<template>
  <div class="table-panel-with-toolbar page-fill table-scroll">
    <ElTable :data="rows" border height="100%" v-loading="loading">
      <ElTableColumn type="expand">
        <template #default="{ row }">
          <div class="audit-detail">
            <ElDescriptions :column="3" border size="small">
              <ElDescriptionsItem label="结果">
                <ElTag
                  :type="parseDetail(row.detail).success ? 'success' : 'danger'"
                  size="small"
                >
                  {{ parseDetail(row.detail).success ? '成功' : '失败' }}
                </ElTag>
              </ElDescriptionsItem>
              <ElDescriptionsItem label="状态码">
                {{ parseDetail(row.detail).statusCode ?? '-' }}
              </ElDescriptionsItem>
              <ElDescriptionsItem label="耗时">
                {{ formatDuration(row.durationMs) }}
              </ElDescriptionsItem>
              <ElDescriptionsItem :span="2" label="请求">
                {{ row.summary }}
              </ElDescriptionsItem>
              <ElDescriptionsItem label="IP">
                {{ row.ip || '-' }}
              </ElDescriptionsItem>
              <ElDescriptionsItem :span="3" label="客户端">
                {{ row.userAgent || '-' }}
              </ElDescriptionsItem>
            </ElDescriptions>

            <div v-if="parseDetail(row.detail).error" class="audit-error">
              错误信息:{{ parseDetail(row.detail).error }}
            </div>

            <div class="audit-changes">
              <div class="audit-changes-title">字段变更</div>
              <ElTable
                v-if="parseDetail(row.detail).changes.length > 0"
                :data="parseDetail(row.detail).changes"
                border
                size="small"
              >
                <ElTableColumn label="字段" min-width="160" prop="field" />
                <ElTableColumn label="原值" min-width="220">
                  <template #default="{ row: change }">
                    <span class="audit-value-before">{{
                      formatValue(change.before)
                    }}</span>
                  </template>
                </ElTableColumn>
                <ElTableColumn label="新值" min-width="220">
                  <template #default="{ row: change }">
                    <span class="audit-value-after">{{
                      formatValue(change.after)
                    }}</span>
                  </template>
                </ElTableColumn>
              </ElTable>
              <div v-else class="audit-changes-empty">
                无字段级变更(该操作未涉及可追踪实体或属性未变化)
              </div>
            </div>
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn label="时间" min-width="170">
        <template #default="{ row }">
          {{ formatDateTime(row.occurredAt, { seconds: true }) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="操作人" min-width="120">
        <template #default="{ row }">
          {{ row.userName || row.userId || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="操作" width="120">
        <template #header>
          <HeaderMultiFilter
            v-model="query.actionTypes"
            :options="actionFilterOptions"
            label="操作"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          <ElTag :type="actionType(row.actionType)" size="small">
            {{ actionTypeLabel(row.actionType) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn class-name="hide-on-mobile" label="模块" min-width="120">
        <template #default="{ row }">
          <ElTooltip
            v-if="row.targetType"
            :content="row.targetType"
            placement="top"
          >
            <span>{{ moduleLabel(row.targetType) }}</span>
          </ElTooltip>
          <span v-else>-</span>
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="目标ID"
        min-width="90"
        prop="targetId"
      />
      <ElTableColumn
        label="摘要"
        min-width="240"
        prop="summary"
        show-overflow-tooltip
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="IP"
        min-width="130"
        prop="ip"
      />
      <ElTableColumn
        align="right"
        class-name="hide-on-mobile"
        label="耗时"
        width="96"
      >
        <template #default="{ row }">
          {{ formatDuration(row.durationMs) }}
        </template>
      </ElTableColumn>
    </ElTable>

    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ total }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          aria-label="审计日志每页条数"
          style="width: 92px"
          @change="emit('search')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size}`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="query.page"
        :page-size="query.pageSize"
        :total="total"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./AuditTable.css"></style>
