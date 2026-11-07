declare module 'vitepress' {
  import type { Ref } from 'vue'
  export function useData(): { theme: Ref<Record<string, unknown>> }
}

/** Components of the documentation theme, imported by the tests through the `@docs` alias of vitest.config.ts. */
declare module '@docs/*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>
  export default component
}
