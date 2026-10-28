import type { WorkspaceReportData } from './project-workspace-report';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  buildWorkspaceReportHtml,
  escapeHtml,
  loadWorkspaceReportData,
  workspaceReportTitle,
} from './project-workspace-report';

const mocks = vi.hoisted(() => ({
  detail: vi.fn(),
  followups: vi.fn(),
  materials: vi.fn(),
  workspace: vi.fn(),
}));

vi.mock('#/api/material', () => ({
  getMaterialDetailApi: mocks.detail,
  listMaterialsApi: mocks.materials,
}));
vi.mock('#/api/project-workspace', () => ({
  getProjectWorkspaceApi: mocks.workspace,
}));
vi.mock('#/api/test-project', () => ({
  listTestProjectFollowupsApi: mocks.followups,
}));

function project(overrides: Record<string, unknown> = {}) {
  return {
    code: 'QA-01',
    id: 7,
    isDeleted: false,
    name: '机械手测试',
    ownerName: '张三',
    progressLabel: '测试中',
    projectTypeLabel: '样机测试',
    startDate: '2026-10-01',
    ...overrides,
  } as unknown as WorkspaceReportData['project'];
}

function reportData(
  overrides: Partial<WorkspaceReportData> = {},
): WorkspaceReportData {
  return {
    documents: [],
    flows: [],
    followups: [],
    generatedAt: new Date(2026, 9, 27, 9, 30),
    materials: [],
    project: project(),
    stages: [],
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('项目工作台导出报告', () => {
  it('文件名替换非法字符，编号为空时省略', () => {
    const date = new Date(2026, 9, 26);
    expect(workspaceReportTitle('光电/模块', 'TM:01', date)).toBe(
      '光电_模块-TM_01-20261026',
    );
    expect(workspaceReportTitle('项目', '  ', date)).toBe('项目-20261026');
  });

  it('转义用户输入，报告中不出现可执行标记与按钮', () => {
    expect(escapeHtml(`<b>"a"&'b'</b>`)).toBe(
      '&lt;b&gt;&quot;a&quot;&amp;&#39;b&#39;&lt;/b&gt;',
    );
    const html = buildWorkspaceReportHtml(
      reportData({
        materials: [
          {
            id: 1,
            materialNo: 'TM-001',
            name: '<script>alert(1)</script>',
            quantity: 2,
            status: 0,
          } as never,
        ],
      }),
    );
    expect(html).not.toContain('<script>alert(1)');
    expect(html).toContain('&lt;script&gt;alert(1)&lt;/script&gt;');
    expect(html).not.toContain('<button');
  });

  it('按日期推导当前阶段与阶段进度，并列出各阶段的报告附件', () => {
    const html = buildWorkspaceReportHtml(
      reportData({
        documents: [
          { fileName: '阶段1报告.docx', id: 1, stageId: 11 } as never,
        ],
        stages: [
          {
            endDate: '2026-10-05',
            id: 11,
            name: '功能测试',
            sequence: 1,
            startDate: '2026-10-01',
          } as never,
          {
            id: 12,
            name: '耐久测试',
            sequence: 2,
            startDate: '2026-10-06',
          } as never,
        ],
      }),
    );
    expect(html).toContain('2. 耐久测试');
    expect(html).toContain('1 / 2');
    expect(html).toContain('阶段1报告.docx');
    expect(html).toContain('2026-10-06 至 —');
  });

  it('没有数据的区块显示空状态，流转状态显示中文', () => {
    const empty = buildWorkspaceReportHtml(reportData());
    for (const text of [
      '暂无料件',
      '暂无测试阶段',
      '暂无流转记录',
      '暂无跟进记录',
    ]) {
      expect(empty).toContain(text);
    }
    const withFlow = buildWorkspaceReportHtml(
      reportData({
        flows: [
          {
            applyTime: '2026-10-20T08:00:00Z',
            flowNo: 'MF-001',
            id: 1,
            status: 'receiving',
          } as never,
        ],
      }),
    );
    expect(withFlow).toContain('待接收');
  });

  it('取齐全部料件分页，合并各料件流转并去重、按时间倒序', async () => {
    mocks.materials
      .mockResolvedValueOnce({
        items: [{ id: 1 }, { id: 2 }],
        page: 1,
        pageSize: 200,
        total: 3,
      })
      .mockResolvedValueOnce({
        items: [{ id: 3 }],
        page: 2,
        pageSize: 200,
        total: 3,
      });
    const flowA = { applyTime: '2026-10-01T00:00:00Z', id: 10 };
    const flowB = { applyTime: '2026-10-03T00:00:00Z', id: 11 };
    mocks.detail.mockImplementation(async (id: number) => ({
      flows: id === 3 ? [flowB] : [flowA],
    }));
    mocks.workspace.mockResolvedValue({ documents: [], stages: [] });
    mocks.followups.mockResolvedValue([
      { filledAt: '2026-10-01T00:00:00Z', id: 1 },
      { filledAt: '2026-10-09T00:00:00Z', id: 2 },
    ]);

    const data = await loadWorkspaceReportData(project());

    expect(mocks.materials).toHaveBeenCalledTimes(2);
    expect(mocks.materials).toHaveBeenNthCalledWith(1, {
      deleteStatus: 'active',
      page: 1,
      pageSize: 200,
      projectId: 7,
    });
    expect(data.materials.map((item) => item.id)).toEqual([1, 2, 3]);
    expect(data.flows.map((flow) => flow.id)).toEqual([11, 10]);
    expect(data.followups.map((item) => item.id)).toEqual([2, 1]);
  });
});
