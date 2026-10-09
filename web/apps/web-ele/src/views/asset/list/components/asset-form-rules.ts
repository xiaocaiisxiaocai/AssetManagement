export type AssetFormLike = {
  categoryId: number;
  custodianId?: number;
  departmentId?: number;
  locationName: string;
  name: string;
  quantity: number;
  status?: number;
};

export type AssetCustodianLike = {
  departmentId?: null | number;
  id: number;
};

export function validateAssetForm(
  form: AssetFormLike,
  users?: AssetCustodianLike[],
  isEdit = false,
) {
  if (!form.name.trim()) return '请填写资产名称';
  if (form.name.trim().length > 100) return '资产名称不能超过 100 个字符';
  if (!form.categoryId) return '请选择资产分类';
  if (!form.departmentId) return '请选择归属部门';
  if (!form.locationName.trim()) return '请填写存放位置';
  if (form.locationName.trim().length > 100)
    return '存放位置不能超过 100 个字符';
  if (!form.custodianId) return '请选择保管人';
  if (!isEdit && users) {
    const custodian = users.find((item) => item.id === form.custodianId);
    if (!custodian || custodian.departmentId === undefined) {
      return '保管人归属部门尚未加载，请稍后重试';
    }
    if (!custodian.departmentId) return '保管人必须属于有效部门';
    if (custodian.departmentId !== form.departmentId) {
      return '保管人与归属部门不一致';
    }
  }
  if (!form.quantity || form.quantity < 1) return '请填写数量';
  if (!Number.isInteger(form.quantity) || form.quantity > 999999) {
    return '数量须为 1-999999 的整数';
  }
  return null;
}
