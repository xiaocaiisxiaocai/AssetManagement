import { describe, expect, it } from 'vitest';

import {
  approvalListRequestFlowId,
  beginNotificationFlowAttempt,
  notificationSource,
  withoutNotificationFlowId,
} from './notification-flow-attempt';

describe('审批通知深链消费', () => {
  it('首次请求时立即标记已消费，不依赖目标是否命中', () => {
    const first = beginNotificationFlowAttempt(
      '42',
      'material',
      'material',
      '',
    );
    expect(first).toMatchObject({
      consumedKey: 'material:42',
      requestedFlowId: 42,
    });

    expect(
      beginNotificationFlowAttempt(
        '42',
        'material',
        'material',
        first.consumedKey,
      ).requestedFlowId,
    ).toBeUndefined();
  });

  it('清理 flowId 时保留来源和其他查询条件', () => {
    expect(
      withoutNotificationFlowId({
        flowId: '42',
        source: 'material',
        tab: 'pending',
      }),
    ).toEqual({ source: 'material', tab: 'pending' });
  });

  it('料件专用隐藏路由默认打开料件来源', () => {
    expect(notificationSource(undefined, '/material/approvals')).toBe(
      'material',
    );
  });

  it('我已处理列表不发送 flowId=0 造成空结果', () => {
    expect(approvalListRequestFlowId('handled', undefined)).toBeUndefined();
    expect(approvalListRequestFlowId('handled', 42)).toBeUndefined();
    expect(approvalListRequestFlowId('pending', 42)).toBe(42);
  });
});
