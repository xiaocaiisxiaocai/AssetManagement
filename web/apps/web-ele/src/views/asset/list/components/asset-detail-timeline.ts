import type { AssetFlow } from '#/api/asset';

export function flowParticipantText(flow: AssetFlow) {
  switch (flow.bizType) {
    case 'borrow': {
      return `借用人：${flow.applicant}`;
    }
    case 'extension': {
      return `当前保管人：${flow.applicant}`;
    }
    case 'return': {
      return `归还人：${flow.applicant}`;
    }
    case 'transfer': {
      return flow.transferee
        ? `原保管人：${flow.applicant} → 新保管人：${flow.transferee}`
        : `原保管人：${flow.applicant}`;
    }
    default: {
      return `申请人：${flow.applicant}`;
    }
  }
}

export function custodyTimelineCount(flowCount: number) {
  return flowCount + 1;
}
