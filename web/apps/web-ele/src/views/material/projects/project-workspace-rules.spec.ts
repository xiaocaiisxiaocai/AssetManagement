import type { MaterialFlowItem } from '#/api/material';

import { describe, expect, it } from 'vitest';

import {
  canUpdateProjectProgress,
  canWithdrawMaterialFlow,
  projectFollowUpStatusMeta,
} from './project-workspace-rules';

describe('测试项目工作台规则', () => {
  it('只有未删除且未结案项目的负责人可以更新项目进展', () => {
    expect(
      canUpdateProjectProgress(
        { closedDate: null, isDeleted: false, ownerId: 25 },
        25,
      ),
    ).toBe(true);
    expect(
      canUpdateProjectProgress(
        { closedDate: null, isDeleted: false, ownerId: 25 },
        26,
      ),
    ).toBe(false);
    expect(
      canUpdateProjectProgress(
        { closedDate: null, isDeleted: true, ownerId: 25 },
        25,
      ),
    ).toBe(false);
    expect(
      canUpdateProjectProgress(
        { closedDate: '2026-07-22', isDeleted: false, ownerId: 25 },
        25,
      ),
    ).toBe(false);
    expect(
      canUpdateProjectProgress(
        { closedDate: null, isDeleted: false, ownerId: null },
        25,
      ),
    ).toBe(false);
  });

  it('只有后端明确授权的待审批料件流程才显示撤回', () => {
    const flow = { status: 'pending' } as MaterialFlowItem;
    expect(canWithdrawMaterialFlow(flow)).toBe(false);
    expect(canWithdrawMaterialFlow({ ...flow, canWithdraw: false })).toBe(
      false,
    );
    expect(canWithdrawMaterialFlow({ ...flow, canWithdraw: true })).toBe(true);
    expect(
      canWithdrawMaterialFlow({
        ...flow,
        canWithdraw: true,
        status: 'approved',
      }),
    ).toBe(false);
  });

  it('结案和删除项目不再显示为未到期', () => {
    expect(
      projectFollowUpStatusMeta({
        closedDate: '2026-07-19',
        followUpStatus: 'upcoming',
        isDeleted: false,
      }).label,
    ).toBe('已结案');
    expect(
      projectFollowUpStatusMeta({
        closedDate: null,
        followUpStatus: 'upcoming',
        isDeleted: true,
      }).label,
    ).toBe('已删除');
  });
});
