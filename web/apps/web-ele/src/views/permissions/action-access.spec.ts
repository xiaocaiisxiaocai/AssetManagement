import { describe, expect, it } from 'vitest';

import {
  buildApprovalActionAccess,
  buildCategoryActionAccess,
  buildFileActionAccess,
  buildLocationActionAccess,
  buildMaterialActionAccess,
  buildProjectActionAccess,
  buildReportActionAccess,
  buildUserActionAccess,
  buildWorkflowActionAccess,
} from './action-access';

function has(codes: string[]) {
  const permissions = new Set(codes);
  return (required: string[]) => required.some((code) => permissions.has(code));
}

describe('页面操作权限映射', () => {
  it('我的申请发起操作使用 approval:create', () => {
    expect(buildApprovalActionAccess(has(['approval:create'])).canCreate).toBe(
      true,
    );
    expect(buildApprovalActionAccess(has(['approval:view'])).canCreate).toBe(
      false,
    );
  });

  it('图片上传同时需要上传和查看权限', () => {
    expect(
      buildFileActionAccess(has(['file:upload'])).canUploadAndPreview,
    ).toBe(false);
    expect(
      buildFileActionAccess(has(['file:upload', 'file:view']))
        .canUploadAndPreview,
    ).toBe(true);
  });

  it('用户导入要求创建与分配角色，交互式新增还需查看角色', () => {
    expect(buildUserActionAccess(has(['user:create'])).canCreate).toBe(false);
    expect(
      buildUserActionAccess(has(['user:create', 'user:assign-role'])).canCreate,
    ).toBe(false);
    expect(
      buildUserActionAccess(has(['user:create', 'user:assign-role'])).canImport,
    ).toBe(true);
    expect(
      buildUserActionAccess(
        has(['user:create', 'user:assign-role', 'role:view']),
      ).canCreate,
    ).toBe(true);
  });

  it('资产分类使用 category 权限码而不是 asset 权限码', () => {
    const access = buildCategoryActionAccess(
      has(['category:delete', 'asset:purge']),
    );

    expect(access.canDelete).toBe(true);
    expect(access.canPurge).toBe(false);
  });

  it('测试项目彻底删除使用 project:purge', () => {
    const access = buildProjectActionAccess(
      has(['project:purge', 'material:purge']),
    );

    expect(access.canPurge).toBe(true);
  });

  it('测试项目导出使用 project:export', () => {
    const access = buildProjectActionAccess(has(['project:export']));

    expect(access.canExport).toBe(true);
    expect(access.canEdit).toBe(false);
  });

  it('料件退回和流转使用各自接口权限码', () => {
    const access = buildMaterialActionAccess(
      has(['material:return', 'material-flow:transfer']),
    );

    expect(access.canReturn).toBe(true);
    expect(access.canTransfer).toBe(true);
    expect(access.canEdit).toBe(false);
  });

  it('位置、工作流、报表操作使用对应模块权限码', () => {
    const location = buildLocationActionAccess(has(['location:create']));
    const workflow = buildWorkflowActionAccess(
      has(['workflow:design', 'workflow:delete']),
    );
    const report = buildReportActionAccess(has(['report:remind']));

    expect(location.canCreate).toBe(true);
    expect(location.canEdit).toBe(false);
    expect(workflow.canDesign).toBe(true);
    expect(workflow.canDelete).toBe(true);
    expect(report.canRemind).toBe(true);
    expect(report.canExport).toBe(false);
  });

  it('工作流编辑权限不能冒充设计权限', () => {
    const workflow = buildWorkflowActionAccess(has(['workflow:edit']));

    expect(workflow.canEdit).toBe(true);
    expect(workflow.canDesign).toBe(false);
  });
});
