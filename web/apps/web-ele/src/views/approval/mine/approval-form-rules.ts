const reasonRequiredTypes = new Set(['borrow', 'transfer']);

export function isApprovalReasonRequired(type: string) {
  return reasonRequiredTypes.has(type);
}

export function validateApprovalReason(type: string, reason: string) {
  const trimmed = reason.trim();
  if (isApprovalReasonRequired(type) && !trimmed) {
    return '请填写申请事由';
  }
  if (trimmed.length > 500) {
    return '申请事由不能超过 500 个字符';
  }
  return null;
}
