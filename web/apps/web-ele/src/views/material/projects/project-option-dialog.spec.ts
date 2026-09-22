import type {
  SaveTestProjectOptionPayload,
  TestProjectOption,
} from '#/api/test-project';

import { defineComponent, h, nextTick, ref } from 'vue';

import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';

import ProjectOptionDialog from './ProjectOptionDialog.vue';

const wrappers: ReturnType<typeof mount>[] = [];

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  document.body.innerHTML = '';
});

function mountDialog(initialKind: SaveTestProjectOptionPayload['kind']) {
  const options: TestProjectOption[] = [
    {
      code: 'type',
      id: 1,
      isActive: true,
      kind: 'project_type',
      label: '类型选项',
      sort: 0,
    },
    {
      code: 'progress',
      id: 2,
      isActive: true,
      kind: 'project_progress',
      label: '进度选项',
      sort: 0,
    },
  ];
  const activeKind = ref(initialKind);
  const form = ref<SaveTestProjectOptionPayload>({
    code: 'existing',
    isActive: true,
    kind: initialKind,
    label: '正在编辑的配置',
    sort: 0,
  });
  const editingId = ref<null | number>(1);
  const resetKinds: string[] = [];
  const wrapper = mount(
    defineComponent({
      setup: () => () =>
        h(ProjectOptionDialog, {
          activeKind: activeKind.value,
          canManage: true,
          displayedOptions: options.filter(
            (option) => option.kind === activeKind.value,
          ),
          editingId: editingId.value,
          form: form.value,
          'onUpdate:activeKind': (kind) => (activeKind.value = kind),
          'onUpdate:form': (value) => (form.value = value),
          onReset: (kind) => {
            resetKinds.push(kind);
            activeKind.value = kind;
            editingId.value = null;
            form.value = {
              code: '',
              isActive: true,
              kind,
              label: '',
              sort: 0,
            };
          },
          saving: false,
          visible: true,
        }),
    }),
    {
      attachTo: document.body,
      global: {
        stubs: {
          ElDialog: { template: '<div><slot /></div>' },
          ElTable: true,
        },
      },
    },
  );
  wrappers.push(wrapper);
  return { activeKind, editingId, form, resetKinds, wrapper };
}

describe('项目字典分组切换', () => {
  it.each([
    ['project_type', 'project_progress', '项目进度'],
    ['project_progress', 'project_type', '项目类型'],
  ] as const)(
    '从 %s 切换到 %s 时清除旧编辑状态并使用目标分组',
    async (from, to, label) => {
      const state = mountDialog(from);
      await nextTick();
      const tab = state.wrapper.get(`#tab-${to}`);
      expect(tab.text()).toBe(label);
      await tab.trigger('click');
      await nextTick();

      expect(state.resetKinds).toEqual([to]);
      expect(state.activeKind.value).toBe(to);
      expect(state.form.value.kind).toBe(to);
      expect(state.form.value.label).toBe('');
      expect(state.editingId.value).toBeNull();
      expect(
        state.wrapper.findComponent({ name: 'ElTable' }).props('data'),
      ).toEqual([expect.objectContaining({ kind: to })]);
    },
  );

  it('连续切换分组后列表和新增表单保持一致', async () => {
    const state = mountDialog('project_type');
    await nextTick();
    for (const kind of [
      'project_progress',
      'project_type',
      'project_progress',
    ] as const) {
      await state.wrapper.get(`#tab-${kind}`).trigger('click');
      await nextTick();
      expect(state.activeKind.value).toBe(kind);
      expect(state.form.value.kind).toBe(kind);
      expect(
        state.wrapper.findComponent({ name: 'ElTable' }).props('data'),
      ).toEqual([expect.objectContaining({ kind })]);
    }
  });

  it('点击当前分组保留正在编辑的配置', async () => {
    const state = mountDialog('project_type');
    await nextTick();
    await state.wrapper.get('#tab-project_type').trigger('click');
    expect(state.resetKinds).toEqual([]);
    expect(state.form.value.label).toBe('正在编辑的配置');
    expect(state.editingId.value).toBe(1);
  });
});
