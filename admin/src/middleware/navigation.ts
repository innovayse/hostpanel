/**
 * Router hooks that keep the navigation store in step with vue-router.
 *
 * Route records hold plain lazy loaders (the shell layout is eager), so the router keeps the old screen up until the chunk of
 * the next one has arrived. These hooks tell the shell about that wait, so `RoutedView` can answer a
 * click at once with a skeleton, and with the load error when the download fails for good.
 */
import type { RouteLocationNormalized } from 'vue-router'
import { pinia } from '@/pinia'
import { useNavigationStore } from '@/stores/navigation'
import { lazyLoadersOf } from '@/lib/lazyRoute'

/**
 * The last `beforeEach` guard: the navigation passed every check and now only loads its screens.
 *
 * Registered after the auth guard on purpose. A navigation that guard refuses or redirects never
 * reaches here, so the screen a visitor is sent away from is not replaced by a skeleton first.
 *
 * @param to - Target route being navigated to.
 */
export const navigationAdmitted = (to: RouteLocationNormalized): void => {
  useNavigationStore(pinia).admit(to, lazyLoadersOf(to).length > 0)
}

/**
 * The `afterEach` hook: the navigation arrived, or stopped short without an error (cancelled by a
 * newer one, aborted or refused). The pending state ends either way.
 *
 * @param to - Target route the navigation was heading for.
 */
export const navigationSettled = (to: RouteLocationNormalized): void => {
  useNavigationStore(pinia).settle(to)
}

/**
 * The `onError` hook: the navigation threw, typically because a chunk never arrived after the repeats.
 *
 * @param _error - What the navigation threw; the screen shows its own sentence, not this error.
 * @param to - Target route the navigation was heading for.
 */
export const navigationFailed = (_error: unknown, to: RouteLocationNormalized): void => {
  useNavigationStore(pinia).fail(to)
}
