export type AssetFormLike = {
  categoryId: number;
  custodianId?: number;
  departmentId?: number;
  locationId?: number;
  name: string;
  quantity: number;
  status?: number;
};

export function validateAssetForm(form: AssetFormLike) {
  if (!form.name.trim()) return '请填写资产名称';
  if (!form.categoryId) return '请选择资产分类';
  if (!form.departmentId) return '请选择归属部门';
  if (!form.locationId) return '请选择存放位置';
  if (!form.custodianId) return '请选择保管人';
  if (!form.quantity || form.quantity < 1) return '请填写数量';
  return null;
}
