/**
 * 该文件可自行根据业务逻辑进行调整
 */
import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  errorMessageResponseInterceptor,
  HttpStatusCode,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { ElMessage } from 'element-plus';

import { useAuthStore } from '#/store';

import { isLoginRequestUrl } from './auth-response';

// import { refreshTokenApi } from './core';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

// 并发请求同时触发 401 时，避免每个响应都重复调用一次登出流程。
let isLoggingOut = false;

function createRequestClient(baseURL: string) {
  const client = new RequestClient({
    baseURL,
  });

  /**
   * 重新认证逻辑
   */
  // async function doReAuthenticate() {
  //   console.warn('Access token or refresh token is invalid or expired. ');
  //   const accessStore = useAccessStore();
  //   const authStore = useAuthStore();
  //   accessStore.setAccessToken(null);
  //   if (
  //     preferences.app.loginExpiredMode === 'modal' &&
  //     accessStore.isAccessChecked
  //   ) {
  //     accessStore.setLoginExpired(true);
  //   } else {
  //     await authStore.logout();
  //   }
  // }

  /**
   * 刷新token逻辑
   */
  // async function doRefreshToken() {
  //   const accessStore = useAccessStore();
  //   const resp = await refreshTokenApi();
  //   const newToken = resp.data;
  //   accessStore.setAccessToken(newToken);
  //   return newToken;
  // }

  function formatToken(token: null | string) {
    return token ? `Bearer ${token}` : null;
  }

  // 请求头处理
  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      config.headers.Authorization = formatToken(accessStore.accessToken);
      config.headers['Accept-Language'] = preferences.app.locale;
      // FormData 必须交给浏览器/axios 自动生成 multipart boundary，
      // 否则会按默认 JSON Content-Type 发出，后端无法绑定 IFormFile。
      if (config.data instanceof FormData) {
        delete config.headers['Content-Type'];
      }
      return config;
    },
  });

  // response数据解构
  client.addResponseInterceptor({
    fulfilled: (response) => {
      const { data, status, headers, config } = response;
      const accessStore = useAccessStore();
      if (headers.accesstoken) {
        accessStore.setAccessToken(response.headers.accesstoken);
      }
      if (config.responseType === 'blob') {
        return response;
      }
      if (status === HttpStatusCode.NoContent) {
        ElMessage.success('操作成功');
        return;
      }
      if (status === HttpStatusCode.Ok) {
        // 导入类接口需要拿到后端返回的行级错误明细，由调用方显式跳过业务错误拦截。
        if ((config as { skipBusinessError?: boolean }).skipBusinessError) {
          return data;
        }
        if (data?.code && data.code !== 0) {
          ElMessage.error(data.message || '请求失败');
          throw new Error(data.message || '请求失败');
        }
        return data;
      }
      throw Object.assign({}, response, { response });
    },
  });

  // token过期的处理
  // client.addResponseInterceptor(
  //   authenticateResponseInterceptor({
  //     client,
  //     doReAuthenticate,
  //     doRefreshToken,
  //     enableRefreshToken: preferences.app.enableRefreshToken,
  //     formatToken,
  //   }),
  // );

  // 通用的错误处理,如果没有进入上面的错误处理逻辑，就会进入这里
  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseMessage = (data: any) =>
        data?.message || data?.error?.message || msg;
      const { code } = error;
      if (code === 'ECONNABORTED' || code === 'ERR_NETWORK') {
        ElMessage.warning(msg);
        return;
      }
      // 业务错误已在上一个响应拦截器弹过提示并 throw(普通 Error,无 response),
      // 此处直接放行,避免重复处理与解构 undefined 再弹一个 TypeError 提示框
      if (!error?.response) {
        throw error;
      }
      const {
        response: { config, data },
        status,
      } = error;
      const { validationErrors } = error;
      switch (status) {
        case HttpStatusCode.BadRequest: {
          if (Array.isArray(validationErrors)) {
            validationErrors.forEach((element) => {
              ElMessage.warning(element.message);
            });
          } else {
            ElMessage.warning(responseMessage(data));
          }
          break;
        }
        case HttpStatusCode.Forbidden: {
          ElMessage.warning(responseMessage(data));
          break;
        }
        case HttpStatusCode.Unauthorized: {
          if (isLoginRequestUrl(config?.url)) {
            ElMessage.warning(responseMessage(data));
            break;
          }
          if (!isLoggingOut) {
            isLoggingOut = true;
            const authStore = useAuthStore();
            void authStore
              .logout()
              .catch((logoutError) => {
                // 本地状态已在 logout 中清理，路由跳转失败不应形成未处理 Promise，
                // 但仍记录下来便于排查登出失败的原因。
                console.warn('[request] 401 登出流程失败', logoutError);
              })
              .finally(() => {
                isLoggingOut = false;
              });
          }
          break;
        }
        default: {
          ElMessage.error(responseMessage(data));
          break;
        }
      }
      throw error;
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL);

export const baseRequestClient = new RequestClient({ baseURL: apiURL });
