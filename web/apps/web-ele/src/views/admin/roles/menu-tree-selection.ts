export function mergeMenuTreeSelection(checkedKeys: number[], halfCheckedKeys: number[]) {
  return [...new Set([...checkedKeys, ...halfCheckedKeys])];
}
