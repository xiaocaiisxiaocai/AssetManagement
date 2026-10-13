import { bindProjectCatalogDeleteActions } from './project-catalog-delete-actions';
import { bindProjectCatalogFormActions } from './project-catalog-form-actions';
import { bindProjectCatalogListActions } from './project-catalog-list-actions';
import { bindProjectCatalogOptionActions } from './project-catalog-option-actions';

export function bindProjectCatalogActions(page: Record<string, any>) {
  const listActions = bindProjectCatalogListActions(page);
  const formActions = bindProjectCatalogFormActions(page, {
    loadBaseOptions: listActions.loadBaseOptions,
    loadData: listActions.loadData,
    loadUsers: listActions.loadUsers,
  });
  const optionActions = bindProjectCatalogOptionActions(page, {
    loadData: listActions.loadData,
    loadOptions: listActions.loadOptions,
  });
  const deleteActions = bindProjectCatalogDeleteActions({
    loadData: listActions.loadData,
  });

  return {
    ...listActions,
    ...formActions,
    ...optionActions,
    ...deleteActions,
  };
}
