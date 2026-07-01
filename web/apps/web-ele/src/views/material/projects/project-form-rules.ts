export type ProjectFormLike = {
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

export function validateProjectForm(form: ProjectFormLike, isEdit: boolean) {
  if (!form.name.trim()) return '请填写项目名称';
  if (isEdit) return null;
  if (!form.code.trim()) return '请填写项目编号';
  if (!form.projectTypeCode) return '请选择项目类型';
  if (!form.progressCode) return '请选择进度';
  if (!form.ownerId) return '请选择负责人';
  if (!form.followUpIntervalDays || form.followUpIntervalDays < 1) return '请填写跟进间隔';
  if (!form.startDate) return '请选择开始时间';
  if (!form.plannedFinishDate) return '请选择计划完成';
  return null;
}
