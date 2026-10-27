import { describe, expect, it } from 'vitest';

import {
  workspacePrintTitle,
  workspacePrintZoom,
} from './project-workspace-print';

describe('项目工作台打印', () => {
  const date = new Date(2026, 9, 26);

  it('替换文件名非法字符', () => {
    expect(workspacePrintTitle('光电/模块', 'TM:01', date)).toBe(
      '光电_模块-TM_01-20261026',
    );
    expect(workspacePrintTitle(String.raw`a\b*c?d"e<f>g|h`, 'x', date)).toBe(
      'a_b_c_d_e_f_g_h-x-20261026',
    );
  });

  it('编号为空时省略该段', () => {
    expect(workspacePrintTitle('项目', null, date)).toBe('项目-20261026');
    expect(workspacePrintTitle('项目', undefined, date)).toBe('项目-20261026');
    expect(workspacePrintTitle('项目', '', date)).toBe('项目-20261026');
    expect(workspacePrintTitle('项目', '   ', date)).toBe('项目-20261026');
  });

  it('宽度不超过纸宽时不缩小', () => {
    expect(workspacePrintZoom(800)).toBe(1);
    expect(workspacePrintZoom(1047)).toBe(1);
    expect(workspacePrintZoom(0)).toBe(1);
    expect(workspacePrintZoom(2094)).toBe(0.5);
  });
});
