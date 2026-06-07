/**
 * Universal API composable
 *
 * - `useApi`    — wraps `useFetch` with an explicit locale-aware key.
 *                 Uses Nuxt's SSR-aware fetch so relative URLs like `/api/...`
 *                 resolve correctly during server-side rendering.
 *                 Each locale gets its own cached response.
 *                 Re-fetches automatically when locale or query changes.
 *
 * - `apiFetch`  — wraps `$fetch` for one-shot calls (event handlers, Pinia stores).
 *                 Always runs client-side so relative URLs are fine.
 *                 Same headers. On 401: delegates to the global auth guard plugin.
 *
 * 401 handling strategy:
 * - The Nuxt server transparently refreshes expired tokens before the client
 *   ever sees a 401. If a 401 reaches the client, it means the refresh token
 *   is also expired — the session is truly dead.
 * - `apiFetch` catches 401s and calls `$handleAuthExpired()` from the
 *   auth-guard plugin to clear state and redirect to login.
 * - `useApi` does NOT handle 401s — errors from useFetch are caught by
 *   the calling component or by Vue's error boundaries.
 */
import type { MaybeRefOrGetter } from 'vue'
import type { UseFetchOptions } from 'nuxt/app'

/** Options for the useApi composable, extending useFetch options. */
export interface UseApiOptions<T> extends Omit<UseFetchOptions<T>, 'key' | 'query' | 'watch'> {
  /** Query parameters — reactive or static. */
  query?: MaybeRefOrGetter<Record<string, unknown>>
  /** Additional sources to watch for automatic re-fetch. */
  watch?: UseFetchOptions<T>['watch']
}

/**
 * SSR-aware API composable wrapping `useFetch` with locale-aware caching.
 *
 * @param url - API endpoint URL (reactive or static)
 * @param options - Fetch options
 * @returns useFetch return value with reactive data, error, pending, etc.
 */
export function useApi<T = unknown>(url: MaybeRefOrGetter<string>, options: UseApiOptions<T> = {}) {
  const { locale } = useI18n()

  const { query, watch: watchOpts, default: defaultFn, headers: extraHeaders, ...restOptions } = options

  const resolvedUrl = computed(() => toValue(url))
  const resolvedQuery = computed(() => toValue(query) ?? {})

  const key = `api:${locale.value}:${toValue(url)}:${JSON.stringify(toValue(query) ?? {})}`

  return useFetch<T>(resolvedUrl, {
    key,
    query: resolvedQuery,
    headers: computed(() => ({
      'x-locale': locale.value,
      ...(extraHeaders as Record<string, string> | undefined ?? {})
    })),
    default: defaultFn,
    watch: [locale, resolvedQuery, ...(watchOpts ?? [])],
    ...restOptions
  } as UseFetchOptions<T>)
}

/**
 * One-shot API fetch for event handlers and Pinia stores.
 *
 * On 401 (session truly expired — server-side refresh already failed),
 * delegates to the global auth guard plugin to clear state and redirect.
 *
 * @param url - API endpoint URL
 * @param opts - $fetch options
 * @returns Parsed response data
 */
export async function apiFetch<T = unknown>(
  url: string,
  opts: Parameters<typeof $fetch>[1] = {}
): Promise<T> {
  let locale = 'en'
  try {
    locale = (useNuxtApp().$i18n as { locale: { value: string } }).locale.value
  } catch {}

  try {
    return await $fetch<T>(url, {
      ...opts,
      headers: {
        'x-locale': locale,
        ...(opts.headers as Record<string, string> | undefined ?? {})
      }
    })
  } catch (err: unknown) {
    const statusCode = (err as { statusCode?: number })?.statusCode
      ?? (err as { status?: number })?.status

    if (statusCode === 401) {
      const nuxtApp = useNuxtApp()
      const handleAuthExpired = nuxtApp.$handleAuthExpired as (() => Promise<void>) | undefined
      if (handleAuthExpired) {
        await nuxtApp.runWithContext(() => handleAuthExpired())
      }
    }

    throw err
  }
}
