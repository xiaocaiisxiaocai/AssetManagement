<script lang="ts" setup>
import type { AssetItem, AssetPayload, AssetStatus } from '#/api/asset';
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';
import type { UserDto, UserOptionDto } from '#/api/user';

import { computed, nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue';
import { useDebounceFn } from '@vueuse/core';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElSelect,
  ElSwitch,
  ElTag,
  ElUpload,
} from 'element-plus';

import {
  createAssetApi,
  loadAssetImageObjectUrl,
  updateAssetApi,
  uploadAssetImageApi,
} from '#/api/asset';
import { getRuntimeSettings } from '#/utils/runtime-settings';

import { validateAssetForm } from './asset-form-rules';

type FlatOption = { code?: string; id: number; label: string };

const props = defineProps<{
  asset: AssetItem | null;
  categoryOptions: FlatOption[];
  defaultCategoryId: number;
  departmentOptions: FlatOption[];
  locationOptions: FlatOption[];
  users: (UserDto | UserOptionDto)[];
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
];

const saving = ref(false);
const attachmentMaxMb = ref(5);
const conditionOptions = ref([
  '正常使用',
  '轻微损坏',
  '待维修',
  '维修中',
  '停用',
]);
const pendingUploads = new Set<Promise<unknown>>();
const uploading = ref(false);
type AuthenticatedUploadFile = UploadUserFile & { rawUrl?: string };
const imageFileList = ref<AuthenticatedUploadFile[]>([]);
let imageLoadGeneration = 0;
const form = reactive({
  categoryId: 0,
  custodianId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  locationId: undefined as number | undefined,
  name: '',
  purchaseDate: '',
  quantity: 1,
  registrationTime: '',
  currentCondition: '',
  isFirstRegistration: true,
  remark: '',
  status: 0 as AssetStatus,
});

const isEdit = computed(() => props.asset !== null);
const selectableConditionOptions = computed(() => {
  const current = form.currentCondition.trim();
  return current && !conditionOptions.value.includes(current)
    ? [current, ...conditionOptions.value]
    : conditionOptions.value;
});
const selectableDepartmentOptions = computed(() => {
  const options = [...props.departmentOptions];
  const currentId = props.asset?.departmentId;
  if (currentId && !options.some((item) => item.id === currentId)) {
    options.unshift({
      id: currentId,
      label: `${props.asset?.departmentName || '原归属部门'}（停用）`,
    });
  }
  return options;
});

const assetNoPreview = computed(() => {
  const cat = props.categoryOptions.find((o) => o.id === form.categoryId);
  return cat?.code ? `${cat.code}-自动流水` : '选择分类后生成';
});

function revokeImageObjectUrls() {
  imageFileList.value.forEach((file) => {
    if (file.url?.startsWith('blob:')) URL.revokeObjectURL(file.url);
    const responseUrl = (file.response as { url?: string } | undefined)?.url;
    if (responseUrl?.startsWith('blob:') && responseUrl !== file.url)
      URL.revokeObjectURL(responseUrl);
  });
}

onBeforeUnmount(revokeImageObjectUrls);

function onImageRemove(file: AuthenticatedUploadFile) {
  if (file.url?.startsWith('blob:')) URL.revokeObjectURL(file.url);
  const responseUrl = (file.response as { url?: string } | undefined)?.url;
  if (responseUrl?.startsWith('blob:') && responseUrl !== file.url)
    URL.revokeObjectURL(responseUrl);
}

watch(visible, async (opened) => {
  const generation = ++imageLoadGeneration;
  if (!opened) {
    revokeImageObjectUrls();
    imageFileList.value = [];
    return;
  }
  void getRuntimeSettings()
    .then((settings) => {
      attachmentMaxMb.value = settings.attachmentMaxMb;
      conditionOptions.value = settings.assetConditionOptions;
    })
    .catch(() => {});
  if (props.asset) {
    Object.assign(form, {
      categoryId: props.asset.categoryId,
      custodianId: props.asset.custodianId ?? undefined,
      departmentId: props.asset.departmentId ?? undefined,
      locationId: props.asset.locationId ?? undefined,
      name: props.asset.name,
      purchaseDate: props.asset.purchaseDate?.slice(0, 10) ?? '',
      quantity: props.asset.quantity,
      registrationTime: props.asset.registrationTime?.slice(0, 10) ?? '',
      currentCondition: props.asset.currentCondition ?? '',
      isFirstRegistration: props.asset.isFirstRegistration,
      remark: props.asset.remark ?? '',
      status: props.asset.status,
    });
    const files = await Promise.all(
      (props.asset.images ?? []).map(async (rawUrl, index) => ({
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success' as const,
        uid: -(index + 1),
        url: await loadAssetImageObjectUrl(rawUrl),
      })),
    );
    if (generation !== imageLoadGeneration || !visible.value) {
      files.forEach((file) => URL.revokeObjectURL(file.url));
      return;
    }
    imageFileList.value = files;
  } else {
    Object.assign(form, {
      categoryId: props.defaultCategoryId,
      custodianId: undefined,
      departmentId: undefined,
      locationId: undefined,
      name: '',
      purchaseDate: '',
      quantity: 1,
      registrationTime: nowLocalDate(),
      currentCondition: '',
      isFirstRegistration: true,
      remark: '',
      status: 0,
    });
    imageFileList.value = [];
  }
});

