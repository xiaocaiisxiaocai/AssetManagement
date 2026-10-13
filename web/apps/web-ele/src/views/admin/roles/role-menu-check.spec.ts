import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { cwd } from 'node:process';

import { describe, expect, it } from 'vitest';

const rolesDir = join(cwd(), 'apps/web-ele/src/views/admin/roles');

describe('角色菜单树勾选同步', () => {
  it('勾选菜单时立刻把选中结果交给授权对话框', () => {
    const menuPanel = readFileSync(join(rolesDir, 'RoleMenuPanel.vue'), 'utf8');
    const dialog = readFileSync(join(rolesDir, 'RoleAccessDialog.vue'), 'utf8');

    expect(menuPanel).toMatch(/<ElTree[\s\S]*@check="onCheck"/);
    expect(menuPanel).toContain("emit('check')");
    expect(dialog).toMatch(/<RoleMenuPanel[\s\S]*@check="syncTreeSelection"/);
    expect(dialog).toMatch(/function onSave\(\) \{\s*syncTreeSelection\(\);/);
  });
});
