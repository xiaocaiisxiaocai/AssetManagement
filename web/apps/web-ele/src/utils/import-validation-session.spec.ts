import { describe, expect, it } from 'vitest';

import { createImportValidationSession } from './import-validation-session';

describe('导入校验会话', () => {
  it('清空文件后拒绝旧请求写回', () => {
    const session = createImportValidationSession<{ name: string }>();
    const generation = session.start(new File(['a'], 'a.xlsx'))!;

    session.reset();

    expect(session.canApply(generation)).toBe(false);
    expect(session.selectedFile.value).toBeNull();
    expect(session.rows.value).toEqual([]);
    expect(session.loading.value).toBe(false);
  });

  it('选择新文件后只接受最新请求', () => {
    const session = createImportValidationSession<{ name: string }>();
    const first = session.start(new File(['a'], 'a.xlsx'))!;
    const second = session.start(new File(['b'], 'b.xlsx'))!;

    expect(session.canApply(first)).toBe(false);
    expect(session.canApply(second)).toBe(true);
  });

  it('空文件选择会使在途请求失效', () => {
    const session = createImportValidationSession<{ name: string }>();
    const generation = session.start(new File(['a'], 'a.xlsx'))!;

    expect(session.start(null)).toBeNull();
    expect(session.canApply(generation)).toBe(false);
  });
});
