export function isWorkflowDesignerBusy(exporting: boolean, saving: boolean) {
  return exporting || saving;
}
