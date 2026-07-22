import type { Plugin } from 'vite';

import { Buffer } from 'node:buffer';

/** web-ele 任一生产 JavaScript chunk 的硬上限（Vite 以十进制 kB 报告）。 */
export const WEB_ELE_CHUNK_LIMIT_KB = 1100;

interface ChunkSize {
  fileName: string;
  sizeInBytes: number;
}

export function assertWebEleChunkBudget(
  chunks: readonly ChunkSize[],
  limitInKb = WEB_ELE_CHUNK_LIMIT_KB,
) {
  const limitInBytes = limitInKb * 1000;
  const oversized = chunks
    .filter((chunk) => chunk.sizeInBytes > limitInBytes)
    .sort((left, right) => right.sizeInBytes - left.sizeInBytes);

  if (oversized.length === 0) return;

  const details = oversized
    .map(
      (chunk) =>
        `${chunk.fileName} ${(chunk.sizeInBytes / 1000).toFixed(2)} kB`,
    )
    .join('、');
  throw new Error(`web-ele 构建产物超过 ${limitInKb} kB 单块预算：${details}`);
}

export function createWebEleChunkBudgetPlugin(
  limitInKb = WEB_ELE_CHUNK_LIMIT_KB,
): Plugin {
  return {
    name: 'web-ele-chunk-budget',
    apply: 'build',
    generateBundle(_options, bundle) {
      assertWebEleChunkBudget(
        Object.values(bundle)
          .filter((output) => output.type === 'chunk')
          .map((output) => ({
            fileName: output.fileName,
            sizeInBytes: Buffer.byteLength(output.code, 'utf8'),
          })),
        limitInKb,
      );
    },
  };
}
