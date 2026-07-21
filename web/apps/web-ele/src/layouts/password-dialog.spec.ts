import { describe, expect, it, vi } from 'vitest';

import {
  closePasswordDialogUnlessSubmitting,
  PASSWORD_DIALOG_WIDTH,
} from './password-dialog';

describe('修改密码弹窗状态', () => {
  it('提交期间拒绝关闭，空闲时允许关闭', () => {
    const done = vi.fn();

    expect(closePasswordDialogUnlessSubmitting(true, done)).toBe(false);
    expect(done).not.toHaveBeenCalled();
    expect(closePasswordDialogUnlessSubmitting(false, done)).toBe(true);
    expect(done).toHaveBeenCalledOnce();
  });

  it('弹窗宽度始终保留窄屏边距', () => {
    expect(PASSWORD_DIALOG_WIDTH).toBe('min(500px, calc(100vw - 24px))');
  });
});
