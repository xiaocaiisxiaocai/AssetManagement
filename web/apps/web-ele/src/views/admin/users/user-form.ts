import type { UserDto, UserPayload } from '#/api/user';

export interface UserFormState {
  departmentId?: number;
  email: string;
  employeeNo: string;
  name: string;
  phone: string;
  roleId?: number;
  roleName: string;
  supervisorId?: number;
}

export interface DepartmentSupervisorSource {
  id: number;
  managerId?: number;
  managerName?: string;
}

export interface RoleCodeSource {
  code: string;
  id: number;
  isActive: boolean;
}

export function resolveDefaultEmployeeRoleId(roles: RoleCodeSource[]) {
  return roles.find((role) => role.code === 'employee' && role.isActive)?.id;
}

export function resolveDepartmentSupervisor(
  departments: DepartmentSupervisorSource[],
  departmentId?: number,
  editingUserId?: null | number,
) {
  const department = departments.find((item) => item.id === departmentId);
  if (!department?.managerId || department.managerId === editingUserId) {
    return {
      supervisorId: undefined,
      supervisorName: undefined,
    };
  }

  return {
    supervisorId: department.managerId,
    supervisorName: department.managerName,
  };
}

export function userToForm(row: UserDto): UserFormState {
  return {
    departmentId: row.departmentId ?? undefined,
    email: row.email ?? '',
    employeeNo: row.employeeNo,
    name: row.name,
    phone: row.phone ?? '',
    roleId: row.roleIds?.[0],
    roleName: row.roleNames?.[0] ?? '',
    supervisorId: row.supervisorId ?? undefined,
  };
}

export function buildUserPayload(form: UserFormState): UserPayload {
  return {
    departmentId: form.departmentId ?? null,
    email: form.email || null,
    name: form.name,
    phone: form.phone || null,
    roleIds: form.roleId ? [form.roleId] : [],
    supervisorId: form.supervisorId ?? null,
  };
}
