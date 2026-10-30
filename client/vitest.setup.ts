/**
 * Global test setup: stands in for the Nuxt auto-imports.
 *
 * Under Nuxt, `ref`, `computed`, `useI18n`, `useRoute` and friends are injected into every
 * source file at build time. Vitest compiles the same files without that step, so each name a
 * source file uses without importing is defined here as a global. Vue's own reactivity is the
 * real one; the Nuxt composables are minimal stand-ins that a test replaces with
 * `vi.stubGlobal` when it needs a specific answer.
 *
 * @module vitest.setup
 */

import * as vue from 'vue'
import { beforeEach, vi } from 'vitest'

/** Vue names Nuxt auto-imports (the real implementations, not fakes). */
const VUE_AUTO_IMPORTS = [
  'ref', 'shallowRef', 'reactive', 'computed', 'watch', 'watchEffect', 'toRef', 'toRefs', 'unref', 'isRef',
  'readonly', 'nextTick', 'onMounted', 'onUpdated', 'onBeforeUnmount', 'onUnmounted', 'provide', 'inject', 'h',
  'defineComponent', 'useId', 'toValue',
] as const

/**
 * Defines one stand-in as a global, unless the name already has one.
 *
 * Leaving an existing definition alone is what lets a test file stub a name at its top level
 * (before any test runs) and still have it survive the reinstall that runs before each test.
 *
 * @param name The auto-import name.
 * @param value The stand-in.
 */
const define = (name: string, value: unknown): void => {
  if ((globalThis as Record<string, unknown>)[name] === undefined) {
    vi.stubGlobal(name, value)
  }
}

/** The refs `useState` hands out, by key; emptied before every test so no state leaks between tests. */
const sharedState = new Map<string, unknown>()

/**
 * Defines every auto-import stand-in as a global.
 *
 * A test file that calls `vi.unstubAllGlobals()` (to undo its own stubs) removes these too, since
 * vitest restores the globals to what they were before the first stub. So the missing ones are
 * installed again before every test.
 */
const installAutoImports = (): void => {
  for (const name of VUE_AUTO_IMPORTS) {
    define(name, (vue as Record<string, unknown>)[name])
  }

  /** A cookie ref that lives in memory only, as `useCookie` returns under Nuxt. */
  define('useCookie', () => vue.ref<string | null>(null))

  /**
   * `useState` stand-in: a ref per key, created from the initialiser on first use and shared by
   * every later call with the same key, as the shared-state composable behaves under Nuxt.
   */
  define('useState', <T>(key: string, init?: () => T) => {
    if (!sharedState.has(key)) {
      sharedState.set(key, vue.ref(init ? init() : undefined))
    }
    return sharedState.get(key)
  })

  /** `useI18n` stand-in: `t` echoes the key, the locale is `en`. */
  define('useI18n', () => ({
    t: (key: string) => key,
    te: () => false,
    locale: vue.ref('en'),
    locales: vue.ref([{ code: 'en', name: 'English' }, { code: 'ru', name: 'Russian' }, { code: 'hy', name: 'Armenian' }]),
  }))

  /** `useRoute` stand-in: an empty route. */
  define('useRoute', () => ({ params: {}, query: {}, path: '/', fullPath: '/', meta: {} }))

  /** `useRouter` stand-in: navigation is recorded by the spy, not performed. */
  define('useRouter', () => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() }))

  /** `useNuxtApp` stand-in: an app that has finished hydrating, and that runs a callback at once. */
  define('useNuxtApp', () => ({ isHydrating: false, runWithContext: <T>(callback: () => T): T => callback() }))

  /** `onNuxtReady` stand-in: the app is ready at once, so the callback runs now. */
  define('onNuxtReady', (callback: () => void) => {
    callback()
  })

  /** `useRuntimeConfig` stand-in: empty public half. */
  define('useRuntimeConfig', () => ({ public: {} }))

  /** `navigateTo` stand-in: records the call. */
  define('navigateTo', vi.fn())

  /** `definePageMeta` is a compile-time macro under Nuxt; at test time it does nothing. */
  define('definePageMeta', vi.fn())

  /**
   * `useAsyncData` stand-in: runs the handler once, exposes the result as refs, and re-runs the
   * handler when a source named in `options.watch` changes or a getter key does, which is how a
   * page refetches in a new currency or language under Nuxt.
   */
  define('useAsyncData', async (
    key: string | (() => string),
    handler: () => Promise<unknown>,
    options: { watch?: unknown[] } = {},
  ) => {
    const data = vue.ref<unknown>(null)
    const error = vue.ref<unknown>(null)
    /** Runs the handler and records its result or its failure. */
    const run = async (): Promise<void> => {
      try {
        data.value = await handler()
        error.value = null
      } catch (caught) {
        error.value = caught
      }
    }
    await run()
    const sources = [...(options.watch ?? []), ...(typeof key === 'function' ? [key] : [])]
    if (sources.length > 0) {
      vue.watch(sources as Parameters<typeof vue.watch>[0], () => {
        void run()
      })
    }
    return { data, error, pending: vue.ref(false), refresh: run }
  })

  /** `useSiteIdentity` stand-in: the operator-set name falls back to the brand, no tagline. */
  define('useSiteIdentity', () => ({ name: vue.ref('Hostpanel'), tagline: vue.ref('') }))

  /** `useContactDetails` stand-in: nothing configured, so every contact row is hidden. */
  define('useContactDetails', () => ({
    email: vue.ref(''),
    phone: vue.ref(''),
    phoneLink: vue.ref(''),
    telegramUrl: vue.ref(''),
    whatsappUrl: vue.ref(''),
    address: vue.ref(''),
    mapUrl: vue.ref(''),
  }))

  /** `useContactHours` stand-in: no working hours set, so the contact page shows no hours row. */
  define('useContactHours', () => ({ workingHours: vue.ref('') }))
}

installAutoImports()

beforeEach(() => {
  sharedState.clear()
  installAutoImports()
})
