interface AddSignTokenState {
  addedSigners?: null | Record<string, number>;
}

interface AddSignCandidate {
  id: number;
}

export function excludeCurrentUserFromAddSignCandidates<
  T extends AddSignCandidate,
>(candidates: T[], currentUserId: number) {
  return candidates.filter((candidate) => candidate.id !== currentUserId);
}

export function canUserInitiateAddSign(
  token: AddSignTokenState | null | undefined,
  userId: number,
) {
  if (!token || !Number.isInteger(userId) || userId <= 0) return false;

  return !Object.prototype.hasOwnProperty.call(
    token.addedSigners ?? {},
    String(userId),
  );
}
