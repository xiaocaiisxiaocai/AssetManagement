import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { cwd } from 'node:process';

import { describe, expect, it } from 'vitest';

import {
  getDefaultCustodianId,
  nextCustodianDepartmentId,
  resolveCustodianDepartment,
  validateCustodianDepartment,
  validateMaterialForm,
} from './material-form-rules';

const baseForm = {
  brand: 'SAA',
  custodianId: 1,
  departmentId: 2,
  locationName: '二楼实验室 B-08',
  model: 'M-100',
  name: '测试料件',
  projectId: 10,
  quantity: 1,
  receivedDate: '2026-06-30',
  remark: '',
  vendorName: '供应商',
};

const projects = [
  { id: 10, ownerId: 8 },
  { id: 11, ownerId: null },
];

describe('测试料件表单规则', () => {
  it('仅名称、所属项目和数量为必填', () => {
    expect(validateMaterialForm({ ...baseForm, name: '' })).toBe(
      '请填写料件名称',
    );
    expect(validateMaterialForm({ ...baseForm, projectId: undefined })).toBe(
      '请选择所属项目',
    );
    expect(validateMaterialForm({ ...baseForm, quantity: 0 })).toBe(
      '请填写数量',
    );
  });

  it('可选的来源、型号品牌、部门位置、保管人和日期可以为空', () => {
    expect(
      validateMaterialForm({
        ...baseForm,
        brand: '',
        custodianId: undefined,
        departmentId: undefined,
        locationName: '',
        model: '',
        receivedDate: undefined,
        remark: '',
        vendorName: '',
      }),
    ).toBeNull();
  });

  it('手工填写的存放位置限制为 100 个字符', () => {
    expect(
      validateMaterialForm({ ...baseForm, locationName: 'A'.repeat(101) }),
    ).toBe('存放位置不能超过 100 个字符');
  });

  it('新增时默认保管人为项目负责人', () => {
    expect(getDefaultCustodianId(projects, 10)).toBe(8);
    expect(getDefaultCustodianId(projects, 11)).toBeUndefined();
  });

  it('按用户选项同步保管人的归属部门', () => {
    expect(
      resolveCustodianDepartment(
        [{ departmentName: '甲部门', id: 8 }],
        [{ id: 2, label: '　甲部门' }],
        8,
      ),
    ).toEqual({ departmentId: 2, resolved: true });
    expect(
      resolveCustodianDepartment(
        [{ departmentId: 3, departmentName: '旧名称', id: 9 }],
        [{ id: 2, label: '甲部门' }],
        9,
      ),
    ).toEqual({ departmentId: 3, resolved: true });
  });

  it('保存前拒绝保管人与归属部门不一致或选项尚未加载', () => {
    const users = [{ departmentName: '甲部门', id: 8 }];
    const departments = [{ id: 2, label: '甲部门' }];

    expect(
      validateCustodianDepartment(
        { custodianId: 8, departmentId: 3 },
        users,
        departments,
      ),
    ).toBe('保管人与归属部门不一致');
    expect(
      validateCustodianDepartment(
        { custodianId: 8, departmentId: 2 },
        users,
        departments,
      ),
    ).toBeNull();
    expect(
      validateCustodianDepartment(
        { custodianId: 8, departmentId: undefined },
        [],
        departments,
      ),
    ).toBe('保管人归属部门尚未加载，请稍后重试');
  });

  it('编辑时允许占位保管人沿用原部门', () => {
    expect(
      validateCustodianDepartment(
        { custodianId: 8, departmentId: 2 },
        [{ id: 8 }],
        [{ id: 2, label: '甲部门' }],
        true,
      ),
    ).toBeNull();
  });

  it('未选择保管人或选项尚未解析时保留手工部门', () => {
    expect(
      nextCustodianDepartmentId(2, undefined, {
        departmentId: undefined,
        resolved: true,
      }),
    ).toBe(2);
    expect(nextCustodianDepartmentId(2, 8, { resolved: false })).toBe(2);
    expect(
      nextCustodianDepartmentId(2, 8, {
        departmentId: 3,
        resolved: true,
      }),
    ).toBe(3);
  });

  it('桌面端使用紧凑双列布局避免弹窗超出视口', () => {
    const componentPath = join(
      cwd(),
      'apps/web-ele/src/views/material/components/MaterialFormDialog.vue',
    );
    const source = readFileSync(componentPath, 'utf8');

    expect(source).toContain('class="material-form-dialog"');
    expect(source).toContain('width="840px"');
    expect(source).toContain('<div class="material-form-grid">');
    expect(source).toContain(
      'grid-template-columns: repeat(2, minmax(0, 1fr))',
    );
    expect(source).toContain(
      'class="material-form-field--wide material-form-photo"',
    );
    expect(source).toContain('width: 104px');
    expect(source).toContain(':global(.material-form-dialog .el-dialog__body)');
  });
});
