export type AssigneeType =
  | ''
  | 'deptManager'
  | 'roleName'
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

export function loadAssigneeSelection(
  assignee?: null | string,
  candidateUsers?: null | string,
  candidateGroups?: null | string,
): AssigneeSelection {
  if (assignee === 'supervisor' || assignee === 'deptManager') {
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

  if (type === 'supervisor' || type === 'deptManager') {
    result.assignee = type;
  } else if (type === 'username') {
    result.assignee = typeof value === 'string' ? value : '';
  } else if (type === 'usernames') {
    result.candidateUsers = Array.isArray(value) ? value.join(',') : value;
  } else if (type === 'roleName') {
    result.candidateGroups = typeof value === 'string' ? value : '';
  }

  return result;
}
