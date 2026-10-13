export type AuditTagType = 'danger' | 'info' | 'success' | 'warning';

export interface AuditChange {
  after?: unknown;
  before?: unknown;
  field: string;
}

export interface ParsedDetail {
  changes: AuditChange[];
  error?: null | string;
  statusCode?: null | number;
  success: boolean;
}

const emptyDetail: ParsedDetail = { changes: [], success: true };

const actionLabelMap: Record<string, string> = {
  POST: '新增',
  PUT: '修改',
  DELETE: '删除',
  business: '业务',
  cleanup: '清理',
  purge: '彻底删除',
  remind: '催办',
  soft_delete: '软删除',
};

const moduleLabelMap: Record<string, string> = {
  Approval: '审批流转',
  Asset: '资产',
  AssetCategory: '资产分类',
  AuditLog: '审计日志',
  Auth: '登录认证',
  DatabaseBackup: '数据库备份',
  Department: '部门',
  File: '文件',
  Location: '位置',
  MaterialFlow: '料件流转',
  Menu: '菜单',
  Notification: '通知',
  Permission: '权限',
  RbacMenu: '菜单',
  Role: '角色',
  Setting: '系统参数',
  TestMaterial: '测试料件',
  TestProject: '测试项目',
  User: '用户',
  Workflow: '工作流',
};

export function actionTypeLabel(type: string): string {
  return actionLabelMap[type] ?? type;
}

export function actionType(type: string): AuditTagType | undefined {
  if (type === 'remind') return 'warning';
  if (type === 'business') return 'info';
  if (type === 'cleanup') return 'info';
  if (type === 'DELETE' || type === 'purge') return 'danger';
  if (type === 'soft_delete') return 'warning';
  if (type === 'POST') return 'success';
  if (type === 'PUT') return 'info';
  return undefined;
}

export function moduleLabel(type?: null | string): string {
  if (!type) return '-';
  return moduleLabelMap[type] ?? type;
}

export function parseDetail(detail?: null | string): ParsedDetail {
  if (!detail) return emptyDetail;
  try {
    const parsed = JSON.parse(detail) as {
      changes?: AuditChange[];
      error?: null | string;
      statusCode?: null | number;
      success?: boolean;
    };
    return {
      changes: Array.isArray(parsed.changes) ? parsed.changes : [],
      error: parsed.error,
      statusCode: parsed.statusCode,
      success: parsed.success ?? true,
    };
  } catch {
    return emptyDetail;
  }
}

export function formatDuration(ms?: null | number): string {
  if (ms === null || ms === undefined) return '-';
  if (ms < 1000) return `${ms} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

export function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '(空)';
  if (typeof value === 'boolean') return value ? '是' : '否';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}
