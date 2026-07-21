import { describe, expect, it } from 'vitest';

import { hasRequiredRouteAccess } from './route-permission';

describe('隐藏业务路由权限', () => {
  it('无料件审批权限时拒绝进入待审页空壳', () => {
    expect(
      hasRequiredRouteAccess(['material-flow:approve'], ['material-flow:view']),
    ).toBe(false);
  });

  it('无料件查看权限时拒绝进入申请页空壳', () => {
    expect(
      hasRequiredRouteAccess(['material-flow:view'], ['material-flow:approve']),
    ).toBe(false);
  });

  it('具有对应权限或路由无限制时放行', () => {
    expect(
      hasRequiredRouteAccess(
        ['material-flow:approve'],
        ['material-flow:approve'],
      ),
    ).toBe(true);
    expect(hasRequiredRouteAccess(undefined, [])).toBe(true);
  });
});
