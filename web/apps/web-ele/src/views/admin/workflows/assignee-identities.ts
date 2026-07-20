export type AssigneeType =
  | ''
  | 'departmentManager'
  | 'deptManager'
  | 'organizationManager'
  | 'roleName'
  | 'sectionManager'
  | 'supervisor'
  | 'username'
  | 'usernames';

export interface AssigneeSelection {
  type: AssigneeType;
  value: string | string[];
}

export interface SerializedAssignee {
  assignee: string;
  candidateGroups: string;
  candidateUsers: string;
}

export function userAssigneeIdentity(userId: number | string) {
  return `user:${userId}`;
}

export function roleAssigneeIdentity(roleCode: string) {
  return `role:${roleCode}`;
}

export function organizationManagerIdentity(levelCode: string) {
  return `orgManager:${levelCode}`;
}

export function loadAssigneeSelection(
  assignee?: null | string,
  candidateUsers?: null | string,
  candidateGroups?: null | string,
): AssigneeSelection {
  if (assignee?.startsWith('orgManager:')) {
    return {
      type: 'organizationManager',
      value: assignee.slice('orgManager:'.length),
    };
  }
  if (
    assignee === 'supervisor' ||
    assignee === 'deptManager' ||
    assignee === 'sectionManager' ||
    assignee === 'departmentManager'
  ) {
    return { type: assignee, value: '' };
  }
  if (assignee) return { type: 'username', value: assignee };
  if (candidateUsers) {
    return {
      type: 'usernames',
      value: candidateUsers
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean),
    };
  }
  if (candidateGroups) {
    return { type: 'roleName', value: candidateGroups };
  }
  return { type: '', value: '' };
}

export function serializeAssigneeSelection(
  type: AssigneeType,
  value: string | string[],
): SerializedAssignee {
  const result: SerializedAssignee = {
    assignee: '',
    candidateGroups: '',
    candidateUsers: '',
  };

  switch (type) {
    case 'departmentManager':
    case 'deptManager':
    case 'sectionManager':
    case 'supervisor': {
      result.assignee = type;

      break;
    }
    case 'organizationManager': {
      result.assignee =
        typeof value === 'string' && value
          ? organizationManagerIdentity(value)
          : '';

      break;
    }
    case 'roleName': {
      result.candidateGroups = typeof value === 'string' ? value : '';

      break;
    }
    case 'username': {
      result.assignee = typeof value === 'string' ? value : '';

      break;
    }
    case 'usernames': {
      result.candidateUsers = Array.isArray(value) ? value.join(',') : value;

      break;
    }
    // No default
  }

  return result;
}
