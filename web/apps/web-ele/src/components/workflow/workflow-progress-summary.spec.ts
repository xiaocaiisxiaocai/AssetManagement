import { describe, expect, it } from 'vitest';

import { getTerminalProgress } from './workflow-progress-summary';

describe('workflow progress summary', () => {
  it.each([
    ['approved', '审批已完成', 'success'],
    ['rejected', '审批已驳回', 'danger'],
    ['withdrawn', '申请已撤回', 'info'],
  ])(
    'maps terminal status %s without pending wording',
    (status, text, tone) => {
      expect(getTerminalProgress(status)).toEqual({ text, tone });
    },
  );

  it('keeps pending flow in the live progress branch', () => {
    expect(getTerminalProgress('pending')).toBeUndefined();
  });
});
