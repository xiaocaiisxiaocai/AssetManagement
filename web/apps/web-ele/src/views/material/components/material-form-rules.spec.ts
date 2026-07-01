import { describe, expect, it } from 'vitest';

import { getDefaultCustodianId, validateMaterialForm } from './material-form-rules';

const baseForm = {
  brand: 'SAA',
  custodianId: 1,
  departmentId: 2,
  locationId: 3,
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
  it('除料件照片和备注外均为必填', () => {
    expect(validateMaterialForm({ ...baseForm, name: '' })).toBe('请填写料件名称');
    expect(validateMaterialForm({ ...baseForm, projectId: undefined })).toBe('请选择所属项目');
    expect(validateMaterialForm({ ...baseForm, vendorName: '' })).toBe('请填写厂商/来源');
    expect(validateMaterialForm({ ...baseForm, model: '' })).toBe('请填写型号');
    expect(validateMaterialForm({ ...baseForm, brand: '' })).toBe('请填写品牌');
    expect(validateMaterialForm({ ...baseForm, quantity: 0 })).toBe('请填写数量');
    expect(validateMaterialForm({ ...baseForm, departmentId: undefined })).toBe('请选择归属部门');
    expect(validateMaterialForm({ ...baseForm, locationId: undefined })).toBe('请选择存放位置');
    expect(validateMaterialForm({ ...baseForm, custodianId: undefined })).toBe('请选择保管人');
    expect(validateMaterialForm({ ...baseForm, receivedDate: undefined })).toBe('请选择接收日期');
  });

  it('料件照片和备注可以为空', () => {
    expect(validateMaterialForm({ ...baseForm, remark: '' })).toBeNull();
  });

  it('新增时默认保管人为项目负责人', () => {
    expect(getDefaultCustodianId(projects, 10)).toBe(8);
    expect(getDefaultCustodianId(projects, 11)).toBeUndefined();
  });
});
