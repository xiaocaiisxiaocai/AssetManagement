<script lang="ts" setup>
import type { MaterialDetail, MaterialStatus } from '#/api/material';

import {
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElEmpty,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

defineProps<{ detail: MaterialDetail | null; loading: boolean }>();
const visible = defineModel<boolean>('visible', { default: false });

const statusText: Record<MaterialStatus, string> = {
  0: '在用',
  1: '已退回厂商',
};

const actionText: Record<string, string> = {
  approve: '审批通过',
  direct_transfer: '直接转移',
  reject: '驳回',
  start: '发起流转',
};
</script>

<template>
  <ElDialog v-model="visible" title="测试料件详情" width="720px">
    <div v-loading="loading">
      <template v-if="detail">
        <ElDescriptions :column="2" border>
          <ElDescriptionsItem label="料件编号">
            {{ detail.material.materialNo }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="名称">
            {{ detail.material.name }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="所属项目">
            {{ detail.material.projectName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="厂商/来源">
            {{ detail.material.vendorName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="型号">
            {{ detail.material.model ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="品牌">
            {{ detail.material.brand ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="数量">
            {{ detail.material.quantity }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="状态">
            <ElTag :type="detail.material.status === 0 ? 'success' : 'info'" size="small">
              {{ statusText[detail.material.status] }}
            </ElTag>
            <ElTag v-if="detail.material.isDeleted" class="ml-1" size="small" type="danger">
              已删除
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="归属部门">
            {{ detail.material.departmentName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="存放位置">
            {{ detail.material.locationName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="保管人">
            {{ detail.material.custodianName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="接收日期">
            {{ detail.material.receivedDate?.slice(0, 10) ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="备注">
            {{ detail.material.remark ?? '-' }}
          </ElDescriptionsItem>
        </ElDescriptions>

        <div v-if="detail.material.images.length" class="mt-3 flex flex-wrap gap-2">
          <span class="text-sm text-gray-500">照片:{{ detail.material.images.length }} 张</span>
        </div>

        <div class="mt-4 mb-2 text-sm font-semibold">流转记录</div>
        <ElTimeline v-if="detail.records.length">
          <ElTimelineItem
            v-for="record in detail.records"
            :key="record.id"
            :timestamp="record.operatedAt.replace('T', ' ').slice(0, 19)"
          >
            <span class="font-medium">
              {{ actionText[record.action] ?? record.action }}
            </span>
            <span v-if="record.operator" class="ml-2 text-gray-500">
              {{ record.operator }}
            </span>
            <div v-if="record.comment" class="text-sm text-gray-500">
              {{ record.comment }}
            </div>
          </ElTimelineItem>
        </ElTimeline>
        <ElEmpty v-else :image-size="60" description="暂无流转记录" />
      </template>
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>
