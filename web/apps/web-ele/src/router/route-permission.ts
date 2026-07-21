export function hasRequiredRouteAccess(
  requiredAccessCodes: string[] | undefined,
  userAccessCodes: string[],
) {
  if (!requiredAccessCodes || requiredAccessCodes.length === 0) return true;
  const userCodes = new Set(userAccessCodes);
  return requiredAccessCodes.some((code) => userCodes.has(code));
}
