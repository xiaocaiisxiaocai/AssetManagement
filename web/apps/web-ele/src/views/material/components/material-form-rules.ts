export type MaterialFormLike = {
  brand: string;
  custodianId?: number;
  departmentId?: number;
  locationId?: number;
  model: string;
  name: string;
  projectId?: number;
  quantity: number;
  receivedDate?: string;
  remark?: string;
  vendorName: string;
};

export type ProjectOwnerLike = {
  id: number;
  ownerId?: null | number;
};

export function validateMaterialForm(form: MaterialFormLike) {
  if (!form.name.trim()) return '请填写料件名称';
  if (!form.projectId) return '请选择所属项目';
  if (!form.vendorName.trim()) return '请填写厂商/来源';
  if (!form.model.trim()) return '请填写型号';
  if (!form.brand.trim()) return '请填写品牌';
  if (!form.quantity || form.quantity < 1) return '请填写数量';
  if (!form.departmentId) return '请选择归属部门';
  if (!form.locationId) return '请选择存放位置';
  if (!form.custodianId) return '请选择保管人';
  if (!form.receivedDate) return '请选择接收日期';
  return null;
}

export function getDefaultCustodianId(
  projects: ProjectOwnerLike[],
  projectId?: number,
) {
  return projects.find((item) => item.id === projectId)?.ownerId ?? undefined;
}
