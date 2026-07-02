import { describe, expect, it } from 'vitest';

import {
  buildCategoryActionAccess,
  buildLocationActionAccess,
  buildMaterialActionAccess,
  buildProjectActionAccess,
  buildReportActionAccess,
  buildWorkflowActionAccess,
} from './action-access';

function has(codes: string[]) {
  const permissions = new Set(codes);
  return (required: string[]) => required.some((code) => permissions.has(code));
}

describe('页面操作权限映射', () => {
  it('资产分类使用 category 权限码而不是 asset 权限码', () => {
    const access = buildCategoryActionAccess(has(['category:delete', 'asset:purge']));

    expect(access.canDelete).toBe(true);
    expect(access.canPurge).toBe(false);
  });

  it('测试项目彻底删除使用 project:purge', () => {
    const access = buildProjectActionAccess(has(['project:purge', 'material:purge']));

    expect(access.canPurge).toBe(true);
  });

  it('料件退回和流转使用各自接口权限码', () => {
    const access = buildMaterialActionAccess(has(['material:return', 'material-flow:transfer']));

    expect(access.canReturn).toBe(true);
    expect(access.canTransfer).toBe(true);
    expect(access.canEdit).toBe(false);
  });

  it('位置、工作流、报表操作使用对应模块权限码', () => {
    const location = buildLocationActionAccess(has(['location:create']));
    const workflow = buildWorkflowActionAccess(has(['workflow:design', 'workflow:delete']));
    const report = buildReportActionAccess(has(['report:remind']));

    expect(location.canCreate).toBe(true);
    expect(location.canEdit).toBe(false);
    expect(workflow.canDesign).toBe(true);
    expect(workflow.canDelete).toBe(true);
    expect(report.canRemind).toBe(true);
    expect(report.canExport).toBe(false);
  });
});
