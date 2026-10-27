import { ref } from 'vue';

/** A4 横向 297mm 减去左右各 10mm 后，按 96dpi 折算的内容宽度。 */
export const WORKSPACE_PRINT_PAGE_WIDTH = 1047;

const ILLEGAL_FILE_CHARS = /[\\/:*?"<>|]/g;
const PRINTING_CLASS = 'project-workspace-printing';
const PAGE_STYLE_ID = 'project-workspace-print-page';

export const workspacePrinting = ref(false);

export function workspacePrintZoom(widthPx: number) {
  if (!Number.isFinite(widthPx) || widthPx <= 0) return 1;
  return Math.min(1, WORKSPACE_PRINT_PAGE_WIDTH / widthPx);
}

export function workspacePrintTitle(
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

function mountWorkspacePrintPageStyle() {
  if (document.querySelector(`#${PAGE_STYLE_ID}`)) return;
  const style = document.createElement('style');
  style.id = PAGE_STYLE_ID;
  style.textContent =
    '@media print { @page { size: A4 landscape; margin: 10mm; } }';
  document.head.append(style);
}

let activeCleanup: (() => void) | null = null;

export function stopWorkspacePrint() {
  activeCleanup?.();
}

export function startWorkspacePrint(options: {
  drawer: HTMLElement;
  title: string;
}) {
  if (activeCleanup) return false;
  const width = Math.round(options.drawer.getBoundingClientRect().width);
  if (width <= 0) return false;

  const root = document.documentElement;
  const previousTitle = document.title;
  root.classList.add(PRINTING_CLASS);
  root.style.setProperty('--workspace-print-width', `${width}px`);
  root.style.setProperty(
    '--workspace-print-zoom',
    String(workspacePrintZoom(width)),
  );
  document.title = options.title;
  mountWorkspacePrintPageStyle();
  workspacePrinting.value = true;

  let cleaned = false;
  function cleanup() {
    if (cleaned) return;
    cleaned = true;
    window.removeEventListener('afterprint', cleanup);
    root.classList.remove(PRINTING_CLASS);
    root.style.removeProperty('--workspace-print-width');
    root.style.removeProperty('--workspace-print-zoom');
    document.querySelector(`#${PAGE_STYLE_ID}`)?.remove();
    document.title = previousTitle;
    workspacePrinting.value = false;
    if (activeCleanup === cleanup) activeCleanup = null;
  }

  activeCleanup = cleanup;
  window.addEventListener('afterprint', cleanup, { once: true });
  try {
    window.print();
  } catch {
    cleanup();
    return false;
  }
  return true;
}
