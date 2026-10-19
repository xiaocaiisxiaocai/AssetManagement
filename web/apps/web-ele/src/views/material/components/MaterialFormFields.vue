<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { TestProjectItem } from '#/api/test-project';

import {
  ElDatePicker,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElUpload,
} from 'element-plus';

type FlatOption = { id: number; label: string };
type AuthenticatedUploadFile = { rawUrl?: string } & UploadUserFile;
type MaterialFormState = {
  brand: string;
  custodianId?: number;
  departmentId?: number;
  dockingVendorName: string;
  locationName: string;
  model: string;
  name: string;
  projectId?: number;
  quantity: number;
  receivedDate?: string;
  remark: string;
  vendorName: string;
};

defineProps<{
  beforeImageUpload: (file: File) => boolean;
  canUploadImages: boolean;
  customImageUpload: (options: UploadRequestOptions) => Promise<unknown>;
  isCustodianDepartmentRequired: boolean;
  isEdit: boolean;
  onImageExceed: () => void;
  onImageRemove: (file: AuthenticatedUploadFile) => void;
  projectLocked?: boolean;
  projects: TestProjectItem[];
  searchCustodians: (keyword: string) => Promise<void> | undefined;
  selectableDepartmentOptions: FlatOption[];
  syncCustodianDepartment: () => void;
  userOptionsLoading?: boolean;
  users: Array<{ employeeNo: string; id: number; name: string }>;
}>();

const form = defineModel<MaterialFormState>('form', { required: true });
const imageFileList = defineModel<AuthenticatedUploadFile[]>('imageFileList', {
  required: true,
});
</script>

<template>
  <div class="material-form-grid">
    <ElFormItem class="material-form-field--wide" label="料件名称" required>
      <ElInput
        v-model="form.name"
        :maxlength="100"
        placeholder="请输入料件名称"
        show-word-limit
      />
    </ElFormItem>
    <ElFormItem label="所属项目" required>
      <ElSelect
        v-model="form.projectId"
        :disabled="projectLocked"
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
      <ElInput
        v-model="form.vendorName"
        :maxlength="100"
        placeholder="请输入寄件厂商名称"
        show-word-limit
      />
    </ElFormItem>
    <ElFormItem class="material-form-field--wide" label="型号品牌">
      <div class="material-form-model">
        <ElInput
          v-model="form.model"
          :maxlength="100"
          placeholder="请输入型号"
          show-word-limit
        />
        <ElInput
          v-model="form.brand"
          :maxlength="100"
          placeholder="请输入品牌"
          show-word-limit
        />
      </div>
    </ElFormItem>
    <ElFormItem label="数量" required>
      <ElInputNumber
        v-model="form.quantity"
        :max="999999"
        :min="1"
        :precision="0"
        :step="1"
        style="width: 100%"
      />
    </ElFormItem>
    <ElFormItem label="对接厂商">
      <ElInput
        v-model="form.dockingVendorName"
        :maxlength="100"
        placeholder="请输入对接厂商名称"
        show-word-limit
      />
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
    <ElFormItem :required="isCustodianDepartmentRequired" label="归属部门">
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
    <ElFormItem label="保管人">
      <ElSelect
        v-model="form.custodianId"
        :disabled="isEdit"
        :loading="userOptionsLoading"
        :remote-method="searchCustodians"
        clearable
        filterable
        placeholder="选择保管人"
        remote
        style="width: 100%"
        @change="syncCustodianDepartment"
      >
        <ElOption
          v-for="user in users"
          :key="user.id"
          :label="`${user.name}(${user.employeeNo})`"
          :value="user.id"
        />
      </ElSelect>
    </ElFormItem>
    <ElFormItem class="material-form-field--wide" label="存放位置">
      <ElInput
        v-model="form.locationName"
        :maxlength="100"
        clearable
        placeholder="请输入存放位置"
        show-word-limit
      />
    </ElFormItem>
    <ElFormItem
      class="material-form-field--wide material-form-photo"
      label="料件照片"
    >
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
    <ElFormItem class="material-form-field--wide" label="备注">
      <ElInput
        v-model="form.remark"
        :maxlength="500"
        :rows="2"
        placeholder="可选"
        show-word-limit
        type="textarea"
      />
    </ElFormItem>
  </div>
</template>

<style scoped src="./MaterialFormDialog.css"></style>
