import { describe, expect, it } from 'vitest';

import { getWorkflowProgressEmptyText } from './workflow-progress-empty-text';

describe('审批流转空状态文案', () => {
  it('区分自动通过、待接收和已确认的直接转移', () => {
    expect(getWorkflowProgressEmptyText({ status: 'approved' })).toBe(
      '无需人工审批，申请已自动通过',
    );
    expect(
      getWorkflowProgressEmptyText({
        source: 'material',
        status: 'receiving',
      }),
    ).toBe('待接收人确认，保管人尚未变更');
    expect(
      getWorkflowProgressEmptyText({
        directTransfer: true,
        source: 'material',
        status: 'approved',
      }),
    ).toBe('接收人已确认，无需审批');
  });

  it('为撤回和驳回申请保留明确状态', () => {
    expect(getWorkflowProgressEmptyText({ status: 'withdrawn' })).toBe(
      '申请已撤回，暂无可展示的审批节点',
    );
    expect(getWorkflowProgressEmptyText({ status: 'rejected' })).toBe(
      '申请已驳回，暂无可展示的审批节点',
    );
  });
});
