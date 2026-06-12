<script lang="ts" setup>
import { ref, nextTick, h } from 'vue';
import type { VxeFormPropTypes } from 'vxe-pc-ui';
import { getSingle, submitData } from '#/api/system/role';
import { ReRoleTreeSelect } from '#/components/role';
import { $t } from '@vben/locales';
const emits = defineEmits<{ (e: 'reload'): void }>();
const reModalRef = ref();
interface AddRoleInput {
  parentId: number | null;
  name: string;
  description: string;
  remark: string;
}
const formRef = ref();
const defaultFormData = () => {
  return {
    name: '',
    parentId: null,
    description: '',
    remark: '',
  };
};
const formData = ref<AddRoleInput>(defaultFormData());
const formItems = ref<VxeFormPropTypes.Items>([
  {
    field: 'name',
    title: $t('role.form.name'),
    span: 24,
    itemRender: {
      name: '$input',
      props: { placeholder: $t('role.form.placeholder.name') },
    },
  },
  {
    field: 'parentId',
    title: $t('role.form.parentId'),
    span: 24,
    slots: {
      default: ({ data }) => [
        h(ReRoleTreeSelect, {
          modelValue: data.parentId,
          placeholder: $t('role.form.placeholder.parentId'),
          onNodeClick(nodeData: any) {
            formData.value.parentId = nodeData.id;
            formRef.value?.validateField('parentId');
          },
        }),
      ],
    },
  },
  {
    field: 'description',
    title: $t('role.form.description'),
    span: 24,
    itemRender: {
      name: '$textarea',
      props: { placeholder: $t('role.form.placeholder.description') },
    },
  },
  {
    field: 'remark',
    title: $t('role.form.remark'),
    span: 24,
    itemRender: {
      name: '$textarea',
      props: { placeholder: $t('role.form.placeholder.remark') },
    },
  },
]);
const formRules = ref<VxeFormPropTypes.Rules>({
  name: [{ required: true, message: $t('role.form.validate.name') }],
  parentId: [{ required: true, message: $t('role.form.validate.parentId') }],
});

const showAddModal = () => {
  reModalRef.value.show($t('role.add'));
  formData.value = defaultFormData();
  nextTick(() => {
    formRef.value.clearValidate();
  });
};
const showEditModal = (record: any) => {
  reModalRef.value.show($t('role.edit') + `->${record.name}`);
  nextTick(() => {
    formRef.value.clearValidate();
    getSingle(record.id).then((data: any) => {
      formData.value = data;
    });
  });
};
const showViewModal = (record: any) => {
  reModalRef.value.show($t('role.view') + `->${record.name}`, true);
  nextTick(() => {
    formRef.value.clearValidate();
    getSingle(record.id).then((data: any) => {
      formData.value = data;
    });
  });
};
const handleSubmit = async () => {
  const validate = await formRef.value.validate();
  if (!validate) {
    submitData(formData.value).then(() => {
      emits('reload');
      reModalRef.value.close();
    });
  }
};

defineExpose({ showAddModal, showEditModal, showViewModal });
</script>
<template>
  <re-modal ref="reModalRef" @submit="handleSubmit">
    <vxe-form
      ref="formRef"
      :data="formData"
      :items="formItems"
      :rules="formRules"
      :titleWidth="100"
      :titleColon="true"
      :titleAlign="`right`"
    />
  </re-modal>
</template>
