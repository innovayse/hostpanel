/**
 * Prefetch-on-hover for every in-app link.
 *
 * One delegated listener on the document watches the pointer, keyboard focus and touch, and when
 * they land on a link that points inside the app it asks the router for the target and starts
 * downloading that screen's chunks. Delegating means the sidebar, tables, cards and breadcrumbs all
 * get it with no per-link code, and no component has to know it exists.
 */
import { onBeforeUnmount, onMounted } from 'vue'
import type { Router } from 'vue-router'
import { anchorOf } from '@/lib/anchor'
import { prefetchRoute } from '@/lib/prefetchRoute'

/** The events that announce the operator is about to use a link, in the order they usually fire. */
const INTENT_EVENTS = ['pointerover', 'focusin', 'touchstart'] as const

/**
 * Finds the router location a link element leads to.
 *
 * @param anchor - The anchor under the pointer.
 * @param router - The application router, for its base path and resolver.
 * @returns The in-app path with query and hash, or null for an external, download or non-navigating link.
 */
export const inAppPathOf = (anchor: HTMLAnchorElement, router: Router): string | null => {
  const attribute = anchor.getAttribute('href')
  if (attribute === null || attribute.startsWith('#') || anchor.target === '_blank' || anchor.hasAttribute('download')) {
    return null
  }
  const url = new URL(anchor.href, window.location.href)
  if (url.origin !== window.location.origin) {
    return null
  }
  const base = router.options.history.base
  if (base !== '' && url.pathname !== base && !url.pathname.startsWith(`${base}/`)) {
    return null
  }
  return `${url.pathname.slice(base.length) || '/'}${url.search}${url.hash}`
}

/**
 * Installs the delegated prefetch listener while the calling component is mounted.
 *
 * @param router - The application router.
 */
export const useLinkPrefetch = (router: Router): void => {
  /**
   * Prefetches the screen behind the link an event landed on.
   *
   * @param event - A pointer, focus or touch event.
   */
  const onIntent = (event: Event): void => {
    const anchor = anchorOf(event.target)
    if (anchor === null) {
      return
    }
    const path = inAppPathOf(anchor, router)
    if (path !== null) {
      prefetchRoute(router.resolve(path))
    }
  }

  onMounted(() => {
    INTENT_EVENTS.forEach((name) => document.addEventListener(name, onIntent, { passive: true }))
  })
  onBeforeUnmount(() => {
    INTENT_EVENTS.forEach((name) => document.removeEventListener(name, onIntent))
  })
}
