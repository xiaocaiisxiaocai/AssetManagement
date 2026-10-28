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
    expect(resolveNotificationRoute('approval_released', 9)).toEqual({
      path: '/approval/mine',
      query: { flowId: '9', source: 'asset' },
    });
  });

  it.each([
    'material_received',
    'material_approved',
    'material_rejected',
    'material_transferred',
  ])('routes %s to my applications', (type) => {
    expect(resolveNotificationRoute(type, 18)).toEqual({
      path: '/approval/mine',
      query: { flowId: '18', source: 'material' },
    });
  });

  it('opens the project workspace on the receiving tab for a pending receipt', () => {
    expect(resolveNotificationRoute('material_receipt_pending', 18)).toEqual({
      path: '/material/projects',
      query: { flowId: '18', source: 'material', intent: 'receive' },
    });
  });
});
