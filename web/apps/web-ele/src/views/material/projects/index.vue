<script lang="ts" setup>
import { provide } from 'vue';

import { projectPageKey } from './project-page-context';
import ProjectCatalogSection from './ProjectCatalogSection.vue';
import ProjectMaterialDialogs from './ProjectMaterialDialogs.vue';
import ProjectRecordDialogs from './ProjectRecordDialogs.vue';
import ProjectWorkspaceSection from './ProjectWorkspaceSection.vue';
import { useProjectPage } from './use-project-page';

defineOptions({ name: 'MaterialProjects' });

const page = useProjectPage();
provide(projectPageKey, page);
const { handledFlows } = page;
</script>

<template>
  <re-page>
    <div class="material-projects-page page-fit p-5">
      <ProjectCatalogSection />
      <ProjectRecordDialogs />
      <ProjectWorkspaceSection :handled-flows="handledFlows" />
      <ProjectMaterialDialogs />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
