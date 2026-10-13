<script lang="ts" setup>
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import type { AssetStatus } from '#/api/asset';

import {
  ElDatePicker,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTag,
  ElUpload,
} from 'element-plus';

type FlatOption = { code?: string; id: number; label: string };
type AuthenticatedUploadFile = { rawUrl?: string } & UploadUserFile;
type AssetFormState = {
  categoryId: number;
  currentCondition: string;
  custodianId?: number;
  departmentId?: number;
  locationName: string;
  name: string;
  purchaseDate: string;
  quantity: number;
  registrationTime: string;
  remark: string;
  status: AssetStatus;
};

defineProps<{
  assetNoPreview: string;
  beforeImageUpload: (file: File) => boolean;
  canUploadImages: boolean;
  categoryOptions: FlatOption[];
  customImageUpload: (options: UploadRequestOptions) => Promise<unknown>;
  isEdit: boolean;
  onImageExceed: () => void;
  onImageRemove: (file: AuthenticatedUploadFile) => void;
  searchCustodians: (keyword: string) => Promise<void> | undefined;
  selectableConditionOptions: string[];
  selectableDepartmentOptions: FlatOption[];
  statusOptions: Array<{ label: string; value: AssetStatus }>;
  userOptionsLoading?: boolean;
  users: Array<{ employeeNo: string; id: number; name: string }>;
}>();

const form = defineModel<AssetFormState>('form', { required: true });
const imageFileList = defineModel<AuthenticatedUploadFile[]>('imageFileList', {
  required: true,
});
</script>

<template>
  <div class="asset-form-grid">
    <ElFormItem class="asset-form-field--wide" label="资产名称" required>
      <ElInput
        v-model="form.name"
        :maxlength="100"
        placeholder="请输入资产名称"
        show-word-limit
      />
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
    <ElFormItem label="保管人" required>
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
      >
        <ElOption
          v-for="user in users"
          :key="user.id"
          :label="`${user.name}（${user.employeeNo}）`"
          :value="user.id"
        />
      </ElSelect>
    </ElFormItem>
    <ElFormItem class="asset-form-field--wide" label="存放位置" required>
      <ElInput
        v-model="form.locationName"
        :maxlength="100"
        clearable
        placeholder="请输入存放位置，如：三楼研发区 A-12"
        show-word-limit
      />
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
    <ElFormItem :class="{ 'asset-form-field--wide': !isEdit }" label="目前状况">
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
    <ElFormItem v-if="isEdit" label="状态">
      <ElSelect v-model="form.status" disabled style="width: 100%">
        <ElOption
          v-for="item in statusOptions"
          :key="item.value"
          :label="item.label"
          :value="item.value"
        />
      </ElSelect>
    </ElFormItem>
    <ElFormItem class="asset-form-field--wide" label="备注">
      <ElInput
        v-model="form.remark"
        :rows="2"
        maxlength="500"
        placeholder="请输入备注"
        show-word-limit
        type="textarea"
      />
    </ElFormItem>
    <ElFormItem
      class="asset-form-field--wide asset-form-photo"
      label="资产照片"
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
  </div>
</template>

<style scoped src="./AssetFormDialog.css"></style>
