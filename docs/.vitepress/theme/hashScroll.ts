/**
 * Scrolling to the heading a link points at, on the first load and after the layout has settled.
 *
 * VitePress scrolls to the `#hash` once, before the page is hydrated. The header, the sidebar, the
 * outline and the fonts all arrive after that, so on a slow connection the heading can end up in the wrong
 * place, or the page can stay at the top. These helpers scroll again at the moments the layout changes,
 * until the reader scrolls on their own. Where the heading stops is set by the `scroll-margin-top` of the
 * headings in `custom.css`, so a link click, the browser's own jump and this scroll all agree.
 */

/** An element that can be scrolled into view. */
export interface ScrollTarget {
  /** Scrolls the element to the top of the viewport, below its scroll margin. */
  scrollIntoView: (options: { block: 'start'; behavior: 'instant' }) => void
}

/** The part of the document the helpers read. */
export interface HashScrollRoot {
  /** Finds an element by its id, or `null`. */
  getElementById: (id: string) => ScrollTarget | null
}

/**
 * Reads the element id out of a URL hash.
 *
 * @param hash - The hash including the leading `#`, possibly percent encoded.
 * @returns The decoded id, or `null` when the hash is empty or cannot be decoded.
 */
export const targetIdOf = (hash: string): string | null => {
  if (hash.length < 2 || !hash.startsWith('#')) {
    return null
  }
  try {
    return decodeURIComponent(hash.slice(1))
  } catch {
    return null
  }
}

/**
 * Scrolls the element a hash points at into view.
 *
 * @param root - The document to search.
 * @param hash - The hash including the leading `#`.
 * @returns `true` when the element was found and scrolled to.
 */
export const scrollToHash = (root: HashScrollRoot, hash: string): boolean => {
  const id = targetIdOf(hash)
  if (id === null) {
    return false
  }
  const target = root.getElementById(id)
  if (!target) {
    return false
  }
  target.scrollIntoView({ block: 'start', behavior: 'instant' })
  return true
}

/** Repeats the scroll to the hash target until it is cancelled. */
export interface HashSettler {
  /** Scrolls to the target again, unless the settler has been cancelled. */
  run: () => boolean
  /** Stops all further scrolling; called when the reader scrolls or the settling time is over. */
  cancel: () => void
}

/**
 * Creates the settler that the layout-change moments (mount, fonts ready, load) call.
 *
 * @param scroll - Scrolls to the current hash target and reports whether it was found.
 * @returns The settler.
 */
export const createHashSettler = (scroll: () => boolean): HashSettler => {
  let cancelled = false
  return {
    run: () => {
      if (cancelled) {
        return false
      }
      return scroll()
    },
    cancel: () => {
      cancelled = true
    },
  }
}
