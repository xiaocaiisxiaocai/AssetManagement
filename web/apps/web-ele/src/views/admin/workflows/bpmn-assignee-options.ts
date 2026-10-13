import type { AssigneeType } from './assignee-identities';

import type { UserOptionDto } from '#/api/user';
import type { WorkflowDesignerOptions } from '#/api/workflow';

import { computed, ref } from 'vue';

import { getWorkflowDesignerOptionsApi } from '#/api/workflow';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { replaceUserOptions } from '#/utils/user-options';

import {
  roleAssigneeIdentity,
  userAssigneeIdentity,
} from './assignee-identities';

export const assigneeTypeOptions = [
  { label: '指定组织层级负责人', value: 'organizationManager' },
  { label: '所属课负责人', value: 'sectionManager' },
  { label: '上级部门负责人', value: 'departmentManager' },
  { label: '所属组织负责人（兼容）', value: 'supervisor' },
  { label: '当前组织负责人（兼容）', value: 'deptManager' },
  { label: '指定人员', value: 'username' },
  { label: '多人审批', value: 'usernames' },
  { label: '按角色审批', value: 'roleName' },
];

export function buildConditionFields(
  organizationLevels: WorkflowDesignerOptions['organizationLevels'],
) {
  return [
    { label: '申请部门', value: 'applicantDept' },
    { label: '申请人角色', value: 'applicantRole' },
    { label: '是否项目负责人', value: 'isProjectOwner' },
    ...organizationLevels.map((level) => ({
      label: `是否需要${level.name}审批`,
      value: `requiresApproval_${level.code}`,
    })),
    { label: '是否需要课级审批（兼容）', value: 'requiresSectionApproval' },
    {
      label: '是否需要部门级审批（兼容）',
      value: 'requiresDepartmentApproval',
    },
  ];
}

export function useAssigneeOptions() {
  const assigneeType = ref<AssigneeType>('');
  const assigneeValue = ref<string | string[]>('');
  const approvalMode = ref<'all' | 'any'>('any');
  const userOptions = ref<UserOptionDto[]>([]);
  const roleOptions = ref<WorkflowDesignerOptions['roles']>([]);
  const departmentOptions = ref<{ label: string; value: string }[]>([]);
  const organizationLevels = ref<WorkflowDesignerOptions['organizationLevels']>(
    [],
  );
  const assigneeOptionsLoading = ref(false);
  const assigneeOptionsRequestGuard = createLatestRequestGuard();

  function selectedAssigneeUserIds() {
    const value = assigneeValue.value;
    const values = Array.isArray(value) ? value : [value];
    return values.flatMap((item) => {
      const match = /^user:(\d+)$/.exec(String(item ?? ''));
      return match ? [Number(match[1])] : [];
    });
  }

  async function loadAssigneeOptions(keyword = '') {
    const requestGeneration = assigneeOptionsRequestGuard.next();
    assigneeOptionsLoading.value = true;
    try {
      const options = await getWorkflowDesignerOptionsApi(keyword, 1, 50);
      if (!assigneeOptionsRequestGuard.isLatest(requestGeneration)) return;
      userOptions.value = replaceUserOptions(
        userOptions.value,
        options.users.items,
        selectedAssigneeUserIds(),
      );
      roleOptions.value = options.roles;
      departmentOptions.value = options.departments.map((department) => ({
        label: department.name,
        value: department.name,
      }));
      organizationLevels.value = options.organizationLevels;
    } catch {
      // 统一请求层已给出错误提示；保留已加载的选项，不阻断设计器。
    } finally {
      if (assigneeOptionsRequestGuard.isLatest(requestGeneration))
        assigneeOptionsLoading.value = false;
    }
  }

  const assigneeValueOptions = computed(() => {
    if (assigneeType.value === 'username') {
      return userOptions.value.map((user) => ({
        label: `${user.name}（${user.employeeNo}）`,
        value: userAssigneeIdentity(user.id),
      }));
    }

    if (assigneeType.value === 'usernames') {
      return userOptions.value.map((user) => ({
        label: `${user.name}（${user.employeeNo}）`,
        value: userAssigneeIdentity(user.id),
      }));
    }

    if (assigneeType.value === 'roleName') {
      return roleOptions.value.map((role) => ({
        label: `${role.name}（${role.code}）`,
        value: roleAssigneeIdentity(role.code),
      }));
    }

    if (assigneeType.value === 'organizationManager') {
      return organizationLevels.value.map((level) => ({
        label: `${level.name}负责人`,
        value: level.code,
      }));
    }

    return [];
  });
  const assigneeValuePlaceholder = computed(() => {
    if (assigneeType.value === 'roleName') return '选择角色';
    if (assigneeType.value === 'organizationManager') return '选择目标组织层级';
    return '选择审批人员';
  });

  return {
    approvalMode,
    assigneeOptionsLoading,
    assigneeType,
    assigneeValue,
    assigneeValueOptions,
    assigneeValuePlaceholder,
    departmentOptions,
    loadAssigneeOptions,
    organizationLevels,
    roleOptions,
  };
}
