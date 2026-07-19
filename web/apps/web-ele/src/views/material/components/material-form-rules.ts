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
  if (!form.quantity || form.quantity < 1) return '请填写数量';
  return null;
}

export function getDefaultCustodianId(
  projects: ProjectOwnerLike[],
  projectId?: number,
) {
  return projects.find((item) => item.id === projectId)?.ownerId ?? undefined;
}
