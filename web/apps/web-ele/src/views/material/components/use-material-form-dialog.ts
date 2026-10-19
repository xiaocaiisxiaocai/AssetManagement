import type { MaterialItem, SaveMaterialPayload } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';
import type { UserDto, UserOptionDto } from '#/api/user';

import type { Ref } from 'vue';
import { computed, nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';

import { useDebounceFn } from '@vueuse/core';
import { ElMessage } from 'element-plus';

import { createMaterialApi, updateMaterialApi } from '#/api/material';
import { getRuntimeSettings } from '#/utils/runtime-settings';
import { buildFileActionAccess } from '#/views/permissions/action-access';

import {
  createMaterialFormImages,
  type MaterialFormState,
} from './material-form-images';
import {
  getDefaultCustodianId,
  nextCustodianDepartmentId,
  resolveCustodianDepartment,
  validateCustodianDepartment,
  validateMaterialForm,
} from './material-form-rules';

type FlatOption = { id: number; label: string };

export type MaterialFormDialogProps = {
  defaultProjectId?: number;
  departmentOptions: FlatOption[];
  material: MaterialItem | null;
  projectLocked?: boolean;
  projects: TestProjectItem[];
  searchUsers?: (
    keyword: string,
    keepIds?: Array<null | number | undefined>,
  ) => Promise<void>;
  userOptionsLoading?: boolean;
  users: (UserDto | UserOptionDto)[];
};

export function useMaterialFormDialog(options: {
  onSaved: () => void;
  props: MaterialFormDialogProps;
  visible: Ref<boolean>;
}) {
  const { onSaved, props, visible } = options;
  const { hasAccessByCodes } = useAccess();
  const canUploadImages = computed(
    () => buildFileActionAccess(hasAccessByCodes).canUploadAndPreview,
  );
  const saving = ref(false);
  const images = createMaterialFormImages();
  const form = reactive<MaterialFormState>({
    brand: '',
    custodianId: undefined,
    departmentId: undefined,
    dockingVendorName: '',
    locationName: '',
    model: '',
    name: '',
    projectId: undefined,
    quantity: 1,
    receivedDate: undefined,
    remark: '',
    vendorName: '',
  });

  function searchCustodians(keyword: string) {
    return props.searchUsers?.(keyword, [form.custodianId]);
  }

  const isEdit = computed(() => props.material !== null);
  const custodianDepartment = computed(() =>
    resolveCustodianDepartment(
      props.users,
      props.departmentOptions,
      form.custodianId,
    ),
  );
  const isCustodianDepartmentRequired = computed(
    () =>
      custodianDepartment.value.resolved &&
      custodianDepartment.value.departmentId !== undefined,
  );
  const selectableDepartmentOptions = computed(() => {
    const departmentOptions = [...props.departmentOptions];
    const currentId = props.material?.departmentId;
    if (currentId && !departmentOptions.some((item) => item.id === currentId)) {
      departmentOptions.unshift({
        id: currentId,
        label: `${props.material?.departmentName || '原归属部门'}（停用）`,
      });
    }
    return departmentOptions;
  });

  onBeforeUnmount(() => {
    images.dispose();
  });

  function syncCustodianDepartment() {
    if (isEdit.value) return;
    form.departmentId = nextCustodianDepartmentId(
      form.departmentId,
      form.custodianId,
      custodianDepartment.value,
    );
  }

  watch([() => props.users, () => props.departmentOptions], () => {
    if (visible.value && !isEdit.value) syncCustodianDepartment();
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
      })
      .catch(() => {});
    if (props.material) {
      Object.assign(form, {
        brand: props.material.brand ?? '',
        custodianId: props.material.custodianId ?? undefined,
        departmentId: props.material.departmentId ?? undefined,
        dockingVendorName: props.material.dockingVendorName ?? '',
        locationName: props.material.locationName ?? '',
        model: props.material.model ?? '',
        name: props.material.name,
        projectId: props.material.projectId,
        quantity: props.material.quantity,
        receivedDate: props.material.receivedDate ?? undefined,
        remark: props.material.remark ?? '',
        vendorName: props.material.vendorName ?? '',
      });
      await images.loadExistingImages(
        () => props.material?.images,
        generation,
        () => visible.value,
      );
    } else {
      const projectId = props.defaultProjectId;
      Object.assign(form, {
        brand: '',
        custodianId: getDefaultCustodianId(props.projects, projectId),
        departmentId: undefined,
        dockingVendorName: '',
        locationName: '',
        model: '',
        name: '',
        projectId,
        quantity: 1,
        receivedDate: undefined,
        remark: '',
        vendorName: '',
      });
      syncCustodianDepartment();
      images.clearFiles();
    }
  });

  function buildPayload(): SaveMaterialPayload {
    return {
      brand: form.brand,
      custodianId: form.custodianId,
      departmentId: form.departmentId,
      dockingVendorName: form.dockingVendorName.trim() || null,
      images: images.imageFileList.value
        .map(
          (file) =>
            file.rawUrl ??
            (file.response as { rawUrl?: string } | undefined)?.rawUrl,
        )
        .filter((url): url is string => !!url),
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

  async function save() {
    const validationMessage =
      validateMaterialForm(form) ||
      validateCustodianDepartment(
        form,
        props.users,
        props.departmentOptions,
        isEdit.value,
      );
    if (validationMessage) {
      ElMessage.warning(validationMessage);
      return;
    }
    saving.value = true;
    const sessionToken = images.pendingUploads.token();
    try {
      await Promise.all(images.pendingUploads.pending(sessionToken));
      if (!images.pendingUploads.isCurrent(sessionToken) || !visible.value)
        return;
      await nextTick();
      await (props.material
        ? updateMaterialApi(props.material.id, buildPayload())
        : createMaterialApi(buildPayload()));
      if (!images.pendingUploads.isCurrent(sessionToken) || !visible.value)
        return;
      ElMessage.success('保存成功');
      visible.value = false;
      onSaved();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      saving.value = false;
    }
  }

  const debouncedSave = useDebounceFn(save, 300);

  return {
    beforeImageUpload: images.beforeImageUpload,
    canUploadImages,
    customImageUpload: images.customImageUpload,
    debouncedSave,
    form,
    imageFileList: images.imageFileList,
    isCustodianDepartmentRequired,
    isEdit,
    onImageExceed: images.onImageExceed,
    onImageRemove: images.onImageRemove,
    saving,
    searchCustodians,
    selectableDepartmentOptions,
    syncCustodianDepartment,
    uploading: images.uploading,
  };
}
