export type MaterialFormLike = {
  brand: string;
  custodianId?: number;
  departmentId?: number;
  locationName: string;
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

export type MaterialCustodianLike = {
  departmentId?: null | number;
  departmentName?: null | string;
  id: number;
};

export type MaterialDepartmentLike = {
  id: number;
  label: string;
};

export type CustodianDepartmentResolution =
  | { departmentId?: number; resolved: true }
  | { resolved: false };

export function validateMaterialForm(form: MaterialFormLike) {
  if (!form.name.trim()) return '请填写料件名称';
  if (form.name.trim().length > 100) return '料件名称不能超过 100 个字符';
  if (form.vendorName.trim().length > 100)
    return '供应商不能超过 100 个字符';
  if (form.model.trim().length > 100) return '型号不能超过 100 个字符';
  if (form.brand.trim().length > 100) return '品牌不能超过 100 个字符';
  if ((form.remark ?? '').trim().length > 500)
    return '备注不能超过 500 个字符';
  if (!form.projectId) return '请选择所属项目';
  if (!form.quantity || form.quantity < 1) return '请填写数量';
  if (!Number.isInteger(form.quantity) || form.quantity > 999999) {
    return '数量须为 1-999999 的整数';
  }
  if (form.locationName.trim().length > 100)
    return '存放位置不能超过 100 个字符';
  return null;
}

export function getDefaultCustodianId(
  projects: ProjectOwnerLike[],
  projectId?: number,
) {
  return projects.find((item) => item.id === projectId)?.ownerId ?? undefined;
}

export function resolveCustodianDepartment(
  users: MaterialCustodianLike[],
  departments: MaterialDepartmentLike[],
  custodianId?: number,
): CustodianDepartmentResolution {
  if (!custodianId) return { departmentId: undefined, resolved: true };
  const custodian = users.find((item) => item.id === custodianId);
  if (!custodian) return { resolved: false };
  if (custodian.departmentId !== undefined) {
    return {
      departmentId: custodian.departmentId ?? undefined,
      resolved: true,
    };
  }
  const departmentName = custodian.departmentName?.trim();
  if (!departmentName) return { departmentId: undefined, resolved: true };
  const department = departments.find(
    (item) => item.label.trim() === departmentName,
  );
  return department
    ? { departmentId: department.id, resolved: true }
    : { resolved: false };
}

export function validateCustodianDepartment(
  form: Pick<MaterialFormLike, 'custodianId' | 'departmentId'>,
  users: MaterialCustodianLike[],
  departments: MaterialDepartmentLike[],
  isEdit = false,
) {
  if (isEdit) return null;
  if (!form.custodianId) return null;
  const resolution = resolveCustodianDepartment(
    users,
    departments,
    form.custodianId,
  );
  if (!resolution.resolved) return '保管人归属部门尚未加载，请稍后重试';
  if (resolution.departmentId !== form.departmentId) {
    return '保管人与归属部门不一致';
  }
  return null;
}

export function nextCustodianDepartmentId(
  currentDepartmentId: number | undefined,
  custodianId: number | undefined,
  resolution: CustodianDepartmentResolution,
) {
  if (!custodianId || !resolution.resolved) return currentDepartmentId;
  return resolution.departmentId;
}
