import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  createProjectStageApi,
  deleteProjectStageApi,
  downloadProjectDocumentApi,
  downloadProjectPackageApi,
  downloadProjectReportApi,
  getProjectWorkspaceApi,
  updateProjectStageApi,
  uploadProjectDocumentApi,
} from './project-workspace';

const client = vi.hoisted(() => ({
  delete: vi.fn(),
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));
vi.mock('#/api/request', () => ({ requestClient: client }));
beforeEach(() => {
  for (const method of Object.values(client))
    method.mockReset().mockResolvedValue({ data: {} });
});

describe('项目测试工作台接口契约', () => {
  it('查询、新增、修改和删除使用相同项目工作台前缀并传递版本', async () => {
    const payload = {
      name: '环境测试',
      rowVersion: 3,
      sequence: 1,
      status: 'planned' as const,
    };
    await getProjectWorkspaceApi(7);
    await createProjectStageApi(7, payload);
    await updateProjectStageApi(7, 9, payload);
    await deleteProjectStageApi(7, 9, 3);
    expect(client.get).toHaveBeenCalledWith('/test-projects/7/workspace');
    expect(client.post).toHaveBeenCalledWith(
      '/test-projects/7/workspace/stages',
      payload,
    );
    expect(client.put).toHaveBeenCalledWith(
      '/test-projects/7/workspace/stages/9',
      payload,
    );
    expect(client.delete).toHaveBeenCalledWith(
      '/test-projects/7/workspace/stages/9',
      { params: { rowVersion: 3 } },
    );
  });
  it('上传保留阶段归属与原文件，下载接口使用二进制响应', async () => {
    const file = new File(['%PDF-1.4'], '阶段报告.pdf');
    await uploadProjectDocumentApi(7, file, 9, '  第一阶段结论  ');
    const uploadCall = client.post.mock.calls[0];
    if (!uploadCall) throw new Error('应发送报告上传请求');
    const [path, form] = uploadCall;
    expect(path).toBe('/test-projects/7/workspace/documents');
    expect(form.get('file').name).toBe(file.name);
    expect(form.get('stageId')).toBe('9');
    expect(form.get('description')).toBe('第一阶段结论');
    await downloadProjectDocumentApi(7, 11);
    await downloadProjectReportApi(7, 9);
    await downloadProjectPackageApi(7);
    expect(client.get).toHaveBeenCalledWith(
      '/test-projects/7/workspace/documents/11/download',
      { responseType: 'blob' },
    );
    expect(client.get).toHaveBeenCalledWith(
      '/test-projects/7/workspace/report',
      { params: { stageId: 9 }, responseType: 'blob' },
    );
    expect(client.get).toHaveBeenCalledWith(
      '/test-projects/7/workspace/package',
      { params: undefined, responseType: 'blob' },
    );
  });
});
