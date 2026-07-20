export function safeInternalRedirect(
  value: null | string | undefined,
  fallback: string,
) {
  if (!value) return fallback;

  let decoded: string;
  try {
    decoded = decodeURIComponent(value);
  } catch {
    return fallback;
  }

  const hasControlCharacter = [...decoded].some((character) => {
    const codePoint = character.codePointAt(0) ?? 0;
    return codePoint <= 31 || codePoint === 127;
  });

  if (
    !decoded.startsWith('/') ||
    decoded.startsWith('//') ||
    decoded.startsWith('/\\') ||
    decoded.includes('\\') ||
    /%(?:2f|5c)/i.test(decoded) ||
    hasControlCharacter
  ) {
    return fallback;
  }
  return decoded;
}
