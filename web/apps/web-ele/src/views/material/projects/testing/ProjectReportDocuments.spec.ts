import type { ProjectDocumentDto } from '#/api/project-workspace';

import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';

import ProjectReportDocuments from './ProjectReportDocuments.vue';

const wrappers: ReturnType<typeof mount>[] = [];

function document(
  id: number,
  stageId: number,
  fileName: string,
): ProjectDocumentDto {
  return {
    contentType: 'application/pdf',
    fileName,
    id,
    projectId: 1,
    size: 100,
    stageId,
    uploadedAt: '2026-10-19T01:00:00Z',
    uploadedBy: '测试负责人',
  };
}

function setup(canManage = true, stageId = 3, canDownload = true) {
  const wrapper = mount(ProjectReportDocuments, {
    props: {
      canDownload,
      canManage,
      deletingDocumentId: null,
      documents: [
        document(1, 3, '第一阶段报告.pdf'),
        document(2, 4, '第二阶段报告.pdf'),
      ],
      downloadingKey: '',
      scopeName: '性能测试',
      stageId,
      uploading: false,
      writeBusy: false,
    },
    global: {
      stubs: {
        ElButton: { template: '<button><slot /></button>' },
        ElPopover: { template: '<div><slot name="reference" /><slot /></div>' },
      },
    },
  });
  wrappers.push(wrapper);
  return wrapper;
}

async function selectFile(wrapper: ReturnType<typeof setup>, file: File) {
  const input = wrapper.get('input[type="file"]');
  Object.defineProperty(input.element, 'files', {
    configurable: true,
    value: [file],
  });
  await input.trigger('change');
}

afterEach(() => wrappers.splice(0).forEach((wrapper) => wrapper.unmount()));

describe('阶段行报告附件', () => {
  it('只显示本阶段附件，文件名点击直接下载绑定文档', async () => {
    const wrapper = setup();
    expect(wrapper.text()).toContain('第一阶段报告.pdf');
    expect(wrapper.text()).not.toContain('第二阶段报告.pdf');
    expect(wrapper.text()).not.toContain('项目总结.pdf');
    const link = wrapper
      .findAll('button')
      .find((button) => button.text() === '第一阶段报告.pdf');
    await link?.trigger('click');
    expect(wrapper.emitted('download')?.[0]?.[0]).toMatchObject({
      id: 1,
      stageId: 3,
    });
  });

  it('阶段行直接选文件上传，自动绑定当前阶段，无需再次选择归属', async () => {
    const wrapper = setup();
    await wrapper.get('[aria-label="上传性能测试报告"]').trigger('click');
    const file = new File(['%PDF-1.4'], '耐久验证.pdf', {
      type: 'application/pdf',
    });
    await selectFile(wrapper, file);
    expect(wrapper.emitted('upload')).toEqual([[file, 3, '']]);
    expect(wrapper.find('select').exists()).toBe(false);
  });

  it('文件选择期间阶段切换时不将原文件传到新阶段', async () => {
    const wrapper = setup();
    await wrapper.get('[aria-label="上传性能测试报告"]').trigger('click');
    await wrapper.setProps({ stageId: 4 });
    await selectFile(wrapper, new File(['pdf'], '原阶段报告.pdf'));
    expect(wrapper.emitted('upload')).toBeUndefined();
  });

  it('没有导出权限时只显示文件名，不能下载原件', () => {
    const wrapper = setup(false, 3, false);
    expect(wrapper.text()).toContain('第一阶段报告.pdf');
    expect(
      wrapper
        .findAll('button')
        .some((button) => button.text() === '第一阶段报告.pdf'),
    ).toBe(false);
  });

  it('只读用户可以下载，不能上传或删除报告', () => {
    const wrapper = setup(false);
    expect(wrapper.find('[aria-label="上传性能测试报告"]').exists()).toBe(
      false,
    );
    expect(
      wrapper.findAll('button').some((button) => button.text() === '删除'),
    ).toBe(false);
    expect(wrapper.text()).toContain('第一阶段报告.pdf');
  });
});
