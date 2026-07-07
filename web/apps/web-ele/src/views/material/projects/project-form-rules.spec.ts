import { describe, expect, it } from 'vitest';

import { validateProjectForm } from './project-form-rules';

const baseForm = {
  closedDate: '',
  code: 'CODX-NT-001',
  followUpIntervalDays: 14,
  name: 'CODX-20260629 New Product Validation',
  ownerId: 1,
  plannedFinishDate: '2026-07-29',
  progressCode: 'planned',
  projectTypeCode: 'sample',
  startDate: '2026-06-29',
  testStatus: '',
};

describe('测试项目表单规则', () => {
  it('新增时除结案时间和测试情况外均为必填', () => {
    expect(validateProjectForm({ ...baseForm, name: '' })).toBe('请填写项目名称');
    expect(validateProjectForm({ ...baseForm, code: '' })).toBe('请填写项目编号');
    expect(validateProjectForm({ ...baseForm, projectTypeCode: '' })).toBe('请选择项目类型');
    expect(validateProjectForm({ ...baseForm, progressCode: '' })).toBe('请选择进度');
    expect(validateProjectForm({ ...baseForm, ownerId: undefined })).toBe('请选择负责人');
    expect(validateProjectForm({ ...baseForm, followUpIntervalDays: 0 })).toBe('请填写跟进间隔');
    expect(validateProjectForm({ ...baseForm, startDate: '' })).toBe('请选择开始时间');
    expect(validateProjectForm({ ...baseForm, plannedFinishDate: '' })).toBe('请选择计划完成');
  });

  it('新增时结案时间和测试情况可以为空', () => {
    expect(validateProjectForm({ ...baseForm, closedDate: '', testStatus: '' })).toBeNull();
  });

  it('编辑时必填字段仍不能为空', () => {
    expect(validateProjectForm({ ...baseForm, code: '' })).toBe('请填写项目编号');
    expect(validateProjectForm({ ...baseForm, name: '' })).toBe('请填写项目名称');
    expect(validateProjectForm({ ...baseForm, projectTypeCode: '' })).toBe('请选择项目类型');
    expect(validateProjectForm({ ...baseForm, progressCode: '' })).toBe('请选择进度');
    expect(validateProjectForm({ ...baseForm, ownerId: undefined })).toBe('请选择负责人');
    expect(validateProjectForm({ ...baseForm, startDate: '' })).toBe('请选择开始时间');
    expect(validateProjectForm({ ...baseForm, plannedFinishDate: '' })).toBe('请选择计划完成');
  });
});
