import { describe, expect, it } from 'vitest';

import { isWorkflowDesignerBusy } from './designer-save-state';

describe('工作流设计器保存状态', () => {
  it.each([
    { exporting: true, saving: false },
    { exporting: false, saving: true },
    { exporting: true, saving: true },
  ])('xML 导出或 API 保存期间均保持忙碌', ({ exporting, saving }) => {
    expect(isWorkflowDesignerBusy(exporting, saving)).toBe(true);
  });

  it('导出与保存都完成后允许关闭', () => {
    expect(isWorkflowDesignerBusy(false, false)).toBe(false);
  });
});
