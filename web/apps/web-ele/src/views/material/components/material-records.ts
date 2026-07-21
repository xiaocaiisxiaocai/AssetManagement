const materialRecordActionLabels: Record<string, string> = {
  approve: '审批通过',
  direct_transfer: '直接转移',
  reject: '驳回',
  return_to_vendor: '退回厂商',
  start: '发起流转',
  withdraw: '撤回申请',
};

export function materialRecordActionText(action: string) {
  return materialRecordActionLabels[action] ?? action;
}
