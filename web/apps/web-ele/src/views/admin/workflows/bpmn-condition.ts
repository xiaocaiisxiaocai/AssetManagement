export interface ParsedCondition {
  expression: string;
  field: string;
  operator: string;
  value: string;
}

export interface ConditionChoice {
  label: string;
  value: string;
}

export interface ConditionValueSources {
  departments: ConditionChoice[];
  roles: Array<{ code: string; name: string }>;
}

export function parseCondition(expression: string): ParsedCondition {
  const stringMatch = expression.match(
    /^\$\{(applicantDept|applicantRole|isProjectOwner|requiresSectionApproval|requiresDepartmentApproval|requiresApproval_[a-z][a-z0-9_]{0,49})\}\s*(==|!=)\s*["'](.+)["']$/,
  );
  if (stringMatch) {
    return {
      expression,
      field: stringMatch[1] || 'applicantDept',
      operator: stringMatch[2] || '==',
      value: stringMatch[3] || '',
    };
  }

  return {
    expression,
    field: 'applicantDept',
    operator: '==',
    value: '',
  };
}

export function parseConditionExpression(expression: string) {
  const parsed = parseCondition(expression);
  return {
    field: parsed.field,
    operator: parsed.operator,
    value: parsed.value,
  };
}

export function buildExpressionByField(
  field: string,
  operator: string,
  value: string,
) {
  if (!value.trim()) return '';

  const trimmed = value.trim();
  return `\${${field}} ${operator} "${trimmed}"`;
}

export function buildExpression(
  value: string,
  field: string,
  operator: string,
) {
  return buildExpressionByField(field, operator, value);
}

export function buildConditionExpression(
  value: string,
  field: string,
  operator: string,
) {
  return buildExpression(value, field, operator);
}

export function getConditionValueOptions(
  field: string,
  sources: ConditionValueSources,
): ConditionChoice[] {
  if (field === 'applicantRole') {
    return sources.roles.map((role) => ({
      label: `${role.name}（${role.code}）`,
      value: role.code,
    }));
  }

  if (
    field === 'isProjectOwner' ||
    field === 'requiresSectionApproval' ||
    field === 'requiresDepartmentApproval' ||
    field.startsWith('requiresApproval_')
  ) {
    return [
      { label: '是', value: 'true' },
      { label: '否', value: 'false' },
    ];
  }

  return sources.departments;
}

export function conditionValuePlaceholder(field: string) {
  if (field === 'isProjectOwner') return '选择是否项目负责人';
  if (field === 'requiresSectionApproval') return '选择是否需要课级审批';
  if (field === 'requiresDepartmentApproval') return '选择是否需要部门级审批';
  if (field.startsWith('requiresApproval_')) return '选择是否需要该层级审批';
  return field === 'applicantRole' ? '选择申请人角色' : '选择申请部门';
}
