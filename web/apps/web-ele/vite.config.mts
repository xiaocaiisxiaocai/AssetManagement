import { defineConfig } from '@vben/vite-config';

import ElementPlus from 'unplugin-element-plus/vite';

import {
  createWebEleChunkBudgetPlugin,
  WEB_ELE_CHUNK_LIMIT_KB,
} from './build-budget';

export default defineConfig(async () => {
  return {
    application: {},
    vite: {
      build: {
        chunkSizeWarningLimit: WEB_ELE_CHUNK_LIMIT_KB,
      },
      plugins: [
        createWebEleChunkBudgetPlugin(),
        ElementPlus({
          format: 'esm',
        }),
      ],
      server: {
        proxy: {
          '/api': {
            changeOrigin: true,
            // 这里填写后端地址
            target: 'http://localhost:5292',
          },
        },
      },
    },
  };
});
