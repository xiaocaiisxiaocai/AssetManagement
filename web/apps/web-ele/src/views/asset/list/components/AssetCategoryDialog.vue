<script lang="ts" setup>
import type { CategoryNode, CategoryPayload } from '#/api/base-data';

import { computed, reactive, ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElTag,
} from 'element-plus';

import { createCategoryApi, updateCategoryApi } from '#/api/base-data';

const props = defineProps<{
  category: CategoryNode | null;
  defaultParentId: null | number;
  parentCode: string;
}>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
const form = reactive<CategoryPayload>({
  codeSeg: '',
  parentId: null,
  remark: '',
});

const previewCode = computed(() =>
  props.parentCode ? `${props.parentCode}-${form.codeSeg}` : form.codeSeg,
);
const isRootCategory = computed(() => !form.parentId);

watch(visible, (opened) => {
  if (!opened) {
    return;
  }
  if (props.category) {
    Object.assign(form, {
      codeSeg: props.category.codeSeg,
      parentId: props.category.parentId ?? null,
      remark: props.category.remark ?? '',
    });
  } else {
    Object.assign(form, {
      codeSeg: '',
      parentId: props.defaultParentId,
      remark: '',
    });
  }
});

async function save() {
  const codeSeg = form.codeSeg.trim();
  if (!codeSeg) {
    ElMessage.warning('请填写编码段');
    return;
  }
  const payload = {
    codeSeg,
    parentId: form.parentId,
    remark: isRootCategory.value ? null : form.remark?.trim() || null,
  };
  saving.value = true;
  try {
    if (props.category) {
      await updateCategoryApi(props.category.id, payload);
    } else {
      await createCategoryApi(payload);
    }
    ElMessage.success('分类已保存');
    visible.value = false;
    emit('saved');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="category ? '编辑分类' : '新增分类'"
    width="460px"
  >
    <ElForm label-width="88px">
      <ElFormItem label="上级 ID">
        <ElInput
          v-model.number="form.parentId"
          clearable
          placeholder="留空为顶级"
        />
      </ElFormItem>
      <ElFormItem label="编码段">
        <ElInput v-model="form.codeSeg" />
      </ElFormItem>
      <ElFormItem v-if="!isRootCategory" label="备注">
        <ElInput v-model="form.remark" :rows="3" type="textarea" />
      </ElFormItem>
      <ElFormItem label="完整编码">
        <ElTag>{{ previewCode || '待输入' }}</ElTag>
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
    </template>
  </ElDialog>
</template>
