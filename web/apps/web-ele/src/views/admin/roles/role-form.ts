export interface RoleListQuery {
  keyword: string;
  page: number;
  pageSize: number;
}

export interface RoleFormState {
  code: string;
  isActive: boolean;
  name: string;
}
