import type { ComputedRef } from 'vue';
import { reactive, ref } from 'vue';

import { runHandled } from '#/utils/handled-promise';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import { createPageSizeOptions } from '#/utils/runtime-settings';

export interface PendingListQuery {
  bizTypes: string[];
  keyword: string;
}

export interface PendingListPage {
  page: number;
  pageSize: number;
}

export function createPendingFilterState() {
  const pageSizeOptions = ref(createPageSizeOptions(20));
  const query = reactive<PendingListQuery>({
    bizTypes: [],
    keyword: '',
  });
  const queryModel = reactiveObjectModel(query);
  const assetPage = reactive<PendingListPage>({ page: 1, pageSize: 20 });
  const materialPage = reactive<PendingListPage>({ page: 1, pageSize: 20 });

  return {
    assetPage,
    materialPage,
    pageSizeOptions,
    query,
    queryModel,
  };
}

export function bindPendingFilterActions(state: {
  currentPage: ComputedRef<PendingListPage>;
  loadData: () => Promise<void>;
  query: PendingListQuery;
}) {
  function onPageSizeChange() {
    state.currentPage.value.page = 1;
    runHandled(state.loadData());
  }

  function search() {
    state.currentPage.value.page = 1;
    runHandled(state.loadData());
  }

  function resetQuery() {
    state.query.keyword = '';
    state.query.bizTypes = [];
    state.currentPage.value.page = 1;
    runHandled(state.loadData());
  }

  return { onPageSizeChange, resetQuery, search };
}
