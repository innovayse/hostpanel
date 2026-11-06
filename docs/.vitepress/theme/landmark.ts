/**
 * The main landmark of the documentation home page.
 *
 * Every other page of the default VitePress theme draws its content inside a `<main>` element, but the
 * home layout (`layout: home`) draws it inside plain `div` elements, so a reader of a screen reader has
 * no landmark to jump to. The home content is marked `role="main"` here instead. The mark sits on the
 * element of the home layout, which VitePress removes when the reader opens another page, so a page
 * with its own `<main>` never ends up with two.
 */

/** An element the helper can mark. */
export interface MarkableElement {
  /** Sets an attribute of the element. */
  setAttribute: (name: string, value: string) => void
}

/** The part of the document the helper reads. */
export interface LandmarkRoot {
  /** Finds the first element that matches a CSS selector, or `null`. */
  querySelector: (selector: string) => MarkableElement | null
}

/** Selector of the element the home layout draws its content in. */
export const HOME_SELECTOR = '.VPHome'

/**
 * Marks the content of the home layout as the main landmark.
 *
 * @param root - The document to search.
 * @param isHome - Whether the page on screen uses the home layout.
 * @returns `true` when the home content was marked, `false` when the page is not the home page or the
 * home content is not on screen yet.
 */
export function markHomeAsMain(root: LandmarkRoot, isHome: boolean): boolean {
  if (!isHome) {
    return false
  }
  const home = root.querySelector(HOME_SELECTOR)
  if (!home) {
    return false
  }
  home.setAttribute('role', 'main')
  return true
}
