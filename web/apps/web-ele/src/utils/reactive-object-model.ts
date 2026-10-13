import { computed } from 'vue';

/** 让子组件通过 v-model 改父页里的 reactive 对象，同时保持同一引用。 */
export function reactiveObjectModel<T extends object>(state: T) {
  return computed<T>({
    get: () => state,
    set: (value) => Object.assign(state, value),
  });
}
