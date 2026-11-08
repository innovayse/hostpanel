/**
 * Navigation bar classes for the pre-rendered pages.
 *
 * The VitePress navigation bar sets its `has-sidebar`, `home` and `top` classes in a post effect, which
 * only runs in the browser. The pre-rendered HTML therefore always shows the bar as on a page without a
 * sidebar: the logo sits in a centred header, and after the scripts have run it jumps into the sidebar
 * column and moves the search box and the menu with it. The page content already carries the answer
 * (`VPContent has-sidebar` or `VPContent is-home`), so the same classes are written onto the bar while
 * the page is rendered. The browser then sets the same values again, and nothing moves.
 */

/** Matches the content element and captures its class list. */
const CONTENT_PATTERN = /<div class="VPContent([^"]*)"/

/** The opening tag of the navigation bar as VitePress renders it before hydration. */
const NAV_BAR_OPEN = '<div class="VPNavBar"'

/**
 * Works out the classes the navigation bar gets in the browser, from the classes of the page content.
 *
 * @param contentClasses - The class list that follows `VPContent`, for example ` has-sidebar`.
 * @returns The classes to add to the bar, with a leading space, or an empty string when there are none.
 */
export const navBarClassesFor = (contentClasses: string): string => {
  const names = contentClasses.split(/\s+/).filter((name) => name.length > 0)
  if (names.includes('has-sidebar')) {
    return ' has-sidebar'
  }
  if (names.includes('is-home')) {
    return ' home top'
  }
  return ''
}

/**
 * Writes the navigation bar classes of the browser into the HTML of a rendered page.
 *
 * @param html - The HTML of the page as VitePress rendered it.
 * @returns The HTML with the classes on the bar; the same string when the page has no bar or no content.
 */
export const withNavBarClasses = (html: string): string => {
  const content = CONTENT_PATTERN.exec(html)
  if (!content) {
    return html
  }
  const extra = navBarClassesFor(content[1])
  if (extra === '') {
    return html
  }
  return html.replace(NAV_BAR_OPEN, `<div class="VPNavBar${extra}"`)
}
