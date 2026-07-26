export async function unwrap<T>(request: Promise<{ data: T }>): Promise<T> {
  const result = await request;
  return result.data;
}
