const materialRecordActionLabels: Record<string, string> = {
  approve: '审批通过',
  confirm_receipt: '确认接收',
  direct_transfer: '发起转移',
  reject: '驳回',
  release: '终止并重新发起',
  return_to_vendor: '退回厂商',
  start: '发起流转',
  withdraw: '撤回申请',
};

export function materialRecordActionText(action: string) {
  return materialRecordActionLabels[action] ?? action;
}
