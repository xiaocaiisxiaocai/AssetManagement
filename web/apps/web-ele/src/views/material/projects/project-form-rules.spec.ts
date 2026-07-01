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
    expect(validateProjectForm({ ...baseForm, name: '' }, false)).toBe('请填写项目名称');
    expect(validateProjectForm({ ...baseForm, code: '' }, false)).toBe('请填写项目编号');
    expect(validateProjectForm({ ...baseForm, projectTypeCode: '' }, false)).toBe('请选择项目类型');
    expect(validateProjectForm({ ...baseForm, progressCode: '' }, false)).toBe('请选择进度');
    expect(validateProjectForm({ ...baseForm, ownerId: undefined }, false)).toBe('请选择负责人');
    expect(validateProjectForm({ ...baseForm, followUpIntervalDays: 0 }, false)).toBe('请填写跟进间隔');
    expect(validateProjectForm({ ...baseForm, startDate: '' }, false)).toBe('请选择开始时间');
    expect(validateProjectForm({ ...baseForm, plannedFinishDate: '' }, false)).toBe('请选择计划完成');
  });

  it('新增时结案时间和测试情况可以为空', () => {
    expect(validateProjectForm({ ...baseForm, closedDate: '', testStatus: '' }, false)).toBeNull();
  });

  it('编辑时允许管理者保存其他字段修改，但项目编号和项目名称仍不能为空', () => {
    expect(validateProjectForm({ ...baseForm, code: '' }, true)).toBe('请填写项目编号');
    expect(validateProjectForm({ ...baseForm, name: '' }, true)).toBe('请填写项目名称');
    expect(validateProjectForm({ ...baseForm, projectTypeCode: '' }, true)).toBeNull();
  });
});
