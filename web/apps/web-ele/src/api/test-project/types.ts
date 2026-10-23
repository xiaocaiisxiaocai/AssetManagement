export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface TestProjectPageQuery {
  code?: string;
  deleteStatus?: string;
  name?: string;
  ownerId?: number;
  ownerIds?: number[];
  page: number;
  pageSize: number;
  progressCode?: string;
  progressCodes?: string[];
  projectTypeCode?: string;
  projectTypeCodes?: string[];
}

export interface TestProjectItem {
  id: number;
  name: string;
  code?: null | string;
  projectTypeCode?: null | string;
  projectTypeLabel?: null | string;
  startDate?: null | string;
  plannedFinishDate?: null | string;
  closedDate?: null | string;
  progressCode?: null | string;
  progressLabel?: null | string;
  ownerId?: null | number;
  ownerName?: null | string;
  testStatus?: null | string;
  followUpIntervalDays: number;
  nextFollowUpDueDate?: null | string;
  followUpStatus: 'due' | 'overdue' | 'upcoming' | string;
  latestFollowUpAt?: null | string;
  latestFollowUpContent?: null | string;
  canWriteFollowUp: boolean;
  testingIncomplete?: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canRestore: boolean;
  canPurge: boolean;
  canUpdateProgress: boolean;
  canReopen: boolean;
  createdAt: string;
  isDeleted: boolean;
  deletedAt?: null | string;
  materialCount: number;
  rowVersion?: number;
}

export interface SaveTestProjectPayload {
  name: string;
  code?: null | string;
  projectTypeCode?: null | string;
  startDate?: null | string;
  plannedFinishDate?: null | string;
  closedDate?: null | string;
  progressCode?: null | string;
  ownerId?: null | number;
  testStatus?: null | string;
  followUpIntervalDays: number;
  rowVersion?: number;
}

export interface TestProjectImportRow {
  closedDate?: null | string;
  code: string;
  error: string;
  followUpIntervalDays: number;
  isValid: boolean;
  name: string;
  ownerEmployeeNo: string;
  ownerId?: null | number;
  ownerName: string;
  plannedFinishDate?: null | string;
  progressCode: string;
  progressLabel: string;
  projectTypeCode: string;
  projectTypeLabel: string;
  row: number;
  startDate?: null | string;
  testStatus?: null | string;
}

export interface TestProjectImportResult {
  failedCount: number;
  rows: TestProjectImportRow[];
  successCount: number;
}

export interface UpdateTestProjectProgressPayload {
  closedDate?: null | string;
  progressCode: string;
  rowVersion?: number;
  testStatus?: null | string;
}

export interface TestProjectOption {
  id: number;
  kind: 'project_progress' | 'project_type';
  code: string;
  label: string;
  sort: number;
  isActive: boolean;
}

export interface SaveTestProjectOptionPayload {
  kind: 'project_progress' | 'project_type';
  code: string;
  label: string;
  sort: number;
  isActive: boolean;
}

export interface TestProjectFollowup {
  id: number;
  projectId: number;
  dueDate: string;
  content: string;
  filledById: number;
  filledByName?: null | string;
  filledAt: string;
  isLatest: boolean;
}

export interface SaveTestProjectFollowupPayload {
  content: string;
  dueDate?: null | string;
}

export interface TestProjectStats {
  year: number;
  total: number;
  closed: number;
  inProgress: number;
  landed: number;
  typeDist: { count: number; label: string }[];
  monthlyStat: { closedCount: number; followUpCount: number; month: number }[];
}
