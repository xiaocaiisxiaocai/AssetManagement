export type HomeChartTheme = {
  axisLine: string;
  gridLine: string;
  muted: string;
  text: string;
};

function readCssColor(variableName: string, fallback: string) {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(variableName)
    .trim();
  return value ? `hsl(${value})` : fallback;
}

export function getChartTheme(): HomeChartTheme {
  const text = readCssColor('--foreground', '#1f2937');
  const muted = readCssColor('--muted-foreground', '#6b7280');
  const border = readCssColor('--border', '#e5e7eb');
  return {
    axisLine: border,
    gridLine: border,
    muted,
    text,
  };
}
