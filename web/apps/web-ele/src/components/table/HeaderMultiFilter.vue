<script lang="ts" setup generic="T extends number | string">
import type { HeaderFilterGroup } from './header-multi-filter';

import { computed, onBeforeUnmount, ref } from 'vue';

import { ElButton, ElCheckbox, ElInput, ElPopover } from 'element-plus';

const props = defineProps<{
  filterable?: boolean;
  groups?: HeaderFilterGroup<T>[];
  label: string;
  loading?: boolean;
  options?: HeaderFilterGroup<T>['options'];
  remote?: boolean;
}>();

const emit = defineEmits<{
  change: [value: T[]];
  search: [keyword: string];
}>();

const model = defineModel<T[]>({ required: true });
const keyword = ref('');
let searchTimer: ReturnType<typeof setTimeout> | undefined;

const active = computed(() => model.value.length > 0);
const showSearch = computed(() => props.filterable || props.remote);

const visibleGroups = computed(() => {
  const groups =
    props.groups && props.groups.length > 0
      ? props.groups
      : [{ options: props.options ?? [] }];
  const text = props.remote ? '' : keyword.value.trim().toLowerCase();
  if (!text) return groups;
  return groups
    .map((group) => ({
      ...group,
      options: group.options.filter((option) =>
        option.label.toLowerCase().includes(text),
      ),
    }))
    .filter((group) => group.options.length > 0);
});

function isChecked(value: T) {
  return model.value.includes(value);
}

function publish(next: T[]) {
  model.value = next;
  emit('change', next);
}

function toggle(value: T, checked: boolean) {
  const next = model.value.filter((item) => item !== value);
  if (checked) next.push(value);
  publish(next);
}

function clearSelection() {
  if (model.value.length === 0) return;
  publish([]);
}

function onKeyword(value: string) {
  keyword.value = value;
  if (!props.remote) return;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => emit('search', value.trim()), 300);
}

onBeforeUnmount(() => clearTimeout(searchTimer));
</script>

<template>
  <ElPopover
    :width="248"
    placement="bottom"
    popper-class="header-multi-filter-popper"
    trigger="click"
  >
    <template #reference>
      <button
        :aria-label="`${label}筛选`"
        :class="{ 'is-active': active }"
        class="header-multi-filter"
        type="button"
        @click.stop
      >
        <span>{{ label }}</span>
        <i class="header-multi-filter-caret"></i>
      </button>
    </template>
    <div class="header-multi-filter-panel" @click.stop>
      <ElInput
        v-if="showSearch"
        :model-value="keyword"
        clearable
        placeholder="搜索"
        size="small"
        @update:model-value="onKeyword"
      />
      <div v-loading="loading" class="header-multi-filter-options">
        <div
          v-for="(group, index) in visibleGroups"
          :key="group.label || index"
          class="header-multi-filter-group"
        >
          <div v-if="group.label" class="header-multi-filter-group-title">
            {{ group.label }}
          </div>
          <ElCheckbox
            v-for="option in group.options"
            :key="`${group.label || ''}-${option.value}`"
            :model-value="isChecked(option.value)"
            @change="
              (checked: boolean | number | string) =>
                toggle(option.value, checked === true)
            "
          >
            {{ option.label }}
          </ElCheckbox>
        </div>
        <div v-if="visibleGroups.length === 0" class="header-multi-filter-empty">
          无匹配选项
        </div>
      </div>
      <div class="header-multi-filter-actions">
        <ElButton
          :disabled="!active"
          link
          size="small"
          type="primary"
          @click="clearSelection"
        >
          清空
        </ElButton>
      </div>
    </div>
  </ElPopover>
</template>

<style scoped src="./HeaderMultiFilter.css"></style>

<style>
.header-multi-filter-popper.el-popper {
  padding: 8px;
}
</style>