function buildPayload(): AssetPayload {
  return {
    categoryId: form.categoryId,
    custodianId: form.custodianId,
    departmentId: form.departmentId,
    images: imageFileList.value
      .map(
        (f) =>
          f.rawUrl ?? (f.response as { rawUrl?: string } | undefined)?.rawUrl,
      )
      .filter((u): u is string => !!u),
    locationId: form.locationId,
    name: form.name,
    purchaseDate: form.purchaseDate || null,
    quantity: form.quantity,
    registrationTime: form.registrationTime || null,
    currentCondition: form.currentCondition.trim() || null,
    isFirstRegistration: form.isFirstRegistration,
    remark: form.remark.trim() || null,
    status: form.status,
  };
}

function nowLocalDate() {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

function beforeImageUpload(file: File) {
  const allowed = ['image/gif', 'image/jpeg', 'image/png', 'image/webp'];
  if (!allowed.includes(file.type)) {
    ElMessage.warning('仅支持 jpg/png/gif/webp 格式图片');
    return false;
  }
  if (file.size > attachmentMaxMb.value * 1024 * 1024) {
    ElMessage.warning(`单张图片大小不能超过 ${attachmentMaxMb.value}MB`);
    return false;
  }
  return true;
}

function customImageUpload(options: UploadRequestOptions) {
  const request = uploadAssetImageApi(options.file).then(async (uploaded) => ({
    ...uploaded,
    rawUrl: uploaded.url,
    url: await loadAssetImageObjectUrl(uploaded.url),
  }));
  pendingUploads.add(request);
  uploading.value = true;
  void request.then(
    () => {
      pendingUploads.delete(request);
      uploading.value = pendingUploads.size > 0;
    },
    () => {
      pendingUploads.delete(request);
      uploading.value = pendingUploads.size > 0;
    },
  );
  return request;
}

function onImageExceed() {
  ElMessage.warning('最多上传 5 张照片');
}

async function save() {
  const error = validateAssetForm(form);
  if (error) {
    ElMessage.warning(error);
    return;
  }
  saving.value = true;
  try {
    await Promise.all([...pendingUploads]);
    await nextTick();
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

// 防抖版本的保存方法,防止用户快速点击导致重复提交
const debouncedSave = useDebounceFn(save, 300);
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="isEdit ? '编辑资产' : '新增资产'"
    width="560px"
  >
    <ElForm label-width="88px">
      <ElFormItem label="资产名称" required>
        <ElInput v-model="form.name" />
      </ElFormItem>
      <ElFormItem label="资产分类" required>
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
      <ElFormItem label="编号预览" required>
        <ElTag>{{ assetNoPreview }}</ElTag>
      </ElFormItem>
      <ElFormItem label="归属部门" required>
        <ElSelect
          v-model="form.departmentId"
          clearable
          filterable
          placeholder="选择部门"
          style="width: 100%"
        >
          <ElOption
            v-for="item in selectableDepartmentOptions"
            :key="item.id"
            :label="item.label"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="存放位置" required>
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
      <ElFormItem label="保管人" required>
        <ElSelect
          v-model="form.custodianId"
          clearable
          filterable
          placeholder="选择保管人"
          style="width: 100%"
        >
          <ElOption
            v-for="user in users"
            :key="user.id"
            :label="user.name"
            :value="user.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="数量" required>
        <ElInput v-model.number="form.quantity" />
      </ElFormItem>
      <ElFormItem label="购入日期">
        <ElDatePicker
          v-model="form.purchaseDate"
          placeholder="选择购入日期"
          style="width: 100%"
          type="date"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="登记日期">
        <ElDatePicker
          v-model="form.registrationTime"
          placeholder="选择资产登记日期"
          style="width: 100%"
          type="date"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="目前状况">
        <ElSelect
          v-model="form.currentCondition"
          clearable
          filterable
          placeholder="请选择资产目前状况"
          style="width: 100%"
        >
          <ElOption
            v-for="option in selectableConditionOptions"
            :key="option"
            :label="option"
            :value="option"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="首次登记">
        <ElSwitch
          v-model="form.isFirstRegistration"
          active-text="是"
          inactive-text="否"
        />
      </ElFormItem>
      <ElFormItem label="备注">
        <ElInput
          v-model="form.remark"
          :rows="3"
          maxlength="500"
          placeholder="请输入备注"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
      <ElFormItem label="资产照片">
        <ElUpload
          v-model:file-list="imageFileList"
          :before-upload="beforeImageUpload"
          :http-request="customImageUpload"
          :limit="5"
          :on-exceed="onImageExceed"
          :on-remove="onImageRemove"
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
      <ElButton
        :loading="saving || uploading"
        type="primary"
        @click="debouncedSave"
        >保存</ElButton
      >
    </template>
  </ElDialog>
</template>
