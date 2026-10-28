import type { ProjectStageDto } from '#/api/project-workspace';

import { businessDateText, isValidCalendarDate } from '#/utils/business-date';

export interface ProjectStageFormState {
  endDate: string;
  name: string;
  objective: string;
  rowVersion: number;
  sequence?: number;
  startDate: string;
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
    endDate: '',
    name: '',
    objective: '',
    rowVersion: 0,
    sequence,
    startDate: '',
    testContent: '',
    testResult: '',
  };
}

export function stageToForm(stage: ProjectStageDto): ProjectStageFormState {
  return {
    endDate: dateOnly(stage.endDate),
    name: stage.name,
    objective: stage.objective ?? '',
    rowVersion: stage.rowVersion,
    sequence: stage.sequence,
    startDate: dateOnly(stage.startDate),
    testContent: stage.testContent ?? '',
    testResult: stage.testResult ?? '',
  };
}

export function dateOnly(value?: null | string): string {
  return value?.slice(0, 10) ?? '';
}

export function isProjectClosed(
  progressCode?: null | string,
  closedDate?: null | string,
) {
  return Boolean(closedDate) || progressCode?.toLowerCase() === 'closed';
}

export function isProjectTesting(progressCode?: null | string) {
  return progressCode?.toLowerCase() === 'testing';
}

export function stageBlocksLanding(stage: {
  endDate?: null | string;
  testContent?: null | string;
  testResult?: null | string;
}) {
  return (
    !stage.endDate || !stage.testContent?.trim() || !stage.testResult?.trim()
  );
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
  if (
    (form.startDate && !isValidCalendarDate(form.startDate)) ||
    (form.endDate && !isValidCalendarDate(form.endDate))
  ) {
    return '请填写有效的起止日期';
  }
  if (form.startDate && form.endDate && form.endDate < form.startDate)
    return '结束日期不能早于开始日期';
  if (form.endDate) {
    if (!form.startDate) return '填写结束日期前请填写开始日期';
    if (!form.testContent.trim()) return '填写结束日期前请填写测试内容';
    if (!form.testResult.trim()) return '填写结束日期前请填写测试结果';
    if (form.endDate > today) return '结束日期不能晚于今天';
  }
  return null;
}

export function findPreviousOpenStage<
  T extends {
    endDate?: null | string;
    id?: number;
    sequence: number;
    startDate?: null | string;
  },
>(stages: readonly T[], sequence?: number, excludeStageId?: null | number) {
  if (!Number.isInteger(sequence)) return null;
  let selected: null | T = null;
  for (const stage of stages) {
    if (
      excludeStageId !== null &&
      excludeStageId !== undefined &&
      stage.id === excludeStageId
    )
      continue;
    if (stage.sequence >= (sequence ?? 0)) continue;
    if (!dateOnly(stage.startDate) || dateOnly(stage.endDate)) continue;
    if (!selected || stage.sequence > selected.sequence) selected = stage;
  }
  return selected;
}

export function defaultPreviousStageEndDate(
  previousStartDate: string,
  currentStartDate: string,
  today: string,
): null | string {
  const previousStart = dateOnly(previousStartDate);
  const currentStart = dateOnly(currentStartDate);
  if (
    !isValidCalendarDate(previousStart) ||
    !isValidCalendarDate(currentStart) ||
    !isValidCalendarDate(today) ||
    previousStart > today
  ) {
    return null;
  }
  const candidate = shiftCalendarDate(currentStart, -1);
  if (candidate < previousStart) return previousStart;
  if (candidate > today) return today;
  return candidate;
}

export function validatePreviousStageClose(
  stage: {
    name: string;
    sequence: number;
    startDate?: null | string;
    testContent?: null | string;
    testResult?: null | string;
  },
  today: string,
): null | string {
  if (!stage.testContent?.trim() || !stage.testResult?.trim()) {
    return `第${stage.sequence}阶段「${stage.name}」尚未填写测试内容或测试结果，请先补齐`;
  }
  const start = dateOnly(stage.startDate);
  if (start > today) {
    return `第${stage.sequence}阶段「${stage.name}」的开始日期晚于今天，无法结束`;
  }
  return null;
}

function shiftCalendarDate(value: string, days: number) {
  const year = Number(value.slice(0, 4));
  const month = Number(value.slice(5, 7));
  const day = Number(value.slice(8, 10));
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() + days);
  const nextMonth = String(date.getUTCMonth() + 1).padStart(2, '0');
  const nextDay = String(date.getUTCDate()).padStart(2, '0');
  return `${date.getUTCFullYear()}-${nextMonth}-${nextDay}`;
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
