import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

describe('待审批弹框布局', () => {
  it('审批弹框居中加宽并保持详情标题单行', () => {
    const componentPath = join(
      process.cwd(),
      'apps/web-ele/src/views/approval/pending/index.vue',
    );
    const source = readFileSync(componentPath, 'utf8');

    expect(source).toMatch(
      /<ElDialog[\s\S]*?v-model="detailVisible"[\s\S]*?align-center[\s\S]*?class="pending-approval-dialog"[\s\S]*?width="800px"/,
    );
    expect(source).toMatch(
      /<ElDescriptions[\s\S]*?:column="2"[\s\S]*?label-width="112px"/,
    );
    expect(source).toContain(':global(.pending-approval-dialog)');
    expect(source).toContain(
      ':global(.pending-approval-dialog .el-dialog__body)',
    );
    expect(source).toContain(
      ':global(.pending-approval-dialog .el-descriptions__label)',
    );
  });

  it('已处理审批记录复用居中弹框布局', () => {
    const componentPath = join(
      process.cwd(),
      'apps/web-ele/src/views/approval/pending/index.vue',
    );
    const source = readFileSync(componentPath, 'utf8');

    expect(source).toMatch(
      /<ElDialog[\s\S]*?v-model="handledDetailVisible"[\s\S]*?align-center[\s\S]*?class="pending-approval-dialog"[\s\S]*?title="审批记录"[\s\S]*?width="680px"/,
    );
  });
});
