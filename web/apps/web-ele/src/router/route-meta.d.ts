import 'vue-router';

declare module 'vue-router' {
  interface RouteMeta {
    /** 访问隐藏页面时至少需要其中一个权限码。 */
    requiredAccessCodes?: string[];
  }
}

export {};
