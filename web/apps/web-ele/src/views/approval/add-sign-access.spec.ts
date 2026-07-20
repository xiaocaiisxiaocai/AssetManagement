import { describe, expect, it } from 'vitest';

import {
  canUserInitiateAddSign,
  excludeCurrentUserFromAddSignCandidates,
} from './add-sign-access';

describe('加签权限边界', () => {
  it('正式节点审批人可以发起加签', () => {
    expect(
      canUserInitiateAddSign(
        {
          addedSigners: { 2: 1 },
        },
        1,
      ),
    ).toBe(true);
  });

  it('动态被加签人不能再次发起加签', () => {
    expect(
      canUserInitiateAddSign(
        {
          addedSigners: { 2: 1 },
        },
        2,
      ),
    ).toBe(false);
  });

  it('加签候选列表不显示当前用户', () => {
    expect(
      excludeCurrentUserFromAddSignCandidates(
        [
          { id: 1, name: '当前审批人' },
          { id: 2, name: '其他主管' },
        ],
        1,
      ),
    ).toEqual([{ id: 2, name: '其他主管' }]);
  });
});
