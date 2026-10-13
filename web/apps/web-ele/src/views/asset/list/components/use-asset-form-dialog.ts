import type { AssetItem, AssetPayload, AssetStatus } from '#/api/asset';
import type { UserDto, UserOptionDto } from '#/api/user';

import type { Ref } from 'vue';
import { computed, nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';

import { useDebounceFn } from '@vueuse/core';
import { ElMessage } from 'element-plus';

import { createAssetApi, updateAssetApi } from '#/api/asset';
import { businessDateText } from '#/utils/business-date';
import { getRuntimeSettings } from '#/utils/runtime-settings';
import { buildFileActionAccess } from '#/views/permissions/action-access';

import { createAssetFormImages } from './asset-form-images';
import { validateAssetForm } from './asset-form-rules';

type FlatOption = { code?: string; id: number; label: string };

export type AssetFormDialogProps = {
  asset: AssetItem | null;
  categoryOptions: FlatOption[];
  defaultCategoryId: number;
  departmentOptions: FlatOption[];
  searchUsers?: (
    keyword: string,
    keepIds?: Array<null | number | undefined>,
  ) => Promise<void>;
  userOptionsLoading?: boolean;
  users: (UserDto | UserOptionDto)[];
};

export function useAssetFormDialog(options: {
  onSaved: () => void;
  props: AssetFormDialogProps;
  visible: Ref<boolean>;
}) {
  const { onSaved, props, visible } = options;
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
  const conditionOptions = ref([
    '正常使用',
    '轻微损坏',
    '待维修',
    '维修中',
    '停用',
  ]);
  const images = createAssetFormImages();
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
    const departmentOptions = [...props.departmentOptions];
    const currentId = props.asset?.departmentId;
    if (currentId && !departmentOptions.some((item) => item.id === currentId)) {
      departmentOptions.unshift({
        id: currentId,
        label: `${props.asset?.departmentName || '原归属部门'}（停用）`,
      });
    }
    return departmentOptions;
  });
  // 只有第三级分类可选；历史资产仍挂在上级分类时保留原值，避免下拉框显示裸 ID。
  const selectableCategoryOptions = computed(() => {
    const categoryOptions = [...props.categoryOptions];
    const currentId = props.asset?.categoryId;
    if (currentId && !categoryOptions.some((item) => item.id === currentId)) {
      categoryOptions.unshift({
        code: props.asset?.categoryCode ?? '',
        id: currentId,
        label: `${props.asset?.categoryCode || '原分类'}（非第三级）`,
      });
    }
    return categoryOptions;
  });
  const assetNoPreview = computed(() => {
    const cat = selectableCategoryOptions.value.find(
      (item) => item.id === form.categoryId,
    );
    return cat?.code ? `${cat.code}-自动流水` : '选择分类后生成';
  });

  onBeforeUnmount(() => {
    images.dispose();
  });

  watch(visible, async (opened) => {
    const generation = images.nextGeneration();
    if (!opened) {
      images.closeSession();
      return;
    }
    images.openSession();
    void getRuntimeSettings()
      .then((settings) => {
        images.attachmentMaxMb.value = settings.attachmentMaxMb;
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
      await images.loadExistingImages(
        () => props.asset?.images,
        generation,
        () => visible.value,
      );
    } else {
      Object.assign(form, {
        // 左侧树选中的一、二级分类不能直接挂资产，不预填。
        categoryId: props.categoryOptions.some(
          (item) => item.id === props.defaultCategoryId,
        )
          ? props.defaultCategoryId
          : 0,
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
      images.clearFiles();
    }
  });

  function buildPayload(): AssetPayload {
    return {
      categoryId: form.categoryId,
      custodianId: form.custodianId,
      departmentId: form.departmentId,
      images: images.imageFileList.value
        .map(
          (file) =>
            file.rawUrl ??
            (file.response as { rawUrl?: string } | undefined)?.rawUrl,
        )
        .filter((url): url is string => !!url),
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

  function searchCustodians(keyword: string) {
    return props.searchUsers?.(keyword, [form.custodianId]);
  }

  async function save() {
    const error = validateAssetForm(form, props.users, props.asset !== null);
    if (error) {
      ElMessage.warning(error);
      return;
    }
    saving.value = true;
    const sessionToken = images.pendingUploads.token();
    try {
      await Promise.all(images.pendingUploads.pending(sessionToken));
      if (!images.pendingUploads.isCurrent(sessionToken) || !visible.value)
        return;
      await nextTick();
      await (props.asset
        ? updateAssetApi(props.asset.id, buildPayload())
        : createAssetApi(buildPayload()));
      if (!images.pendingUploads.isCurrent(sessionToken) || !visible.value)
        return;
      ElMessage.success('保存成功');
      visible.value = false;
      onSaved();
    } catch {
      // 请求错误已由统一请求层提示；关闭会话产生的 AbortError 无需额外提示。
    } finally {
      saving.value = false;
    }
  }

  // 防抖版本的保存方法,防止用户快速点击导致重复提交
  const debouncedSave = useDebounceFn(save, 300);

  return {
    assetNoPreview,
    beforeImageUpload: images.beforeImageUpload,
    canUploadImages,
    customImageUpload: images.customImageUpload,
    debouncedSave,
    form,
    imageFileList: images.imageFileList,
    isEdit,
    onImageExceed: images.onImageExceed,
    onImageRemove: images.onImageRemove,
    saving,
    searchCustodians,
    selectableCategoryOptions,
    selectableConditionOptions,
    selectableDepartmentOptions,
    statusOptions,
    uploading: images.uploading,
  };
}
