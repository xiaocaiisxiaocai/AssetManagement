import type { ProjectStageFormState } from './project-testing-rules';

import { describe, expect, it } from 'vitest';

import {
  createEmptyStageForm,
  isProjectClosed,
  isProjectTesting,
  stageToForm,
  validateProjectDocument,
  validateProjectStageForm,
} from './project-testing-rules';

const completedForm: ProjectStageFormState = {
  endDate: '2026-10-17',
  name: '样机环境验证',
  objective: '验证样机在目标环境下的稳定性',
  rowVersion: 2,
  sequence: 2,
  startDate: '2026-10-10',
  status: 'completed',
  testContent: '完成温度、振动和连续运行测试',
  testResult: '全部指标符合测试要求',
};

describe('测试阶段表单规则', () => {
  it('只有项目真正结案时才标记结案阶段', () => {
    expect(isProjectClosed('landing', null)).toBe(false);
    expect(isProjectClosed('closed', null)).toBe(true);
    expect(isProjectClosed('landing', '2026-10-20')).toBe(true);
  });

  it('只有项目进入测试中后才允许新增测试阶段', () => {
    expect(isProjectTesting('planning')).toBe(false);
    expect(isProjectTesting('testing')).toBe(true);
    expect(isProjectTesting('landing')).toBe(false);
  });

  it('阶段序号必须为 1-999 的整数且名称必填', () => {
    expect(validateProjectStageForm({ ...completedForm, sequence: 0 })).toBe(
      '请填写阶段序号',
    );
    expect(validateProjectStageForm({ ...completedForm, sequence: 1000 })).toBe(
      '阶段序号须为 1-999 的整数',
    );
    expect(validateProjectStageForm({ ...completedForm, sequence: 1.5 })).toBe(
      '请填写阶段序号',
    );
    expect(validateProjectStageForm({ ...completedForm, name: '  ' })).toBe(
      '请填写阶段名称',
    );
  });

  it('进行中和受阻阶段必须填写开始日期', () => {
    for (const status of ['in_progress', 'blocked'] as const) {
      expect(
        validateProjectStageForm({
          ...createEmptyStageForm(),
          name: '联调',
          startDate: '',
          status,
        }),
      ).toBe('进行中或受阻阶段必须填写开始日期');
    }
  });

  it('完成阶段要求完整测试内容和有效日期', () => {
    expect(
      validateProjectStageForm(
        { ...completedForm, endDate: '2026-10-09' },
        '2026-10-18',
      ),
    ).toBe('结束日期不能早于开始日期');
    expect(
      validateProjectStageForm(
        { ...completedForm, testContent: '' },
        '2026-10-18',
      ),
    ).toBe('已完成阶段必须填写测试内容');
    expect(
      validateProjectStageForm(
        { ...completedForm, testResult: '' },
        '2026-10-18',
      ),
    ).toBe('已完成阶段必须填写测试结果');
    expect(
      validateProjectStageForm(
        { ...completedForm, endDate: '2026-10-19' },
        '2026-10-18',
      ),
    ).toBe('已完成阶段的结束日期不能晚于今天');
    expect(validateProjectStageForm(completedForm, '2026-10-18')).toBeNull();
  });

  it('严格执行各长文本上限', () => {
    expect(
      validateProjectStageForm({
        ...completedForm,
        objective: '目'.repeat(2001),
      }),
    ).toBe('阶段目标不能超过 2000 个字符');
    expect(
      validateProjectStageForm({
        ...completedForm,
        testResult: '结'.repeat(8001),
      }),
    ).toBe('测试结果不能超过 8000 个字符');
  });

  it('将后端 DateTime 序列化值转为日期表单值', () => {
    const form = stageToForm({
      endDate: '2026-10-18T00:00:00',
      id: 1,
      name: '环境测试',
      objective: null,
      projectId: 9,
      rowVersion: 1,
      sequence: 1,
      startDate: '2026-10-12T00:00:00',
      status: 'in_progress',
      testContent: null,
      testResult: null,
      updatedAt: '2026-10-18T08:00:00Z',
    });

    expect(form.startDate).toBe('2026-10-12');
    expect(form.endDate).toBe('2026-10-18');
  });
});

describe('测试报告文件规则', () => {
  it('接受约定格式且兼容大写扩展名', () => {
    const file = new File(['report'], '阶段报告.PDF', {
      type: 'application/octet-stream',
    });
    expect(validateProjectDocument(file)).toBeNull();
  });

  it('拒绝不支持格式、空文件和超过 20MB 的文件', () => {
    expect(validateProjectDocument(new File(['x'], 'report.txt'))).toBe(
      '仅支持 PDF、DOCX、XLSX、PPTX、PNG、JPG 文件',
    );
    expect(validateProjectDocument(new File([], 'report.pdf'))).toBe(
      '不能上传空文件',
    );
    const oversized = new File([new Uint8Array(20 * 1024 * 1024 + 1)], 'a.pdf');
    expect(validateProjectDocument(oversized)).toBe('单个文件不能超过 20MB');
  });
});
