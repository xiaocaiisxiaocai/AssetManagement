import { readFileSync } from 'node:fs';
import { cwd } from 'node:process';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

describe('项目流转审批记录', () => {
  it('同时提供待审批、已处理和我的发起三类记录', () => {
    const component = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectFlowsTab.vue',
      ),
      'utf8',
    );
    const page = readFileSync(
      join(cwd(), 'apps/web-ele/src/views/material/projects/index.vue'),
      'utf8',
    );

    expect(component).toContain('待我审批 {{ pendingCount }}');
    expect(component).toContain('我已处理 {{ handledCount }}');
    expect(component).toContain(':data="handledFlows"');
    expect(component).toContain('我的发起 {{ myCount }}');
    expect(page).toContain('listHandledFlowsPageApi');
    expect(page).toContain(':handled-flows="handledFlows"');
  });
});
