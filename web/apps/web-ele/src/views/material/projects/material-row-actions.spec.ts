import type { MaterialItem } from '#/api/material';

import { describe, expect, it } from 'vitest';

import { canTransferMaterial } from './material-row-actions';

const material = {
  custodianId: 2,
  hasPendingFlow: false,
  isDeleted: false,
  status: 0,
} as MaterialItem;

describe('料件转移可达性', () => {
  it.each([
    [
      '当前保管人',
      { currentUserId: 2, isSupervisor: false, projectOwnerId: 3 },
    ],
    [
      '项目负责人',
      { currentUserId: 3, isSupervisor: false, projectOwnerId: 3 },
    ],
    ['主管', { currentUserId: 4, isSupervisor: true, projectOwnerId: 3 }],
  ])('%s 可以发起转移', (_, context) => {
    expect(canTransferMaterial(material, context)).toBe(true);
  });

  it('无关员工不能发起转移', () => {
    expect(
      canTransferMaterial(material, {
        currentUserId: 4,
        isSupervisor: false,
        projectOwnerId: 3,
      }),
    ).toBe(false);
  });
});
