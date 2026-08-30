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
 *
 * ## Why this file carries an overloaded signature rather than one inferred return
 *
 * The previous version ended `} as UseFetchOptions<T>)`, casting the whole options object at
 * the call to `useFetch`. That cast pinned `UseFetchOptions`'s six type parameters to their
 * defaults — including `R extends NitroFetchRequest = string & {}` — while `useFetch` was
 * inferring `ReqT` from the `ComputedRef<string>` url beside it. The two disagreed, neither
 * overload matched, and TypeScript fell back to the last one with `DataT` left unresolved.
 *
 * The visible cost was not one error here but roughly a hundred and seventy across the app:
 * every `data.value` reached through this composable typed as
 * `never[] | NonNullable<PickFrom<_ResT, KeysOf<DataT>>>`, on which no property of `T` exists.
 * Pages read `invoice.total` and got TS2339 — a message that reads like a wrong field name and
 * is in fact a wrong cast two layers away. Removing the cast is what makes a genuine
 * field-name mismatch legible again, so it is a prerequisite for trusting the rest of the run.
 *
 * The two overloads mirror `useFetch`'s own: with a `default` factory the data is never
 * `undefined`, without one it is. Collapsing them into a single `T | undefined` return would
 * be sound but would force a `?? []` at call sites that already guaranteed a value, so the
 * distinction is kept.
 *
 * @module composables/useApi
 */
import type { MaybeRefOrGetter, MultiWatchSources } from 'vue'
import type { AsyncData, UseFetchOptions } from 'nuxt/app'
import type { FetchError } from 'ofetch'

/**
 * The key list `useFetch` uses to describe a picked subset of a response.
 *
 * Declared here rather than imported: Nuxt exports it from
 * `nuxt/dist/app/composables/asyncData`, which is an internal path `#app` does not re-export,
 * and reaching into it would tie this file to a build layout that moves between releases. The
 * definition is one line and is only needed to pin `useFetch`'s `PickKeys` parameter below.
 */
type KeysOf<T> = Array<T extends T ? keyof T extends string ? keyof T : never : never>

/**
 * Options for {@link useApi}, extending `useFetch`'s own.
 *
 * `key` is omitted because this composable builds a locale-aware one itself — a caller-supplied
 * key would defeat the per-locale cache separation that is the reason this wrapper exists.
 *
 * `method` is omitted because this is the read path. The split at the top of this module is the
 * whole design: `useApi` is the SSR-aware GET, `apiFetch` is the one-shot call an event handler
 * or a store action makes to change something. No call site has ever passed a method, and
 * fixing it at `get` is what lets the `useFetch` call below pin its type parameters at all.
 */
export interface UseApiOptions<T> extends Omit<UseFetchOptions<T>, 'key' | 'query' | 'watch' | 'default' | 'method'> {
  /** Query parameters — reactive or static. */
  query?: MaybeRefOrGetter<Record<string, unknown>>
  /**
   * Additional sources to watch for automatic re-fetch, or `false` to disable watching
   * entirely. `false` suppresses the locale and query watchers this composable adds too — it
   * means "fetch once", which is the only reading under which it is worth passing.
   */
  watch?: MultiWatchSources | false
  /**
   * Value to hold before the first response arrives and whenever the request fails.
   *
   * Typed `() => T` rather than `useFetch`'s `() => undefined`: the wrapper's whole purpose at
   * a call site passing this is to guarantee `data.value` is a `T`, and the overload below
   * reflects that in the return type.
   */
  default?: () => T
}

/** The two shapes {@link useApi} answers in — see the module note for why they are separate. */
interface UseApi {
  /**
   * Reads an endpoint whose data is guaranteed present, because a `default` factory supplies
   * a value before the first response and after a failure.
   *
   * @param url - API endpoint URL (reactive or static).
   * @param options - Fetch options, including the `default` factory that makes this overload apply.
   * @returns The `useFetch` handle, with `data` typed as `T` rather than `T | undefined`.
   */
  <T>(url: MaybeRefOrGetter<string>, options: UseApiOptions<T> & { default: () => T }): AsyncData<T, FetchError | undefined>

