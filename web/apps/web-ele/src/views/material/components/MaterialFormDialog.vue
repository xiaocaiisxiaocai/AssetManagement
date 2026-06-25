<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { MaterialItem, SaveMaterialPayload } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';
import type { UserDto } from '#/api/user';

import { computed, reactive, ref, watch } from 'vue';

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
  ElUpload,
} from 'element-plus';

import { assetImageUrl, stripImageToken, uploadAssetImageApi } from '#/api/asset';
import { createMaterialApi, updateMaterialApi } from '#/api/material';

type FlatOption = { id: number; label: string };

const props = defineProps<{
  departmentOptions: FlatOption[];
  locationOptions: FlatOption[];
  material: MaterialItem | null;
  projects: TestProjectItem[];
  users: UserDto[];
}>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
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

watch(visible, (opened) => {
  if (!opened) {
    return;
  }
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
    Object.assign(form, {
      brand: '',
      custodianId: undefined,
      departmentId: undefined,
      locationId: undefined,
      model: '',
      name: '',
      projectId: undefined,
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
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('单张图片大小不能超过 5MB');
    return false;
  }
  return true;
}

async function customImageUpload(options: UploadRequestOptions) {
  return await uploadAssetImageApi(options.file);
}

function onImageExceed() {
  ElMessage.warning('最多上传 5 张照片');
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写料件名称');
    return;
  }
  if (!form.projectId) {
    ElMessage.warning('请选择所属测试项目');
    return;
  }
  saving.value = true;
  try {
    await (props.material
      ? updateMaterialApi(props.material.id, buildPayload())
      : createMaterialApi(buildPayload()));
    ElMessage.success('保存成功');
    visible.value = false;
    emit('saved');
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
    width="600px"
  >
    <ElForm label-width="96px">
      <ElFormItem label="料件名称">
        <ElInput v-model="form.name" placeholder="必填" />
      </ElFormItem>
      <ElFormItem label="所属项目">
        <ElSelect
          v-model="form.projectId"
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
        <ElInput v-model="form.vendorName" placeholder="寄件厂商名称" />
      </ElFormItem>
      <ElFormItem label="型号品牌">
        <div class="grid w-full grid-cols-2 gap-2">
          <ElInput v-model="form.model" placeholder="型号" />
          <ElInput v-model="form.brand" placeholder="品牌" />
        </div>
      </ElFormItem>
      <ElFormItem label="数量">
        <ElInput v-model.number="form.quantity" />
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
      <ElFormItem label="保管人">
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
      <ElButton :loading="saving" type="primary" @click="debouncedSave">
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>
