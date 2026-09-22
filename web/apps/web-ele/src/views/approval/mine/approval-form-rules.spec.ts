import { describe, expect, it } from 'vitest';

import {
  isApprovalReasonRequired,
  validateApprovalReason,
} from './approval-form-rules';

describe('资产审批申请表单规则', () => {
  it.each(['borrow', 'transfer'])('%s 必须填写申请事由', (type) => {
    expect(isApprovalReasonRequired(type)).toBe(true);
    expect(validateApprovalReason(type, '   ')).toBe('请填写申请事由');
    expect(validateApprovalReason(type, '项目使用')).toBeNull();
  });

  it.each(['extension', 'return'])('%s 允许不填写申请事由', (type) => {
    expect(isApprovalReasonRequired(type)).toBe(false);
    expect(validateApprovalReason(type, '')).toBeNull();
  });
});
