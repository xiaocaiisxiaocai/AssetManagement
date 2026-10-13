import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

const pendingDir = join(
  process.cwd(),
  'apps/web-ele/src/views/approval/pending',
);

function readPendingSource(...fileNames: string[]) {
  return fileNames
    .map((fileName) => readFileSync(join(pendingDir, fileName), 'utf8'))
    .join('\n');
}

describe('待审批弹框布局', () => {
  it('审批弹框居中加宽并保持详情标题单行', () => {
    const source = readPendingSource(
      'ApprovalDetailDialog.vue',
      'ApprovalDetailDialog.css',
      'pending-approval-dialog.css',
    );

    expect(source).toMatch(
      /<ElDialog[\s\S]*?v-model="visible"[\s\S]*?align-center[\s\S]*?class="pending-approval-dialog"[\s\S]*?width="800px"/,
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
    const source = readPendingSource(
      'HandledDetailDialog.vue',
      'pending-approval-dialog.css',
    );

    expect(source).toMatch(
      /<ElDialog[\s\S]*?v-model="visible"[\s\S]*?align-center[\s\S]*?class="pending-approval-dialog"[\s\S]*?title="审批记录"[\s\S]*?width="680px"/,
    );
  });
});
