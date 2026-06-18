<script lang="ts" setup>
import type { AssetItem, AssetPayload, AssetStatus } from '#/api/asset';
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import { computed, reactive, ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElMessage,
  ElOption,
  ElSelect,
  ElTag,
  ElUpload,
} from 'element-plus';

import {
  assetImageUrl,
  createAssetApi,
  stripImageToken,
  updateAssetApi,
  uploadAssetImageApi,
} from '#/api/asset';

type FlatOption = { code?: string; id: number; label: string };

const props = defineProps<{
  asset: AssetItem | null;
  categoryOptions: FlatOption[];
  defaultCategoryId: number;
  departmentOptions: FlatOption[];
  locationOptions: FlatOption[];
}>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const statusOptions: Array<{
  label: string;
  tag: 'danger' | 'info' | 'success' | 'warning';
  value: AssetStatus;
}> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
  { label: '维修', tag: 'info', value: 2 },
  { label: '报废', tag: 'danger', value: 3 },
];

const saving = ref(false);
const imageFileList = ref<UploadUserFile[]>([]);
const form = reactive({
  brand: '',
  categoryId: 0,
  departmentId: undefined as number | undefined,
  locationId: undefined as number | undefined,
  model: '',
  name: '',
  price: 0,
  quantity: 1,
  status: 0 as AssetStatus,
});

const isEdit = computed(() => props.asset !== null);

const assetNoPreview = computed(() => {
  const cat = props.categoryOptions.find((o) => o.id === form.categoryId);
  return cat?.code ? `${cat.code}-自动流水` : '选择分类后生成';
});

watch(visible, (opened) => {
  if (!opened) {
    return;
  }
  if (props.asset) {
    Object.assign(form, {
      brand: props.asset.brand ?? '',
      categoryId: props.asset.categoryId,
      departmentId: props.asset.departmentId ?? undefined,
      locationId: props.asset.locationId ?? undefined,
      model: props.asset.model ?? '',
      name: props.asset.name,
      price: props.asset.price,
      quantity: props.asset.quantity,
      status: props.asset.status,
    });
    imageFileList.value = (props.asset.images ?? []).map((url, index) => ({
      name: url.split('/').pop() ?? url,
      status: 'success',
      uid: -(index + 1),
      url: assetImageUrl(url),
    }));
  } else {
    Object.assign(form, {
      brand: '',
      categoryId: props.defaultCategoryId,
      departmentId: undefined,
      locationId: undefined,
      model: '',
      name: '',
      price: 0,
      quantity: 1,
      status: 0,
    });
    imageFileList.value = [];
  }
});

function buildPayload(): AssetPayload {
  return {
    brand: form.brand,
    categoryId: form.categoryId,
    departmentId: form.departmentId,
    images: imageFileList.value
      .map((f) => f.url ?? (f.response as { url?: string } | undefined)?.url)
      .filter((u): u is string => !!u)
      .map((u) => stripImageToken(u)),
    locationId: form.locationId,
    model: form.model,
    name: form.name,
    price: form.price,
    quantity: form.quantity,
    status: form.status,
  };
}

function beforeImageUpload(file: File) {
  const allowed = ['image/gif', 'image/jpeg', 'image/png', 'image/webp'];
  if (!allowed.includes(file.type)) {
    ElMessage.warning('仅支持 jpg/png/gif/webp 格式图片');
    return false;
  }
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('单张图片大小不能超过 5MB');
    return false;
  }
  return true;
}

async function customImageUpload(options: UploadRequestOptions) {
  // 返回值会成为该文件的 response,buildPayload 据此取 url
  return await uploadAssetImageApi(options.file);
}

function onImageExceed() {
  ElMessage.warning('最多上传 5 张照片');
}

async function save() {
  if (!form.name.trim() || !form.categoryId) {
    ElMessage.warning('请填写资产名称并选择分类');
    return;
  }
  saving.value = true;
  try {
    if (props.asset) {
      await updateAssetApi(props.asset.id, buildPayload());
    } else {
      await createAssetApi(buildPayload());
    }
    ElMessage.success('保存成功');
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
    :title="isEdit ? '编辑资产' : '新增资产'"
    width="560px"
  >
    <ElForm label-width="88px">
      <ElFormItem label="资产名称">
        <ElInput v-model="form.name" />
      </ElFormItem>
      <ElFormItem label="资产分类">
        <ElSelect
          v-model="form.categoryId"
          filterable
          placeholder="选择末级分类"
          style="width: 100%"
        >
          <ElOption
            v-for="item in categoryOptions"
            :key="item.id"
            :label="item.label"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="编号预览">
        <ElTag>{{ assetNoPreview }}</ElTag>
      </ElFormItem>
      <ElFormItem label="归属部门">
        <ElSelect
          v-model="form.departmentId"
          clearable
          filterable
          placeholder="选择部门"
          style="width: 100%"
        >
          <ElOption
            v-for="item in departmentOptions"
            :key="item.id"
            :label="item.label"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="存放位置">
        <ElSelect
          v-model="form.locationId"
          clearable
          filterable
          placeholder="选择位置"
          style="width: 100%"
        >
          <ElOption
            v-for="item in locationOptions"
            :key="item.id"
            :label="item.label"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="型号品牌">
        <div class="grid w-full grid-cols-2 gap-2">
          <ElInput v-model="form.model" placeholder="型号" />
          <ElInput v-model="form.brand" placeholder="品牌" />
        </div>
      </ElFormItem>
      <ElFormItem label="价格数量">
        <div class="grid w-full grid-cols-2 gap-2">
          <ElInputNumber v-model="form.price" :min="0" :precision="2" class="w-full" />
          <ElInputNumber v-model="form.quantity" :min="1" class="w-full" />
        </div>
      </ElFormItem>
      <ElFormItem label="资产照片">
        <ElUpload
          v-model:file-list="imageFileList"
          :before-upload="beforeImageUpload"
          :http-request="customImageUpload"
          :limit="5"
          :on-exceed="onImageExceed"
          accept="image/png,image/jpeg,image/gif,image/webp"
          list-type="picture-card"
        >
          <span class="text-2xl">+</span>
        </ElUpload>
      </ElFormItem>
      <ElFormItem v-if="isEdit" label="状态">
        <ElSelect v-model="form.status" style="width: 100%">
          <ElOption
            v-for="item in statusOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
    </template>
  </ElDialog>
</template>
