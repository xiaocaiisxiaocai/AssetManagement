export interface AuditListQuery {
  actionType: string;
  dateRange: string[];
  module: string;
  page: number;
  pageSize: number;
  userId?: number;
}
