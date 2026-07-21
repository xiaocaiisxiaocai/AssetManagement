import { describe, expect, it } from 'vitest';

import {
  assertWebEleChunkBudget,
  WEB_ELE_CHUNK_LIMIT_KB,
} from './build-budget';

describe('web-ele 构建体积预算', () => {
  it('阈值显著低于旧的 2000 kB 全局阈值', () => {
    expect(WEB_ELE_CHUNK_LIMIT_KB).toBe(1100);
    expect(WEB_ELE_CHUNK_LIMIT_KB).toBeLessThan(2000);
  });

  it('未超过阈值时允许构建', () => {
    expect(() =>
      assertWebEleChunkBudget([
        { fileName: 'bootstrap.js', sizeInBytes: 1_100_000 },
      ]),
    ).not.toThrow();
  });

  it('任一 chunk 超限时抛错并报告文件与体积', () => {
    expect(() =>
      assertWebEleChunkBudget([
        { fileName: 'bootstrap.js', sizeInBytes: 1_100_001 },
      ]),
    ).toThrow(/bootstrap\.js 1100\.00 kB/);
  });
});