  /**
   * Reads an endpoint whose data is absent until the response arrives.
   *
   * @param url - API endpoint URL (reactive or static).
   * @param options - Fetch options.
   * @returns The `useFetch` handle, with `data` typed as `T | undefined`.
   */
  <T>(url: MaybeRefOrGetter<string>, options?: UseApiOptions<T>): AsyncData<T | undefined, FetchError | undefined>
}

/**
 * The shared implementation behind {@link useApi}'s two overloads.
 *
 * Declared with the honest return type — data may be absent — because that is what is true
 * when no `default` factory is supplied. The `default` case is expressed by the overload, not
 * by this signature; see the note on {@link useApi}.
 *
 * The return type is left to inference rather than annotated. `useFetch` describes its data as
 * `PickFrom<T, KeysOf<T>>`, which only reduces to `T` once `T` is a concrete type — that is, at
 * a call site, not here. Annotating it would mean asserting the reduction while `T` is still a
 * parameter; letting it infer means every caller gets the reduced form for free.
 *
 * @param url - API endpoint URL (reactive or static).
 * @param options - Fetch options.
 * @returns The `useFetch` handle.
 */
const request = <T>(
  url: MaybeRefOrGetter<string>,
  options: UseApiOptions<T> = {}
) => {
  const { locale } = useI18n()

  const { query, watch: watchOpts, default: defaultFn, headers: extraHeaders, ...restOptions } = options

  const resolvedUrl = computed(() => toValue(url))
  const resolvedQuery = computed(() => toValue(query) ?? {})

  const key = `api:${locale.value}:${toValue(url)}:${JSON.stringify(toValue(query) ?? {})}`

  // `watch: false` is not a list of extra sources, it is "do not watch anything" — and it has
  // to be handled before the spread rather than with `?? []`, which only catches null and
  // undefined. Spreading `false` throws "is not iterable" at runtime, so a caller passing the
  // documented value would have crashed the render.
  const watchSources: MultiWatchSources | false = watchOpts === false
    ? false
    : [locale, resolvedQuery, ...(watchOpts ?? [])]

  // The type arguments are spelled out as far as `_ResT` on purpose. Left to inference,
  // `useFetch` computes `_ResT` as `T extends void ? unknown : T`, a conditional it cannot
  // resolve while `T` is still a type parameter — so `default: () => T` matches neither
  // overload and the whole call falls back to an unresolved `DataT`. That is the failure this
  // module's note describes. Pinning `_ResT = T` lets both the option and the result reduce.
  return useFetch<T, FetchError, string, 'get', T, T, KeysOf<T>, T | undefined>(resolvedUrl, {
    ...restOptions,
    key,
    query: resolvedQuery,
    headers: computed(() => ({
      'x-locale': locale.value,
      ...(extraHeaders as Record<string, string> | undefined ?? {})
    })),
    default: defaultFn,
    watch: watchSources
  })
}

/**
 * SSR-aware API composable wrapping `useFetch` with locale-aware caching.
 *
 * Adds an `x-locale` header and a cache key carrying the active locale, so two languages do
 * not share one cached response, and re-fetches when the locale or the query changes.
 *
 * The two overloads differ only in whether `data` can be `undefined`, and `request` above
 * satisfies both: with a `default` factory `useFetch` seeds `data` before the first request
 * and restores it on failure, so the value is never absent, which is precisely what the first
 * overload states and what no signature TypeScript can infer will say on its own.
 */
export const useApi = request as UseApi

/**
 * One-shot API fetch for event handlers and Pinia stores.
 *
 * On 401 (session truly expired — server-side refresh already failed),
 * delegates to the global auth guard plugin to clear state and redirect.
 *
 * @param url - API endpoint URL
 * @param opts - $fetch options
 * @returns Parsed response data
 * @throws Whatever `$fetch` throws; a 401 is re-thrown after the auth guard has run.
 */
export const apiFetch = async <T = unknown>(
  url: string,
  opts: Parameters<typeof $fetch>[1] = {}
): Promise<T> => {
  let locale = 'en'
  try {
    locale = (useNuxtApp().$i18n as { locale: { value: string } }).locale.value
  }
  catch {
    // No Nuxt instance — a unit test, or a call from outside a render. `en` is the right answer.
  }

  try {
    return await $fetch<T>(url, {
      ...opts,
      headers: {
        'x-locale': locale,
        ...(opts.headers as Record<string, string> | undefined ?? {})
      }
    })
  }
  catch (err: unknown) {
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
