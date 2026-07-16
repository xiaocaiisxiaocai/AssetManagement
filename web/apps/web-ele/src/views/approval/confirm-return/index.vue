<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import { onMounted, reactive, ref } from 'vue';

import { getPendingReturnsApi, confirmReturnApi } from '#/api/workflow';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';
import { formatDateTime } from '#/utils/date-format';

import {
  ElButton,
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ConfirmReturn' });

const loading = ref(false);
const confirmingIds = ref(new Set<number>());
const flows = ref<ApprovalFlow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));

const query = reactive({
  keyword: '',
  returnDate: '',
  page: 1,
  pageSize: 20,
});

const allFlowsCache = ref<ApprovalFlow[]>([]);

async function loadData() {
  loading.value = true;
  try {
    const allFlows = await getPendingReturnsApi();
    allFlowsCache.value = allFlows.filter(
      (f) => f.bizType === 'borrow' && f.status === 'approved' && !f.confirmedAt
    );
    query.page = 1;
    updatePage();
  } finally {
    loading.value = false;
  }
}

function filteredFlows() {
  const keyword = query.keyword.trim().toLowerCase();
  return allFlowsCache.value.filter((flow) => {
    const matchKeyword =
      !keyword ||
      [
        flow.flowNo,
        flow.assetNo,
        flow.assetName,
        flow.applicant,
        flow.applicantDept,
        flow.reason,
      ]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(keyword));
    const matchReturnDate = !query.returnDate || flow.returnDate === query.returnDate;
    return matchKeyword && matchReturnDate;
  });
}

function updatePage() {
  const filtered = filteredFlows();
  total.value = filtered.length;
  if ((query.page - 1) * query.pageSize >= total.value) {
    query.page = 1;
  }
  const start = (query.page - 1) * query.pageSize;
  flows.value = filtered.slice(start, start + query.pageSize);
}

function onPageSizeChange() {
  query.page = 1;
  updatePage();
}

function search() {
  query.page = 1;
  updatePage();
}

function resetQuery() {
  query.keyword = '';
  query.returnDate = '';
  query.page = 1;
  updatePage();
}

async function confirmReturn(row: ApprovalFlow) {
  try {
    await ElMessageBox.confirm(
      `请确认已实际收回「${row.applicant}」归还的资产「${row.assetName}」（${row.assetNo}）。确认后资产将恢复为可用状态，并清空当前保管人。`,
      '确认资产已归还',
      {
        type: 'warning',
        confirmButtonText: '确认已收回',
        cancelButtonText: '暂不确认',
        closeOnClickModal: false,
        closeOnPressEscape: false,
      }
    );
  } catch {
    return;
  }

  confirmingIds.value.add(row.id);
  try {
    await confirmReturnApi(row.id);
    ElMessage.success('归还确认成功，资产已恢复可用状态');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    confirmingIds.value.delete(row.id);
  }
}

function bizText(type: string) {
  return { borrow: '借用', return: '归还', transfer: '转让' }[type] ?? type;
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="关键字">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="流程编号/资产/借用人"
              style="width: 260px"
              @keyup.enter="search"
            />
          </ElFormItem>
          <ElFormItem label="应归还日期">
            <ElDatePicker
              v-model="query.returnDate"
              clearable
              placeholder="选择日期"
              style="width: 160px"
              type="date"
              value-format="YYYY-MM-DD"
              @change="search"
            />
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="table-panel-with-toolbar">
        <ElTable v-loading="loading" :data="flows" border height="100%">
          <ElTableColumn label="流程编号" min-width="160" prop="flowNo" />
          <ElTableColumn class-name="hide-on-mobile" label="资产编号" min-width="140" prop="assetNo" />
          <ElTableColumn label="资产名称" min-width="200" prop="assetName" />
          <ElTableColumn label="业务类型" width="100" align="center">
            <template #default="{ row }">
              <ElTag type="success" size="small">{{ bizText(row.bizType) }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用人" min-width="130" prop="applicant" />
          <ElTableColumn class-name="hide-on-mobile" label="借用部门" min-width="150" prop="applicantDept" />
          <ElTableColumn label="应归还日期" min-width="140" prop="returnDate" />
          <ElTableColumn class-name="hide-on-mobile" label="申请时间" min-width="180">
            <template #default="{ row }">
              {{ formatDateTime(row.applyTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="借用事由" min-width="200" prop="reason" show-overflow-tooltip />
          <ElTableColumn fixed="right" label="操作" width="120" align="center">
            <template #default="{ row }">
              <ElButton
                :loading="confirmingIds.has(row.id)"
                link
                type="primary"
                size="small"
                @click="confirmReturn(row)"
              >
                确认已收回
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>

        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ total }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect v-model="query.pageSize" style="width: 92px" @change="onPageSizeChange">
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
            @current-change="updatePage"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>
