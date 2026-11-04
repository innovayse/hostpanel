declare module 'vitepress' {
  import type { Ref } from 'vue'
  export function useData(): { theme: Ref<Record<string, unknown>> }
}
