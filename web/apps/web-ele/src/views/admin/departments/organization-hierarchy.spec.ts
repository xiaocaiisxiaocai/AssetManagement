import { describe, expect, it } from 'vitest';

import {
  getAllowedOrganizationLevelCodes,
  getDefaultOrganizationLevelCode,
} from './organization-hierarchy';

describe('组织层级选择规则', () => {
  it('顶层默认公司，事业部可直接新增课别', () => {
    expect(getDefaultOrganizationLevelCode()).toBe('company');
    expect(getAllowedOrganizationLevelCodes('division')).toEqual([
      'department',
      'section',
    ]);
  });

  it('部门下只能新增课别，课别没有下级', () => {
    expect(getAllowedOrganizationLevelCodes('department')).toEqual([
      'section',
    ]);
    expect(getAllowedOrganizationLevelCodes('section')).toEqual([]);
  });
});
