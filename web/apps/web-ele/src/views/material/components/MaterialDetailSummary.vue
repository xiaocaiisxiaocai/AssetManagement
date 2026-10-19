<script lang="ts" setup>
import type { MaterialDetail } from '#/api/material';

import { ElDescriptions, ElDescriptionsItem, ElTag } from 'element-plus';

import { formatDate } from '#/utils/date-format';

defineProps<{
  detail: MaterialDetail;
  statusText: Record<number, string>;
}>();
</script>

<template>
  <ElDescriptions :column="2" border class="material-detail-descriptions">
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
    <ElDescriptionsItem label="对接厂商">
      {{ detail.material.dockingVendorName ?? '-' }}
    </ElDescriptionsItem>
    <ElDescriptionsItem label="状态">
      <ElTag
        :type="detail.material.status === 0 ? 'success' : 'info'"
        size="small"
      >
        {{ statusText[detail.material.status] }}
      </ElTag>
      <ElTag
        v-if="detail.material.isDeleted"
        class="ml-1"
        size="small"
        type="danger"
      >
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
      {{ formatDate(detail.material.receivedDate) }}
    </ElDescriptionsItem>
    <ElDescriptionsItem :span="2" label="备注">
      {{ detail.material.remark ?? '-' }}
    </ElDescriptionsItem>
  </ElDescriptions>
</template>

<style scoped src="./MaterialDetailDialog.css"></style>
