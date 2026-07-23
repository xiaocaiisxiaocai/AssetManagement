export const organizationChildLevelCodes: Record<string, string[]> = {
  company: ['division', 'department'],
  department: ['section'],
  division: ['department', 'section'],
  section: [],
};

export function getAllowedOrganizationLevelCodes(
  parentLevelCode?: null | string,
) {
  if (!parentLevelCode) {
    return ['company', 'division', 'department', 'section'];
  }
  return organizationChildLevelCodes[parentLevelCode] ?? [];
}

export function getDefaultOrganizationLevelCode(
  parentLevelCode?: null | string,
) {
  if (!parentLevelCode) return 'company';
  return getAllowedOrganizationLevelCodes(parentLevelCode)[0] ?? '';
}
