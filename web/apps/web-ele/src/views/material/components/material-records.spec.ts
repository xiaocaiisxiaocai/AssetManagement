import { describe, expect, it } from 'vitest';

import { materialRecordActionText } from './material-records';

describe('测试料件操作记录', () => {
  it('退回厂商记录显示明确的中文动作', () => {
    expect(materialRecordActionText('return_to_vendor')).toBe('退回厂商');
  });
});
