import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { AssetHierarchyModel } from './asset-hierarchy';
import type { AssetListState } from './asset-list-state';

import { bindAssetListLoading } from './asset-list-browse';
import { bindAssetDictionaryQueries } from './asset-list-dictionaries';
import { bindAssetHierarchyDrill } from './asset-list-drill';

export function bindAssetListQueries(input: {
  hasAccessByCodes: (codes: string[]) => boolean;
  hierarchy: AssetHierarchyModel;
  route: RouteLocationNormalizedLoaded;
  state: AssetListState;
}) {
  const page = {
    ...input.hierarchy,
    ...input.state,
    hasAccessByCodes: input.hasAccessByCodes,
    route: input.route,
  };
  const dictionaries = bindAssetDictionaryQueries(page);
  const list = bindAssetListLoading(page);
  const drill = bindAssetHierarchyDrill(page, list.loadData);

  return {
    ...dictionaries,
    ...list,
    ...drill,
  };
}
