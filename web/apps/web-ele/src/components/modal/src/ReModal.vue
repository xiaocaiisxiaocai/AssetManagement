<script setup lang="ts">
import { reactive, ref } from 'vue';

const props = withDefaults(
  defineProps<{
    heigh?: number;
    width?: number;
  }>(),
  {
    heigh: 600,
    width: 800,
  },
);
const emits = defineEmits<{
  (e: 'submit'): void;
}>();
const vxeModalRef = ref();
const modalOptions = reactive({
  value: false,
  title: '',
  readonly: false,
});

const show = (title: string, readonly?: boolean) => {
  modalOptions.title = title;
  modalOptions.readonly = readonly ?? false;
  modalOptions.value = true;
};
const close = () => {
  modalOptions.value = false;
};
defineExpose({ show, close });
</script>

<template>
  <vxe-modal
    ref="vxeModalRef"
    show-footer
    v-bind="$attrs"
    v-model="modalOptions.value"
    :height="props.heigh"
    :title="modalOptions.title"
    :width="props.width"
  >
    <template #default><slot name="default"></slot></template>
    <template #footer>
      <vxe-button
        v-if="!modalOptions.readonly"
        :content="$t(`common.cancel`)"
        size="small"
        @click="modalOptions.value = false"
      />
      <vxe-button
        v-if="!modalOptions.readonly"
        :content="$t(`common.save`)"
        size="small"
        status="primary"
        @click="emits('submit')"
      />
    </template>
  </vxe-modal>
</template>
