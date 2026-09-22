const reasonRequiredTypes = new Set(['borrow', 'transfer']);

export function isApprovalReasonRequired(type: string) {
  return reasonRequiredTypes.has(type);
}

export function validateApprovalReason(type: string, reason: string) {
  if (isApprovalReasonRequired(type) && !reason.trim()) {
    return '请填写申请事由';
  }
  return null;
}
