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

export function validateProjectForm(form: ProjectFormLike) {
  if (!form.code.trim()) return '请填写项目编号';
  if (form.code.trim().length > 50) return '项目编号不能超过 50 个字符';
  if (!form.name.trim()) return '请填写项目名称';
  if (form.name.trim().length > 100) return '项目名称不能超过 100 个字符';
  if (!form.projectTypeCode) return '请选择项目类型';
  if (!form.progressCode) return '请选择进度';
  if (!form.ownerId) return '请选择负责人';
  if (!form.followUpIntervalDays || form.followUpIntervalDays < 1)
    return '请填写跟进间隔';
  if (
    !Number.isInteger(form.followUpIntervalDays) ||
    form.followUpIntervalDays > 365
  ) {
    return '跟进间隔须为 1-365 的整数';
  }
  if (form.testStatus.trim().length > 1000)
    return '测试情况不能超过 1000 个字符';
  if (!form.startDate) return '请选择开始时间';
  if (!form.plannedFinishDate) return '请选择计划完成';
  if (form.plannedFinishDate < form.startDate)
    return '计划完成时间不能早于开始时间';
  if (form.closedDate && form.closedDate < form.startDate)
    return '结案时间不能早于开始时间';
  if (form.progressCode === 'closed' && !form.closedDate)
    return '已结案项目必须填写结案时间';
  if (form.progressCode !== 'closed' && form.closedDate)
    return '只有已结案项目才能填写结案时间';
  return null;
}
