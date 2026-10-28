import {
  bindProjectFlowActions,
  type ProjectFlowListKind,
} from './project-flow-actions';
import { bindProjectFollowupActions } from './project-followup-actions';
import { bindProjectMaterialCommandActions } from './project-material-command-actions';
import { bindProjectMaterialQueryActions } from './project-material-query-actions';

export function bindProjectWorkspaceActions(page: Record<string, any>) {
  // 跟进会调用料件函数，料件变更会调用流转函数；占位后在调用时读取，避免循环依赖。
  const lateDeps = {} as {
    loadProjectFlows: (
      projectId?: number,
      kind?: ProjectFlowListKind,
    ) => Promise<void>;
    loadProjectMaterials: (projectId?: number) => Promise<void>;
    resetMaterialQuery: () => void;
  };
  const followups = bindProjectFollowupActions(page, lateDeps);
  const materialQueries = bindProjectMaterialQueryActions(page, {
    loadData: page.loadData,
  });
  lateDeps.loadProjectMaterials = materialQueries.loadProjectMaterials;
  lateDeps.resetMaterialQuery = materialQueries.resetMaterialQuery;
  const materialCommands = bindProjectMaterialCommandActions(page, {
    loadBaseOptions: page.loadBaseOptions,
    loadData: page.loadData,
    loadProjectFlows: (projectId?: number) =>
      lateDeps.loadProjectFlows(projectId),
    loadProjectMaterials: materialQueries.loadProjectMaterials,
    loadUsers: page.loadUsers,
    searchUsers: page.searchUsers,
  });
  const flows = bindProjectFlowActions(page, {
    loadProjectMaterials: materialQueries.loadProjectMaterials,
  });
  lateDeps.loadProjectFlows = flows.loadProjectFlows;

  return {
    ...followups,
    ...materialQueries,
    ...materialCommands,
    ...flows,
  };
}
