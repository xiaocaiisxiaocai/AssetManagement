<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { AssetItem, AssetPayload, AssetStatus } from '#/api/asset';
import type { UserDto, UserOptionDto } from '#/api/user';

import { computed, nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';

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
  ElTag,
  ElUpload,
} from 'element-plus';

import {
  createAssetApi,
  loadAssetImageObjectUrl,
  updateAssetApi,
  uploadAssetImageApi,
} from '#/api/asset';
import { createAsyncSessionTracker } from '#/utils/async-session-tracker';
import { businessDateText } from '#/utils/business-date';
import { createObjectUrlLifecycle } from '#/utils/object-url-lifecycle';
import { getRuntimeSettings } from '#/utils/runtime-settings';
import { buildFileActionAccess } from '#/views/permissions/action-access';

import { validateAssetForm } from './asset-form-rules';

type FlatOption = { code?: string; id: number; label: string };

const props = defineProps<{
  asset: AssetItem | null;
  categoryOptions: FlatOption[];
  defaultCategoryId: number;
  departmentOptions: FlatOption[];
  searchUsers?: (keyword: string) => Promise<void>;
  userOptionsLoading?: boolean;
  users: (UserDto | UserOptionDto)[];
}>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });
const { hasAccessByCodes } = useAccess();
const canUploadImages = computed(
  () => buildFileActionAccess(hasAccessByCodes).canUploadAndPreview,
);

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
const pendingUploads = createAsyncSessionTracker();
const uploading = ref(false);
type AuthenticatedUploadFile = { rawUrl?: string } & UploadUserFile;
const imageFileList = ref<AuthenticatedUploadFile[]>([]);
let imageLoadGeneration = 0;
const uploadedImageUrls = createObjectUrlLifecycle();
const form = reactive({
  categoryId: 0,
  custodianId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  locationName: '',
  name: '',
  purchaseDate: '',
  quantity: 1,
  registrationTime: '',
  currentCondition: '',
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

onBeforeUnmount(() => {
  uploadedImageUrls.close();
  revokeImageObjectUrls();
});

function onImageRemove(file: AuthenticatedUploadFile) {
  if (file.url?.startsWith('blob:')) URL.revokeObjectURL(file.url);
  const responseUrl = (file.response as { url?: string } | undefined)?.url;
  if (responseUrl?.startsWith('blob:') && responseUrl !== file.url)
    URL.revokeObjectURL(responseUrl);
}

watch(visible, async (opened) => {
  const generation = ++imageLoadGeneration;
  if (!opened) {
    pendingUploads.close();
    uploading.value = false;
    uploadedImageUrls.close();
    revokeImageObjectUrls();
    imageFileList.value = [];
    return;
  }
  pendingUploads.start();
  uploadedImageUrls.open();
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
      locationName: props.asset.locationName ?? '',
      name: props.asset.name,
      purchaseDate: props.asset.purchaseDate?.slice(0, 10) ?? '',
      quantity: props.asset.quantity,
      registrationTime: props.asset.registrationTime?.slice(0, 10) ?? '',
      currentCondition: props.asset.currentCondition ?? '',
      remark: props.asset.remark ?? '',
      status: props.asset.status,
    });
    const results = await Promise.allSettled(
      (props.asset.images ?? []).map(async (rawUrl, index) => ({
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success' as const,
        uid: -(index + 1),
        url: await loadAssetImageObjectUrl(rawUrl),
      })),
    );
    const sourceImages = props.asset.images ?? [];
    const files: AuthenticatedUploadFile[] = results.map((result, index) => {
      if (result.status === 'fulfilled') return result.value;
      const rawUrl = sourceImages[index]!;
      return {
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success',
        uid: -(index + 1),
      };
    });
    if (generation !== imageLoadGeneration || !visible.value) {
      files.forEach((file) => {
        if (file.url) URL.revokeObjectURL(file.url);
      });
      return;
    }
    imageFileList.value = files;
    const failedCount = results.filter(
      (result) => result.status === 'rejected',
    ).length;
    if (failedCount > 0) {
      ElMessage.warning(`有 ${failedCount} 张原照片加载失败，保存前请确认`);
    }
  } else {
    Object.assign(form, {
      categoryId: props.defaultCategoryId,
      custodianId: undefined,
      departmentId: undefined,
      locationName: '',
      name: '',
      purchaseDate: '',
      quantity: 1,
      registrationTime: nowLocalDate(),
      currentCondition: '',
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
    locationName: form.locationName.trim() || null,
    name: form.name,
    purchaseDate: form.purchaseDate || null,
    quantity: form.quantity,
    registrationTime: form.registrationTime || null,
    currentCondition: form.currentCondition.trim() || null,
    remark: form.remark.trim() || null,
    status: form.status,
  };
}

function nowLocalDate() {
  return businessDateText();
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
  const sessionToken = pendingUploads.token();
  const uploadGeneration = uploadedImageUrls.token();
  const request = uploadAssetImageApi(options.file).then(async (uploaded) => {
    const url = await loadAssetImageObjectUrl(uploaded.url);
    if (!uploadedImageUrls.adopt(url, uploadGeneration)) {
      throw new DOMException('上传会话已关闭', 'AbortError');
    }
    return {
      ...uploaded,
      rawUrl: uploaded.url,
      url,
    };
  });
  pendingUploads.track(request, sessionToken);
  uploading.value = true;
  void request.then(
    () => {
      if (pendingUploads.isCurrent(sessionToken))
        uploading.value = pendingUploads.hasPending(sessionToken);
    },
    () => {
      if (pendingUploads.isCurrent(sessionToken))
        uploading.value = pendingUploads.hasPending(sessionToken);
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
  const sessionToken = pendingUploads.token();
  try {
    await Promise.all(pendingUploads.pending(sessionToken));
    if (!pendingUploads.isCurrent(sessionToken) || !visible.value) return;
    await nextTick();
    await (props.asset
      ? updateAssetApi(props.asset.id, buildPayload())
      : createAssetApi(buildPayload()));
    if (!pendingUploads.isCurrent(sessionToken) || !visible.value) return;
    ElMessage.success('保存成功');
    visible.value = false;
    emit('saved');
  } catch {
    // 请求错误已由统一请求层提示；关闭会话产生的 AbortError 无需额外提示。
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
          :disabled="isEdit"
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
        <ElInput
          v-model="form.locationName"
          :maxlength="100"
          clearable
          placeholder="请输入存放位置，如：三楼研发区 A-12"
          show-word-limit
        />
      </ElFormItem>
      <ElFormItem label="保管人" required>
        <ElSelect
          v-model="form.custodianId"
          :disabled="isEdit"
          :loading="userOptionsLoading"
          :remote-method="searchUsers"
          clearable
          filterable
          placeholder="选择保管人"
          remote
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
        <ElInputNumber v-model="form.quantity" :min="1" style="width: 100%" />
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
          :disabled="!canUploadImages"
          :http-request="customImageUpload"
          :limit="5"
          :on-exceed="onImageExceed"
          :on-remove="onImageRemove"
          accept="image/png,image/jpeg,image/gif,image/webp"
          list-type="picture-card"
        >
          <span v-if="canUploadImages" class="text-2xl">+</span>
        </ElUpload>
        <div v-if="!canUploadImages" class="text-xs text-gray-400">
          当前账号无文件上传权限
        </div>
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
      >
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>
