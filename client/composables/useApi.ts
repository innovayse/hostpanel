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
 *                 Same headers + 401 handling.
 */
import type { MaybeRefOrGetter } from 'vue'
import type { UseFetchOptions } from 'nuxt/app'

export interface UseApiOptions<T> extends Omit<UseFetchOptions<T>, 'key' | 'query' | 'watch'> {
  query?: MaybeRefOrGetter<Record<string, unknown>>
  watch?: UseFetchOptions<T>['watch']
}

export function useApi<T = unknown>(url: MaybeRefOrGetter<string>, options: UseApiOptions<T> = {}) {
  const { locale } = useI18n()

  // Destructure known overrides; spread the rest (server, lazy, transform, etc.) through to useFetch
  const { query, watch: watchOpts, default: defaultFn, headers: extraHeaders, ...restOptions } = options

  const resolvedUrl = computed(() => toValue(url))
  const resolvedQuery = computed(() => toValue(query) ?? {})

  // Explicit key: locale + url + serialized query → unique SSR cache per locale
  const key = `api:${locale.value}:${toValue(url)}:${JSON.stringify(toValue(query) ?? {})}`

  return useFetch<T>(resolvedUrl, {
    key,
    query: resolvedQuery,
    headers: computed(() => ({
      'x-locale': locale.value,
      ...(extraHeaders as Record<string, string> | undefined ?? {})
    })),
    default: defaultFn,
    // re-run when locale or query changes
    watch: [locale, resolvedQuery, ...(watchOpts ?? [])],
    onResponseError({ response }) {
      if (response.status === 401) navigateTo('/client/login')
    },
    // Forward any remaining options (server, lazy, immediate, transform, …)
    ...restOptions
  } as UseFetchOptions<T>)
}

export async function apiFetch<T = unknown>(
  url: string,
  opts: Parameters<typeof $fetch>[1] = {}
): Promise<T> {
  let locale = 'en'
  try {
    locale = (useNuxtApp().$i18n as { locale: { value: string } }).locale.value
  } catch {}

  return $fetch<T>(url, {
    ...opts,
    headers: {
      'x-locale': locale,
      ...(opts.headers as Record<string, string> | undefined ?? {})
    },
    onResponseError({ response }) {
      if (response.status === 401) {
        useNuxtApp().runWithContext(() => navigateTo('/client/login'))
      }
    }
  })
}
