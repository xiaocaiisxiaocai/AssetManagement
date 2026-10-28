import type { MaterialFlowItem, MaterialItem } from '#/api/material';
import type {
  ProjectDocumentDto,
  ProjectStageDto,
} from '#/api/project-workspace';
import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { getMaterialDetailApi, listMaterialsApi } from '#/api/material';
import { getProjectWorkspaceApi } from '#/api/project-workspace';
import { listTestProjectFollowupsApi } from '#/api/test-project';
import { formatDate, formatDateTime } from '#/utils/date-format';

/**
 * 项目跟进工作台的「导出页面」：不再打印屏幕上的工作台，而是按接口数据生成一份专用的打印报告，
 * 在隐藏 iframe 中调用浏览器打印（可另存为 PDF）。屏幕上的工作台不发生任何变化。
 */
export interface WorkspaceReportData {
  documents: ProjectDocumentDto[];
  flows: MaterialFlowItem[];
  followups: TestProjectFollowup[];
  generatedAt: Date;
  materials: MaterialItem[];
  project: TestProjectItem;
  stages: ProjectStageDto[];
}

const MATERIAL_PAGE_SIZE = 200;
const MATERIAL_PAGE_LIMIT = 20;
const DETAIL_BATCH_SIZE = 5;
const ILLEGAL_FILE_CHARS = /[\\/:*?"<>|]/g;

const MATERIAL_STATUS_LABEL: Record<number, string> = {
  0: '在用',
  1: '已退回厂商',
};

const FLOW_STATUS_LABEL: Record<string, string> = {
  approved: '已通过',
  pending: '审批中',
  receiving: '待接收',
  rejected: '已驳回',
  withdrawn: '已撤回',
};

export function workspaceReportTitle(
  name: string,
  code: null | string | undefined,
  date: Date,
) {
  const sanitize = (value: string) =>
    value.replaceAll(ILLEGAL_FILE_CHARS, '_').trim();
  const stamp = [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('');
  const safeName = sanitize(name);
  const safeCode = sanitize(code ?? '');
  return [safeName, ...(safeCode ? [safeCode] : []), stamp].join('-');
}

export function escapeHtml(value: unknown) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

/** 阶段状态由日期推导：有结束日期为已完成，有开始无结束为当前阶段。 */
function isCompletedStage(stage: ProjectStageDto) {
  return Boolean(stage.endDate);
}

function isCurrentStage(stage: ProjectStageDto) {
  return Boolean(stage.startDate) && !stage.endDate;
}

function text(value: unknown, empty = '—') {
  const content = String(value ?? '').trim();
  return content ? escapeHtml(content) : empty;
}

function cells(values: readonly string[]) {
  return values.map((value) => `<td>${value}</td>`).join('');
}

function tableHtml(options: {
  columns: ReadonlyArray<{ label: string; width: number }>;
  empty: string;
  rows: string[];
}) {
  const colgroup = options.columns
    .map((column) => `<col style="width:${column.width}%">`)
    .join('');
  const head = options.columns
    .map((column) => `<th>${escapeHtml(column.label)}</th>`)
    .join('');
  const body =
    options.rows.length > 0
      ? options.rows.map((row) => `<tr>${row}</tr>`).join('')
      : `<tr><td class="empty" colspan="${options.columns.length}">${escapeHtml(options.empty)}</td></tr>`;
  return `<table><colgroup>${colgroup}</colgroup><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>`;
}

function sectionHtml(title: string, count: null | number, content: string) {
  const badge = count === null ? '' : `<span class="count">${count}</span>`;
  return `<section><h2>${escapeHtml(title)}${badge}</h2>${content}</section>`;
}

function headerHtml(data: WorkspaceReportData) {
  const { project } = data;
  const meta = [
    project.code || '未设置编号',
    project.projectTypeLabel || project.projectTypeCode || '未分类',
    project.progressLabel || project.progressCode || '未设置进度',
    `负责人：${project.ownerName || '-'}`,
    ...(project.isDeleted ? ['已删除'] : []),
  ]
    .map((item) => `<span>${escapeHtml(item)}</span>`)
    .join('');
  const facts = [
    ['开始时间', formatDate(project.startDate)],
    ['计划完成', formatDate(project.plannedFinishDate)],
    ['结案时间', formatDate(project.closedDate)],
    ...(project.nextFollowUpDueDate
      ? [['下次跟进', formatDate(project.nextFollowUpDueDate)]]
      : []),
    ['导出时间', formatDateTime(data.generatedAt)],
  ]
    .map(
      ([label, value]) =>
        `<span><b>${escapeHtml(label)}</b>${escapeHtml(value)}</span>`,
    )
    .join('');
  const summary = project.testStatus?.trim()
    ? `<p class="summary"><b>项目摘要</b>${escapeHtml(project.testStatus.trim())}</p>`
    : '';
  return `<header><h1>${escapeHtml(project.name)}</h1><div class="meta">${meta}</div><div class="facts">${facts}</div>${summary}</header>`;
}

function materialsHtml(materials: readonly MaterialItem[]) {
  const rows = materials.map((item) =>
    cells([
      text(item.materialNo),
      text(item.name),
      text(item.vendorName),
      text([item.model, item.brand].filter(Boolean).join(' ')),
      escapeHtml(item.quantity),
      text(item.dockingVendorName),
      text(item.departmentName),
      text(item.custodianName),
      `<span class="pre">${text(item.remark)}</span>`,
      escapeHtml(MATERIAL_STATUS_LABEL[item.status] ?? item.status),
    ]),
  );
  return sectionHtml(
    '料件清单',
    materials.length,
    tableHtml({
      columns: [
        { label: '料件编号', width: 12 },
        { label: '名称', width: 13 },
        { label: '寄件厂商', width: 10 },
        { label: '型号品牌', width: 12 },
        { label: '数量', width: 5 },
        { label: '对接厂商', width: 10 },
        { label: '部门', width: 10 },
        { label: '保管人', width: 8 },
        { label: '备注', width: 12 },
        { label: '状态', width: 8 },
      ],
      empty: '暂无料件',
      rows,
    }),
  );
}

function stagesHtml(
  stages: readonly ProjectStageDto[],
  documents: readonly ProjectDocumentDto[],
) {
  const ordered = [...stages].sort((a, b) => a.sequence - b.sequence);
  const current = ordered.find((stage) => isCurrentStage(stage));
  const completed = ordered.filter((stage) => isCompletedStage(stage)).length;
  const summary = `<p class="summary"><b>当前阶段</b>${
    current
      ? escapeHtml(`${current.sequence}. ${current.name}`)
      : '暂无进行中阶段'
  }<b>阶段进度</b>${completed} / ${ordered.length}</p>`;
  const rows = ordered.map((stage) => {
    const files = documents
      .filter((document) => document.stageId === stage.id)
      .map((document) => `<div>${escapeHtml(document.fileName)}</div>`)
      .join('');
    const period = stage.startDate
      ? `${formatDate(stage.startDate)} 至 ${stage.endDate ? formatDate(stage.endDate) : '—'}`
      : '—';
    return cells([
      escapeHtml(stage.sequence),
      text(stage.name),
      `<span class="pre">${text(stage.objective)}</span>`,
      `<span class="pre">${text(stage.testContent)}</span>`,
      `<span class="pre">${text(stage.testResult)}</span>`,
      escapeHtml(period),
      files || '—',
    ]);
  });
  return sectionHtml(
    '测试阶段与报告',
    null,
    summary +
      tableHtml({
        columns: [
          { label: '序号', width: 5 },
          { label: '阶段名称', width: 12 },
          { label: '目标', width: 17 },
          { label: '测试内容', width: 22 },
          { label: '测试结果', width: 18 },
          { label: '起止日期', width: 12 },
          { label: '报告附件', width: 14 },
        ],
        empty: '暂无测试阶段',
        rows,
      }),
  );
}

function flowsHtml(flows: readonly MaterialFlowItem[]) {
  const rows = flows.map((flow) =>
    cells([
      text(flow.flowNo),
      text(flow.materialNo),
      text(flow.materialName),
      text(flow.applicant),
      text(flow.transferee),
      `<span class="pre">${text(flow.reason)}</span>`,
      escapeHtml(FLOW_STATUS_LABEL[flow.status] ?? flow.status),
      escapeHtml(formatDateTime(flow.applyTime)),
    ]),
  );
  return sectionHtml(
    '料件流转',
    flows.length,
    tableHtml({
      columns: [
        { label: '流转单号', width: 13 },
        { label: '料件编号', width: 12 },
        { label: '料件名称', width: 13 },
        { label: '发起人', width: 9 },
        { label: '接收人', width: 9 },
        { label: '原因', width: 22 },
        { label: '状态', width: 8 },
        { label: '发起时间', width: 14 },
      ],
      empty: '暂无流转记录',
      rows,
    }),
  );
}

function followupsHtml(followups: readonly TestProjectFollowup[]) {
  const rows = followups.map((item) =>
    cells([
      escapeHtml(formatDateTime(item.filledAt)),
      text(item.filledByName),
      `<span class="pre">${text(item.content)}</span>`,
    ]),
  );
  return sectionHtml(
    '落地跟进',
    followups.length,
    tableHtml({
      columns: [
        { label: '跟进时间', width: 16 },
        { label: '填写人', width: 12 },
        { label: '落地情况', width: 72 },
      ],
      empty: '暂无跟进记录',
      rows,
    }),
  );
}

const REPORT_STYLE = `
@page { size: A4 landscape; margin: 12mm; }
* { box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { margin: 0; color: #1f2329; font: 11px/1.5 "Microsoft YaHei", "PingFang SC", "Noto Sans SC", sans-serif; }
header { padding-bottom: 8px; border-bottom: 2px solid #1d4f91; }
h1 { margin: 0 0 4px; font-size: 18px; }
.meta, .facts { display: flex; flex-wrap: wrap; gap: 2px 16px; color: #4e5969; }
.facts { margin-top: 4px; }
.facts b, .summary b { margin-right: 6px; color: #86909c; font-weight: 400; }
.summary { margin: 6px 0; }
.summary b:not(:first-child) { margin-left: 16px; }
section { margin-top: 14px; }
h2 { margin: 0 0 6px; font-size: 13px; }
.count { margin-left: 6px; color: #86909c; font-weight: 400; }
table { width: 100%; table-layout: fixed; border-collapse: collapse; }
thead { display: table-header-group; }
tr { break-inside: avoid; }
th, td { padding: 4px 6px; text-align: left; vertical-align: top; overflow-wrap: anywhere; border: 1px solid #c9cdd4; }
th { background: #f2f3f5; font-weight: 600; }
.empty { padding: 10px; text-align: center; color: #86909c; }
.pre { white-space: pre-wrap; }
`;

export function buildWorkspaceReportHtml(data: WorkspaceReportData) {
  const title = workspaceReportTitle(
    data.project.name,
    data.project.code,
    data.generatedAt,
  );
  return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>${escapeHtml(title)}</title><style>${REPORT_STYLE}</style></head><body>${[
    headerHtml(data),
    materialsHtml(data.materials),
    stagesHtml(data.stages, data.documents),
    flowsHtml(data.flows),
    followupsHtml(data.followups),
  ].join('')}</body></html>`;
}

async function loadAllMaterials(projectId: number) {
  const items: MaterialItem[] = [];
  for (let page = 1; page <= MATERIAL_PAGE_LIMIT; page++) {
    const result = await listMaterialsApi({
      deleteStatus: 'active',
      page,
      pageSize: MATERIAL_PAGE_SIZE,
      projectId,
    });
    items.push(...result.items);
    if (items.length >= result.total || result.items.length === 0) break;
  }
  return items;
}

/** 每个料件详情包含其完整流转历史；合并后即为本项目全部料件的流转记录。 */
async function loadMaterialFlows(materials: readonly MaterialItem[]) {
  const flows = new Map<number, MaterialFlowItem>();
  for (let index = 0; index < materials.length; index += DETAIL_BATCH_SIZE) {
    const details = await Promise.all(
      materials
        .slice(index, index + DETAIL_BATCH_SIZE)
        .map((material) => getMaterialDetailApi(material.id)),
    );
    for (const detail of details) {
      for (const flow of detail.flows) flows.set(flow.id, flow);
    }
  }
  return [...flows.values()].sort(
    (a, b) => new Date(b.applyTime).getTime() - new Date(a.applyTime).getTime(),
  );
}

export async function loadWorkspaceReportData(
  project: TestProjectItem,
): Promise<WorkspaceReportData> {
  const [materials, workspace, followups] = await Promise.all([
    loadAllMaterials(project.id),
    getProjectWorkspaceApi(project.id),
    listTestProjectFollowupsApi(project.id),
  ]);
  return {
    documents: workspace.documents,
    flows: await loadMaterialFlows(materials),
    followups: [...followups].sort(
      (a, b) => new Date(b.filledAt).getTime() - new Date(a.filledAt).getTime(),
    ),
    generatedAt: new Date(),
    materials,
    project,
    stages: workspace.stages,
  };
}

let activeFrame: HTMLIFrameElement | null = null;
let restoreTitle: (() => void) | null = null;

export function stopWorkspaceReport() {
  activeFrame?.remove();
  activeFrame = null;
  restoreTitle?.();
  restoreTitle = null;
}

/** 在隐藏 iframe 中打开报告并调用浏览器打印，打印结束后移除；同一时间只保留一份。 */
export function printWorkspaceReport(html: string, title: string) {
  stopWorkspaceReport();
  const frame = document.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.tabIndex = -1;
  frame.style.cssText =
    'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden;';
  const previousTitle = document.title;
  document.title = title;
  activeFrame = frame;
  restoreTitle = () => {
    document.title = previousTitle;
  };
  frame.addEventListener(
    'load',
    () => {
      const view = frame.contentWindow;
      if (!view || activeFrame !== frame) return;
      view.addEventListener(
        'afterprint',
        () => {
          if (activeFrame === frame) stopWorkspaceReport();
        },
        { once: true },
      );
      try {
        view.focus();
        view.print();
      } catch {
        stopWorkspaceReport();
      }
    },
    { once: true },
  );
  frame.srcdoc = html;
  document.body.append(frame);
}
