import { defineConfig } from '@vben/vite-config';

import ElementPlus from 'unplugin-element-plus/vite';

export default defineConfig(async () => {
  return {
    application: {},
    vite: {
      plugins: [
        ElementPlus({
          format: 'esm',
        }),
      ],
      server: {
        proxy: {
          '/api': {
            // 这里填写后端地址
            target: 'http://localhost:5000',
            changeOrigin: true,
            bypass(req, res: any, options: any) {
              //这段代码可以看到代理后的地址
              const proxyURL = options.target + req.url;
              console.log('proxyURL', proxyURL);
              res.setHeader('x-req-proxyURL', proxyURL); // 设置响应头可以看到
            },
          },
        },
      },
    },
  };
});
