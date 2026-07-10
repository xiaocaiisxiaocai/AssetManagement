export type DeleteStatus = 'active' | 'all' | 'deleted';

export type FlatOption = {
  id: number;
  isActive?: boolean;
  label: string;
};

export type OptionKind = 'project_progress' | 'project_type';

export type ProjectFormState = {
  closedDate: string;
  code: string;
  followUpIntervalDays: number;
  name: string;
  ownerId?: number;
  plannedFinishDate: string;
  progressCode: string;
  projectTypeCode: string;
  startDate: string;
  testStatus: string;
};
