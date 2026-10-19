import type {
  ProjectStageDto,
  ProjectStageStatus,
} from '#/api/project-workspace';

import { businessDateText, isValidCalendarDate } from '#/utils/business-date';

export interface ProjectStageFormState {
  conclusion: string;
  endDate: string;
  name: string;
  objective: string;
  rowVersion: number;
  sequence?: number;
  startDate: string;
  status: ProjectStageStatus;
  testContent: string;
  testResult: string;
}

const MAX_FILE_SIZE = 20 * 1024 * 1024;
const ALLOWED_REPORT_EXTENSIONS = new Set([
  'docx',
  'jpeg',
  'jpg',
  'pdf',
  'png',
  'pptx',
  'xlsx',
]);

export function createEmptyStageForm(sequence = 1): ProjectStageFormState {
  return {
    conclusion: '',
    endDate: '',
    name: '',
    objective: '',
    rowVersion: 0,
    sequence,
    startDate: '',
    status: 'planned',
    testContent: '',
    testResult: '',
  };
}

export function stageToForm(stage: ProjectStageDto): ProjectStageFormState {
  return {
    conclusion: stage.conclusion ?? '',
    endDate: dateOnly(stage.endDate),
    name: stage.name,
    objective: stage.objective ?? '',
    rowVersion: stage.rowVersion,
    sequence: stage.sequence,
    startDate: dateOnly(stage.startDate),
    status: stage.status,
    testContent: stage.testContent ?? '',
    testResult: stage.testResult ?? '',
  };
}

export function dateOnly(value?: null | string): string {
  return value?.slice(0, 10) ?? '';
}

export function validateProjectStageForm(
  form: ProjectStageFormState,
  today = businessDateText(),
): null | string {
  if (!Number.isInteger(form.sequence) || !form.sequence)
    return '请填写阶段序号';
  if (form.sequence < 1 || form.sequence > 999)
    return '阶段序号须为 1-999 的整数';
  if (!form.name.trim()) return '请填写阶段名称';
  if (form.name.trim().length > 100) return '阶段名称不能超过 100 个字符';
  if (form.objective.length > 2000) return '阶段目标不能超过 2000 个字符';
  if (form.testContent.length > 8000) return '测试内容不能超过 8000 个字符';
  if (form.testResult.length > 8000) return '测试结果不能超过 8000 个字符';
  if (form.conclusion.length > 2000) return '阶段结论不能超过 2000 个字符';
  if (
    (form.startDate && !isValidCalendarDate(form.startDate)) ||
    (form.endDate && !isValidCalendarDate(form.endDate))
  ) {
    return '请填写有效的起止日期';
  }
  if (form.startDate && form.endDate && form.endDate < form.startDate)
    return '结束日期不能早于开始日期';
  if (
    (form.status === 'in_progress' || form.status === 'blocked') &&
    !form.startDate
  ) {
    return '进行中或受阻阶段必须填写开始日期';
  }
  if (form.status === 'completed') {
    if (!form.startDate || !form.endDate)
      return '已完成阶段必须填写开始和结束日期';
    if (!form.testContent.trim()) return '已完成阶段必须填写测试内容';
    if (!form.testResult.trim()) return '已完成阶段必须填写测试结果';
    if (!form.conclusion.trim()) return '已完成阶段必须填写阶段结论';
    if (form.endDate > today) return '已完成阶段的结束日期不能晚于今天';
  }
  return null;
}

export function validateProjectDocument(file: File): null | string {
  const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
  if (!ALLOWED_REPORT_EXTENSIONS.has(extension)) {
    return '仅支持 PDF、DOCX、XLSX、PPTX、PNG、JPG 文件';
  }
  if (file.size <= 0) return '不能上传空文件';
  if (file.size > MAX_FILE_SIZE) return '单个文件不能超过 20MB';
  return null;
}

export function localToday(date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
