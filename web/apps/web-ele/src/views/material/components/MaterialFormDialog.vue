<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { MaterialItem, SaveMaterialPayload } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';
import type { UserDto, UserOptionDto } from '#/api/user';

import { computed, nextTick, reactive, ref, watch } from 'vue';

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

import { assetImageUrl, stripImageToken, uploadAssetImageApi } from '#/api/asset';
import { createMaterialApi, updateMaterialApi } from '#/api/material';
import { getRuntimeSettings } from '#/utils/runtime-settings';
import { getDefaultCustodianId, validateMaterialForm } from './material-form-rules';

type FlatOption = { id: number; label: string };

const props = defineProps<{
  defaultProjectId?: number;
  departmentOptions: FlatOption[];
  locationOptions: FlatOption[];
  material: MaterialItem | null;
  projectLocked?: boolean;
  projects: TestProjectItem[];
  users: (UserDto | UserOptionDto)[];
}>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
const attachmentMaxMb = ref(5);
const pendingUploads = new Set<Promise<unknown>>();
const uploading = ref(false);
const imageFileList = ref<UploadUserFile[]>([]);
const form = reactive({
  brand: '',
  custodianId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  locationId: undefined as number | undefined,
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

watch(visible, (opened) => {
  if (!opened) {
    return;
  }
  void getRuntimeSettings().then((settings) => {
    attachmentMaxMb.value = settings.attachmentMaxMb;
  }).catch(() => {});
  if (props.material) {
    Object.assign(form, {
      brand: props.material.brand ?? '',
      custodianId: props.material.custodianId ?? undefined,
      departmentId: props.material.departmentId ?? undefined,
      locationId: props.material.locationId ?? undefined,
      model: props.material.model ?? '',
      name: props.material.name,
      projectId: props.material.projectId,
      quantity: props.material.quantity,
      receivedDate: props.material.receivedDate ?? undefined,
      remark: props.material.remark ?? '',
      vendorName: props.material.vendorName ?? '',
    });
    imageFileList.value = (props.material.images ?? []).map((url, index) => ({
      name: url.split('/').pop() ?? url,
      status: 'success',
      uid: -(index + 1),
      url: assetImageUrl(url),
    }));
  } else {
    const projectId = props.defaultProjectId;
    Object.assign(form, {
      brand: '',
      custodianId: getDefaultCustodianId(props.projects, projectId),
      departmentId: undefined,
      locationId: undefined,
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
      .map((f) => f.url ?? (f.response as { url?: string } | undefined)?.url)
      .filter((u): u is string => !!u)
      .map((u) => stripImageToken(u)),
    locationId: form.locationId,
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
  const request = uploadAssetImageApi(options.file);
  pendingUploads.add(request);
  uploading.value = true;
  void request.then(() => {
    pendingUploads.delete(request);
    uploading.value = pendingUploads.size > 0;
  }, () => {
    pendingUploads.delete(request);
    uploading.value = pendingUploads.size > 0;
  });
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
  try {
    await Promise.all([...pendingUploads]);
    await nextTick();
    await (props.material
      ? updateMaterialApi(props.material.id, buildPayload())
      : createMaterialApi(buildPayload()));
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
    align-center
    :title="isEdit ? '编辑测试料件' : '新增测试料件'"
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
      <ElFormItem label="厂商/来源" required>
        <ElInput v-model="form.vendorName" placeholder="请输入寄件厂商名称" />
      </ElFormItem>
      <ElFormItem label="型号品牌" required>
        <div class="grid w-full grid-cols-2 gap-2">
          <ElInput v-model="form.model" placeholder="请输入型号" />
          <ElInput v-model="form.brand" placeholder="请输入品牌" />
        </div>
      </ElFormItem>
      <ElFormItem label="数量" required>
        <ElInputNumber v-model="form.quantity" :min="1" style="width: 100%" />
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
            :label="`${user.name}(${user.employeeNo})`"
            :value="user.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="接收日期" required>
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
          :http-request="customImageUpload"
          :limit="5"
          :on-exceed="onImageExceed"
          accept="image/png,image/jpeg,image/gif,image/webp"
          list-type="picture-card"
        >
          <span class="text-2xl">+</span>
        </ElUpload>
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
      <ElButton :loading="saving || uploading" type="primary" @click="debouncedSave">
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>
