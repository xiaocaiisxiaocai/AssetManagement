import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { validateAssetForm } from './asset-form-rules';

const baseForm = {
  categoryId: 1,
  custodianId: 4,
  departmentId: 2,
  locationId: 3,
  name: '测试资产',
  quantity: 1,
  status: 0,
};

describe('固定资产表单规则', () => {
  it('新增资产仅校验业务必填项', () => {
    expect(validateAssetForm({ ...baseForm, name: '' })).toBe('请填写资产名称');
    expect(validateAssetForm({ ...baseForm, categoryId: 0 })).toBe('请选择资产分类');
    expect(validateAssetForm({ ...baseForm, departmentId: undefined })).toBe('请选择归属部门');
    expect(validateAssetForm({ ...baseForm, locationId: undefined })).toBe('请选择存放位置');
    expect(validateAssetForm({ ...baseForm, custodianId: undefined })).toBe('请选择保管人');
    expect(validateAssetForm({ ...baseForm, quantity: 0 })).toBe('请填写数量');
  });

  it('状态和资产图片不是必填校验项', () => {
    expect(validateAssetForm({ ...baseForm, status: undefined })).toBeNull();
  });

  it('必填项在界面上显示星号', () => {
    const componentPath = join(
      process.cwd(),
      'apps/web-ele/src/views/asset/list/components/AssetFormDialog.vue',
    );
    const source = readFileSync(componentPath, 'utf8');
    const requiredLabels = [
      '资产名称',
      '资产分类',
      '编号预览',
      '归属部门',
      '存放位置',
      '保管人',
      '数量',
    ];

    for (const label of requiredLabels) {
      expect(source).toContain(`<ElFormItem label="${label}" required>`);
    }
    expect(source).not.toContain('<ElFormItem v-if="isEdit" label="状态" required>');
    expect(source).toContain('<ElFormItem label="资产照片">');
    expect(source).not.toContain('label="型号品牌"');
    expect(source).not.toContain('label="首次登记"');
  });

  it('资产登记字段仅选择日期', () => {
    const componentPath = join(
      process.cwd(),
      'apps/web-ele/src/views/asset/list/components/AssetFormDialog.vue',
    );
    const source = readFileSync(componentPath, 'utf8');

    expect(source).toContain('<ElFormItem label="登记日期">');
    expect(source).toContain('placeholder="选择资产登记日期"');
    expect(source).toContain('value-format="YYYY-MM-DD"');
    expect(source).not.toContain('type="datetime"');
  });

  it('目前状况使用数据字典下拉选择', () => {
    const componentPath = join(
      process.cwd(),
      'apps/web-ele/src/views/asset/list/components/AssetFormDialog.vue',
    );
    const source = readFileSync(componentPath, 'utf8');

    expect(source).toContain('placeholder="请选择资产目前状况"');
    expect(source).toContain('v-for="option in selectableConditionOptions"');
    expect(source).not.toContain('placeholder="请输入资产目前状况"');
  });
});
