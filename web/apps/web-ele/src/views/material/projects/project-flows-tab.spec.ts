import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { cwd } from 'node:process';

import { describe, expect, it } from 'vitest';

describe('项目流转审批记录', () => {
  it('同时提供待审批、待接收、已处理和我的发起四类记录', () => {
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
    const pendingPane = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectPendingFlowsPane.vue',
      ),
      'utf8',
    );
    const handledPane = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectHandledFlowsPane.vue',
      ),
      'utf8',
    );
    const myPane = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectMyFlowsPane.vue',
      ),
      'utf8',
    );
    const receivingPane = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectReceivingFlowsPane.vue',
      ),
      'utf8',
    );
    const pageLogic = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/project-flow-actions.ts',
      ),
      'utf8',
    );
    const css = readFileSync(
      join(
        cwd(),
        'apps/web-ele/src/views/material/projects/ProjectFlowsTab.css',
      ),
      'utf8',
    );

    expect(component).toContain('流转审批');
    expect(component).toContain('待我审批');
    expect(component).toContain('{{ pendingCount }}');
    expect(component).toContain('待我接收');
    expect(component).toContain('{{ receivingCount }}');
    expect(component).toContain("'is-warning': receivingCount > 0");
    expect(component).toContain('我已处理');
    expect(component).toContain('{{ handledCount }}');
    expect(component).toContain('我的发起');
    expect(component).toContain('{{ myCount }}');
    expect(component).toContain("canViewApprovals ? 'pending' : 'receiving'");
    const template = component.slice(component.indexOf('<template>'));
    expect(template.indexOf('待我审批')).toBeGreaterThanOrEqual(0);
    expect(template.indexOf('待我审批')).toBeLessThan(
      template.indexOf('待我接收'),
    );
    expect(template.indexOf('待我接收')).toBeLessThan(
      template.indexOf('我已处理'),
    );
    expect(template.indexOf('我已处理')).toBeLessThan(
      template.indexOf('我的发起'),
    );
    expect(component).toContain('name="mine"');
    expect(receivingPane).toContain('确认接收');
    expect(receivingPane).toContain('申请人');
    expect(css).toContain('min-width: 0');
    expect(pendingPane).toContain(':data="pendingFlows"');
    expect(handledPane).toContain(':data="handledFlows"');
    expect(myPane).toContain(':data="myFlows"');
    expect(pageLogic).toContain('listHandledFlowsPageApi');
    expect(page).toContain(':handled-flows="handledFlows"');
  });
});
