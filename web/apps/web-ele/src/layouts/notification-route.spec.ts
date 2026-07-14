import { describe, expect, it } from 'vitest';

import { resolveNotificationRoute } from './notification-route';

describe('resolveNotificationRoute', () => {
  it('routes pending approval notifications to the target approval', () => {
    expect(resolveNotificationRoute('approval_pending', 42)).toEqual({
      path: '/approval/pending',
      query: { flowId: '42' },
    });
  });

  it('routes approval results to my applications', () => {
    expect(resolveNotificationRoute('approval_approved', 8)).toEqual({
      path: '/approval/mine',
      query: { flowId: '8' },
    });
  });
});
