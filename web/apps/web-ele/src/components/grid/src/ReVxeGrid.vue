<script lang="ts" setup>
import type { VxeGridPropTypes, VxePagerEvents } from 'vxe-table';

import type { PurestGridProps } from './types';

import { h, onMounted, reactive, ref, type VNode } from 'vue';

import { useAccess } from '@vben/access';

import { ElCard } from 'element-plus';
import { VxeButton } from 'vxe-pc-ui';

import { $t } from '#/locales';
import { runHandled } from '#/utils/handled-promise';

import './style.css';

const props = withDefaults(defineProps<PurestGridProps>(), {
  rowKey: `id`,
  size: `small`,
  customePager: () => ({
    total: 0,
    pageIndex: 1,
    pageSize: 15,
  }),
});

const { hasAccessByCodes } = useAccess();

const formActions = [
  {
    itemRender: {
      name: '$buttons',
      children: [
        {
          props: {
            type: 'submit',
            icon: 'vxe-icon-search',
            content: $t('common.search'),
            status: 'primary',
          },
        },
        {
          props: {
            type: 'reset',
            icon: 'vxe-icon-undo',
            content: $t(`common.reset`),
          },
        },
      ],
    },
  },
];
const items = [...(props.searchOptions?.formItems ?? []), ...formActions];
const operateColumns: VxeGridPropTypes.Columns<any> =
  props.commonOperation === null || props.commonOperation === undefined
    ? []
    : [
        {
          title: $t('common.operation'),
          field: 'operation',
          align: 'center',
          fixed: `right`,
          width: 210,
          slots: {
            default: ({ row }) => {
              const buttons: VNode[] = [];
              if (props.commonOperation) {
                Object.keys(props.commonOperation).forEach((key) => {
                  const operation =
                    props.commonOperation![key as 'delete' | 'edit' | 'view'];
                  if (hasAccessByCodes([operation!.permissionCode])) {
                    switch (key) {
                      case 'delete': {
                        buttons.push(
                          h(VxeButton, {
                            status: 'danger',
                            mode: 'text',
                            icon: 'vxe-icon-delete',
                            content: $t('common.del'),
                            onClick() {
                              operation!.handleClick(row);
                            },
                          }),
                        );
                        break;
                      }
                      case 'edit': {
                        buttons.push(
                          h(VxeButton, {
                            status: 'primary',
                            icon: 'vxe-icon-edit',
                            mode: 'text',
                            content: $t('common.edit'),
                            onClick() {
                              operation!.handleClick(row);
                            },
                          }),
                        );
                        break;
                      }
                      case 'view': {
                        buttons.push(
                          h(VxeButton, {
                            status: 'warning',
                            mode: 'text',
                            icon: 'vxe-icon-file-txt',
                            content: $t('common.view'),
                            onClick() {
                              operation!.handleClick(row);
                            },
                          }),
                        );
                        break;
                      }
                    }
                  }
                });
              }
              return buttons;
            },
          },
        },
      ];
const columns = [...props.columns, ...operateColumns];
const toolbarConfig: VxeGridPropTypes.ToolbarConfig =
  props.customToolbarActions ?? {
    slots: {
      buttons: () => {
        const buttons: VNode[] = [];
        if (props.commonOperation) {
          Object.keys(props.commonOperation).forEach((key) => {
            const operation =
              props.commonOperation![key as 'add' | 'export' | 'import'];
            if (hasAccessByCodes([operation!.permissionCode])) {
              switch (key) {
                case 'add': {
                  buttons.push(
                    h(VxeButton, {
                      icon: 'vxe-icon-add',
                      status: 'primary',
                      content: $t('common.add'),
                      onClick() {
                        operation!.handleClick();
                      },
                    }),
                  );
                  break;
                }
              }
            }
          });
        }
        return buttons;
      },
    },
    custom: true,
  };
const treeOption = props.treeConfig ?? {};
const data = ref<unknown[]>([]);
const loading = ref(false);
const pager = reactive({ ...props.customePager });

const loadData = async (params?: any) => {
  loading.value = true;
  try {
    const result = await props.request({
      ...pager,
      ...props.searchOptions?.formData,
      ...params,
    });
    const { pageIndex, total, items: resultItems } = result;
    data.value = resultItems;
    pager.total = total;
    pager.pageIndex = pageIndex;
  } finally {
    loading.value = false;
  }
};
const handlePageChange: VxePagerEvents.PageChange = ({
  currentPage,
  pageSize,
}) => {
  pager.pageIndex = currentPage;
  pager.pageSize = pageSize;
  runHandled(loadData());
};
onMounted(() => {
  runHandled(loadData());
});
defineExpose({ loadData });
</script>
<template>
  <div>
    <ElCard v-if="props.searchOptions">
      <vxe-form
        :data="props.searchOptions?.formData"
        :items="items"
        :size="props.size"
        @reset="props.searchOptions?.reset"
        @submit="props.searchOptions?.submit"
      />
      <slot name="searchForm"></slot>
    </ElCard>
    <ElCard class="table-card">
      <vxe-grid
        :columns="columns"
        :data="data"
        :height="props.height"
        :loading="loading"
        :max-height="650"
        :min-height="300"
        :pager-config="{
          pageSizes: [15, 30, 50, 100, 300],
          size: props.size,
          total: pager.total,
          pageSize: pager.pageSize,
          currentPage: pager.pageIndex,
        }"
        :resizable="true"
        :round="true"
        :row-config="{ keyField: rowKey, isHover: true }"
        :size="props.size"
        :toolbar-config="toolbarConfig"
        :tree-config="treeOption"
        @page-change="handlePageChange"
      />
    </ElCard>
  </div>
</template>
