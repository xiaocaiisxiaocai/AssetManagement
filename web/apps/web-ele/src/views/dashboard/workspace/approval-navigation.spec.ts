import { describe, expect, it } from 'vitest';

import { myApplicationPath, pendingApprovalPath } from './approval-navigation';

describe('首页审批导航', () => {
  it('材料专属用户进入材料项目页', () => {
    const access = {
      canHandleApprovals: false,
      canHandleMaterialFlows: true,
      canViewApprovals: false,
      canViewMaterialFlows: true,
    };

    expect(pendingApprovalPath(access)).toBe(
      '/material/approvals?source=material',
    );
    expect(myApplicationPath(access)).toBe(
      '/material/applications?source=material',
    );
  });

  it('固定资产审批权限优先进入固定资产审批页', () => {
    const access = {
      canHandleApprovals: true,
      canHandleMaterialFlows: true,
      canViewApprovals: true,
      canViewMaterialFlows: true,
    };

    expect(pendingApprovalPath(access)).toBe('/approval/pending');
    expect(myApplicationPath(access)).toBe('/approval/mine');
  });
});
