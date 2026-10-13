import type { useProjectPage } from './use-project-page';

import type { InjectionKey } from 'vue';
import { inject } from 'vue';

export type ProjectPage = ReturnType<typeof useProjectPage>;

export const projectPageKey: InjectionKey<ProjectPage> = Symbol('projectPage');

export function useProjectPageContext(): ProjectPage {
  const page = inject(projectPageKey);
  if (!page) {
    throw new Error('测试项目页面尚未初始化');
  }
  return page;
}
