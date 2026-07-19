import { describe, expect, it } from 'vitest';

import { resolveNotificationRoute } from './notification-route';

describe('resolveNotificationRoute', () => {
  it('routes pending approval notifications to the target approval', () => {
    expect(resolveNotificationRoute('approval_pending', 42)).toEqual({
      path: '/approval/pending',
      query: { flowId: '42', source: 'asset' },
    });
  });

  it('routes approval results to my applications', () => {
    expect(resolveNotificationRoute('approval_approved', 8)).toEqual({
      path: '/approval/mine',
      query: { flowId: '8', source: 'asset' },
    });
  });

  it.each(['material_transferred', 'material_approved', 'material_rejected'])(
    'routes %s to the existing material-flow workbench',
    (type) => {
      expect(resolveNotificationRoute(type, 18)).toEqual({
        path: '/approval/mine',
        query: { flowId: '18', source: 'material' },
      });
    },
  );
});
