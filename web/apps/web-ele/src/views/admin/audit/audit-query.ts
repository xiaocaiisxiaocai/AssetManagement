export interface AuditListQuery {
  actionTypes: string[];
  dateRange: string[];
  module: string;
  page: number;
  pageSize: number;
  userId?: number;
}
