import type { MaterialFlowItem } from '#/api/material';

import { describe, expect, it, vi } from 'vitest';

import {
  allowedProgressCodes,
  canUpdateProjectProgress,
  canReleaseMaterialFlow,
  canWithdrawMaterialFlow,
  isSupportedProjectProgress,
  projectFollowUpStatusMeta,
  refreshProjectAfterMaterialChange,
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
        { closedDate: '2026-07-22', isDeleted: false, ownerId: 25 },
        26,
        true,
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

  it('进度选项只允许沿当前阶段向前', () => {
    expect(allowedProgressCodes('')).toEqual(['planning', 'testing']);
    expect(allowedProgressCodes('planning')).toEqual(['planning', 'testing']);
    expect(allowedProgressCodes('testing')).toEqual([
      'testing',
      'landing',
      'closed',
    ]);
    expect(allowedProgressCodes('testing', true)).toEqual(['testing']);
    expect(allowedProgressCodes('landing')).toEqual(['landing', 'closed']);
    expect(allowedProgressCodes('landing', true)).toEqual(['testing']);
    expect(allowedProgressCodes('closed')).toEqual(['closed']);
    expect(allowedProgressCodes('legacy')).toEqual(['planning', 'testing']);
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
    expect(
      canWithdrawMaterialFlow({
        ...flow,
        canReleaseStuck: true,
        canWithdraw: true,
      }),
    ).toBe(false);
    expect(canReleaseMaterialFlow(flow)).toBe(false);
    expect(
      canReleaseMaterialFlow({
        ...flow,
        canReleaseStuck: true,
      }),
    ).toBe(true);
    expect(
      canReleaseMaterialFlow({
        ...flow,
        canReleaseStuck: true,
        status: 'withdrawn',
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

  it('料件变更后同时刷新抽屉数据和项目列表计数', async () => {
    const loadMaterials = vi.fn(async () => {});
    const loadFlows = vi.fn(async () => {});
    const loadProjects = vi.fn(async () => {});

    await refreshProjectAfterMaterialChange(
      loadMaterials,
      loadFlows,
      loadProjects,
    );

    expect(loadMaterials).toHaveBeenCalledOnce();
    expect(loadFlows).toHaveBeenCalledOnce();
    expect(loadProjects).toHaveBeenCalledOnce();
  });

  it('项目生命周期只接受四个系统进度', () => {
    expect(['planning', 'testing', 'landing', 'closed'].every(isSupportedProjectProgress)).toBe(true);
    expect(isSupportedProjectProgress('reviewing')).toBe(false);
    expect(isSupportedProjectProgress('')).toBe(false);
  });
});
