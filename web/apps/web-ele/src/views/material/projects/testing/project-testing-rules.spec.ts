import type { ProjectStageFormState } from './project-testing-rules';

import { describe, expect, it } from 'vitest';

import {
  createEmptyStageForm,
  defaultPreviousStageEndDate,
  findPreviousOpenStage,
  isProjectClosed,
  isProjectTesting,
  stageBlocksLanding,
  stageToForm,
  validatePreviousStageClose,
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

  it('缺少结束日期、测试内容或测试结果时不能进入落地跟进', () => {
    expect(
      stageBlocksLanding({
        endDate: null,
        testContent: '内容',
        testResult: '结果',
      }),
    ).toBe(true);
    expect(
      stageBlocksLanding({
        endDate: '2026-10-17',
        testContent: ' ',
        testResult: '结果',
      }),
    ).toBe(true);
    expect(
      stageBlocksLanding({
        endDate: '2026-10-17',
        testContent: '内容',
        testResult: '',
      }),
    ).toBe(true);
    expect(
      stageBlocksLanding({
        endDate: '2026-10-17',
        testContent: '内容',
        testResult: '结果',
      }),
    ).toBe(false);
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

  it('没有结束日期时可以只保存名称', () => {
    expect(
      validateProjectStageForm({
        ...createEmptyStageForm(),
        name: '联调',
      }),
    ).toBeNull();
  });

  it('填写结束日期时要求开始日期、测试内容和结果', () => {
    expect(
      validateProjectStageForm(
        { ...completedForm, endDate: '2026-10-09' },
        '2026-10-18',
      ),
    ).toBe('结束日期不能早于开始日期');
    expect(
      validateProjectStageForm(
        { ...completedForm, startDate: '' },
        '2026-10-18',
      ),
    ).toBe('填写结束日期前请填写开始日期');
    expect(
      validateProjectStageForm(
        { ...completedForm, testContent: '' },
        '2026-10-18',
      ),
    ).toBe('填写结束日期前请填写测试内容');
    expect(
      validateProjectStageForm(
        { ...completedForm, testResult: '' },
        '2026-10-18',
      ),
    ).toBe('填写结束日期前请填写测试结果');
    expect(
      validateProjectStageForm(
        { ...completedForm, endDate: '2026-10-19' },
        '2026-10-18',
      ),
    ).toBe('结束日期不能晚于今天');
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

describe('结束上一阶段', () => {
  const stages = [
    {
      endDate: null,
      id: 1,
      name: '环境试验',
      sequence: 1,
      startDate: '2026-10-01',
      testContent: '温升',
      testResult: '通过',
    },
    {
      endDate: null,
      id: 2,
      name: '耐久试验',
      sequence: 2,
      startDate: '2026-10-10T00:00:00',
      testContent: ' ',
      testResult: '通过',
    },
    {
      endDate: '2026-10-12',
      id: 3,
      name: '已结束',
      sequence: 3,
      startDate: '2026-10-11',
      testContent: '内容',
      testResult: '结果',
    },
    {
      endDate: null,
      id: 4,
      name: '未开始',
      sequence: 4,
      startDate: null,
      testContent: '内容',
      testResult: '结果',
    },
  ];

  it('取序号更小且已开始未结束的阶段中序号最大的那个', () => {
    expect(findPreviousOpenStage(stages, 5)?.id).toBe(2);
    expect(findPreviousOpenStage(stages, 2)?.id).toBe(1);
    expect(findPreviousOpenStage(stages, 5, 2)?.id).toBe(1);
    expect(findPreviousOpenStage(stages, 1)).toBeNull();
  });

  it('默认结束日期为本阶段开始前一天，并限制在上一阶段开始日期至今天', () => {
    expect(
      defaultPreviousStageEndDate('2026-10-01', '2026-10-14', '2026-10-27'),
    ).toBe('2026-10-13');
    expect(
      defaultPreviousStageEndDate('2026-10-14', '2026-10-14', '2026-10-27'),
    ).toBe('2026-10-14');
    expect(
      defaultPreviousStageEndDate('2026-09-01', '2026-10-01', '2026-10-27'),
    ).toBe('2026-09-30');
    expect(
      defaultPreviousStageEndDate('2026-10-01', '2026-11-02', '2026-10-27'),
    ).toBe('2026-10-27');
    expect(
      defaultPreviousStageEndDate('2026-10-28', '2026-10-30', '2026-10-27'),
    ).toBeNull();
  });

  it('缺少内容或结果时要求先补齐，开始日期晚于今天时无法结束', () => {
    expect(
      validatePreviousStageClose(
        {
          name: '耐久试验',
          sequence: 2,
          startDate: '2026-10-10',
          testContent: '内容',
          testResult: '',
        },
        '2026-10-27',
      ),
    ).toBe('第2阶段「耐久试验」尚未填写测试内容或测试结果，请先补齐');
    expect(
      validatePreviousStageClose(
        {
          name: '环境试验',
          sequence: 1,
          startDate: '2026-10-01',
          testContent: '  ',
          testResult: '通过',
        },
        '2026-10-27',
      ),
    ).toBe('第1阶段「环境试验」尚未填写测试内容或测试结果，请先补齐');
    expect(
      validatePreviousStageClose(
        {
          name: '环境试验',
          sequence: 1,
          startDate: '2026-10-28',
          testContent: '内容',
          testResult: '通过',
        },
        '2026-10-27',
      ),
    ).toBe('第1阶段「环境试验」的开始日期晚于今天，无法结束');
    expect(
      validatePreviousStageClose(
        {
          name: '环境试验',
          sequence: 1,
          startDate: '2026-10-01',
          testContent: '内容',
          testResult: '通过',
        },
        '2026-10-27',
      ),
    ).toBeNull();
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
