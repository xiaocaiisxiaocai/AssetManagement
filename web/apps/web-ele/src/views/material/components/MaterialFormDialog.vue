<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { MaterialItem, SaveMaterialPayload } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';
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
  ElInputNumber,
  ElMessage,
  ElOption,
  ElSelect,
  ElUpload,
} from 'element-plus';

import { loadAssetImageObjectUrl, uploadAssetImageApi } from '#/api/asset';
import { createMaterialApi, updateMaterialApi } from '#/api/material';
import { createAsyncSessionTracker } from '#/utils/async-session-tracker';
import { createObjectUrlLifecycle } from '#/utils/object-url-lifecycle';
import { getRuntimeSettings } from '#/utils/runtime-settings';
import { buildFileActionAccess } from '#/views/permissions/action-access';

import {
  getDefaultCustodianId,
  validateMaterialForm,
} from './material-form-rules';

type FlatOption = { id: number; label: string };

const props = defineProps<{
  defaultProjectId?: number;
  departmentOptions: FlatOption[];
  material: MaterialItem | null;
  projectLocked?: boolean;
  projects: TestProjectItem[];
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

const saving = ref(false);
const attachmentMaxMb = ref(5);
const pendingUploads = createAsyncSessionTracker();
const uploading = ref(false);
type AuthenticatedUploadFile = { rawUrl?: string } & UploadUserFile;
const imageFileList = ref<AuthenticatedUploadFile[]>([]);
let imageLoadGeneration = 0;
const uploadedImageUrls = createObjectUrlLifecycle();
const form = reactive({
  brand: '',
  custodianId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  locationName: '',
  model: '',
  name: '',
  projectId: undefined as number | undefined,
  quantity: 1,
  receivedDate: undefined as string | undefined,
  remark: '',
  vendorName: '',
});

const isEdit = computed(() => props.material !== null);
const selectableDepartmentOptions = computed(() => {
  const options = [...props.departmentOptions];
  const currentId = props.material?.departmentId;
  if (currentId && !options.some((item) => item.id === currentId)) {
    options.unshift({
      id: currentId,
      label: `${props.material?.departmentName || '原归属部门'}（停用）`,
    });
  }
  return options;
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
    })
    .catch(() => {});
  if (props.material) {
    Object.assign(form, {
      brand: props.material.brand ?? '',
      custodianId: props.material.custodianId ?? undefined,
      departmentId: props.material.departmentId ?? undefined,
      locationName: props.material.locationName ?? '',
      model: props.material.model ?? '',
      name: props.material.name,
      projectId: props.material.projectId,
      quantity: props.material.quantity,
      receivedDate: props.material.receivedDate ?? undefined,
      remark: props.material.remark ?? '',
      vendorName: props.material.vendorName ?? '',
    });
    const results = await Promise.allSettled(
      (props.material.images ?? []).map(async (rawUrl, index) => ({
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success' as const,
        uid: -(index + 1),
        url: await loadAssetImageObjectUrl(rawUrl),
      })),
    );
    const sourceImages = props.material.images ?? [];
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
    const projectId = props.defaultProjectId;
    Object.assign(form, {
      brand: '',
      custodianId: getDefaultCustodianId(props.projects, projectId),
      departmentId: undefined,
      locationName: '',
      model: '',
      name: '',
      projectId,
      quantity: 1,
      receivedDate: undefined,
      remark: '',
      vendorName: '',
    });
    imageFileList.value = [];
  }
});

function buildPayload(): SaveMaterialPayload {
  return {
    brand: form.brand,
    custodianId: form.custodianId,
    departmentId: form.departmentId,
    images: imageFileList.value
      .map(
        (f) =>
          f.rawUrl ?? (f.response as { rawUrl?: string } | undefined)?.rawUrl,
      )
      .filter((u): u is string => !!u),
    locationName: form.locationName.trim() || null,
    model: form.model,
    name: form.name,
    projectId: form.projectId as number,
    quantity: form.quantity,
    receivedDate: form.receivedDate ?? null,
    remark: form.remark,
    vendorName: form.vendorName,
  };
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
  const validationMessage = validateMaterialForm(form);
  if (validationMessage) {
    ElMessage.warning(validationMessage);
    return;
  }
  saving.value = true;
  const sessionToken = pendingUploads.token();
  try {
    await Promise.all(pendingUploads.pending(sessionToken));
    if (!pendingUploads.isCurrent(sessionToken) || !visible.value) return;
    await nextTick();
    await (props.material
      ? updateMaterialApi(props.material.id, buildPayload())
      : createMaterialApi(buildPayload()));
    if (!pendingUploads.isCurrent(sessionToken) || !visible.value) return;
    ElMessage.success('保存成功');
    visible.value = false;
    emit('saved');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    saving.value = false;
  }
}

const debouncedSave = useDebounceFn(save, 300);
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="isEdit ? '编辑测试料件' : '新增测试料件'"
    align-center
    width="600px"
  >
    <ElForm label-width="96px">
      <ElFormItem label="料件名称" required>
        <ElInput v-model="form.name" placeholder="请输入料件名称" />
      </ElFormItem>
      <ElFormItem label="所属项目" required>
        <ElSelect
          v-model="form.projectId"
          :disabled="props.projectLocked"
          filterable
          placeholder="选择测试项目"
          style="width: 100%"
        >
          <ElOption
            v-for="item in projects"
            :key="item.id"
            :label="item.name"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="厂商/来源">
        <ElInput v-model="form.vendorName" placeholder="请输入寄件厂商名称" />
      </ElFormItem>
      <ElFormItem label="型号品牌">
        <div class="grid w-full grid-cols-2 gap-2">
          <ElInput v-model="form.model" placeholder="请输入型号" />
          <ElInput v-model="form.brand" placeholder="请输入品牌" />
        </div>
      </ElFormItem>
      <ElFormItem label="数量" required>
        <ElInputNumber v-model="form.quantity" :min="1" style="width: 100%" />
      </ElFormItem>
      <ElFormItem label="归属部门">
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
      <ElFormItem label="存放位置">
        <ElInput
          v-model="form.locationName"
          :maxlength="100"
          clearable
          placeholder="请输入存放位置"
          show-word-limit
        />
      </ElFormItem>
      <ElFormItem label="保管人">
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
            :label="`${user.name}(${user.employeeNo})`"
            :value="user.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="接收日期">
        <ElDatePicker
          v-model="form.receivedDate"
          placeholder="选择接收日期"
          style="width: 100%"
          type="date"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="料件照片">
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
      <ElFormItem label="备注">
        <ElInput
          v-model="form.remark"
          :maxlength="500"
          :rows="2"
          placeholder="可选"
          show-word-limit
          type="textarea"
        />
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
