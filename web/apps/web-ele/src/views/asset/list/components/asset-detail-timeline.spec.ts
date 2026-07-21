import { describe, expect, it } from 'vitest';

import type { AssetFlow } from '#/api/asset';

import {
  custodyTimelineCount,
  flowParticipantText,
} from './asset-detail-timeline';

const baseFlow: AssetFlow = {
  applicant: '张三',
  applyTime: '2026-07-20T10:00:00Z',
  bizType: 'borrow',
  flowNo: 'AF-001',
  id: 1,
  status: 'approved',
};

describe('资产详情保管轨迹', () => {
  it('流转数量包含资产登记时的初始保管节点', () => {
    expect(custodyTimelineCount(0)).toBe(1);
    expect(custodyTimelineCount(3)).toBe(4);
  });

  it('明确标注不同流转动作中的人员角色', () => {
    expect(flowParticipantText(baseFlow)).toBe('借用人：张三');
    expect(
      flowParticipantText({
        ...baseFlow,
        bizType: 'transfer',
        transferee: '李四',
      }),
    ).toBe('原保管人：张三 → 新保管人：李四');
    expect(flowParticipantText({ ...baseFlow, bizType: 'return' })).toBe(
      '归还人：张三',
    );
  });
});
