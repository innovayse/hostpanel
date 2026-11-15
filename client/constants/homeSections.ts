/**
 * Anchors into the home page, in one place so the section that carries an id and the links that
 * point at it cannot drift apart.
 *
 * @module constants/homeSections
 */

import { SITE_HOME_PATH } from '~/constants/siteRoutes'
import { HomeSectionId } from '~/types/homesectionid'

/**
 * Space kept above a home-page section when the browser scrolls it into view, so its heading is
 * not flush against the top edge (or under a pinned header). The class is a complete Tailwind
 * name; the router reads the resulting `scroll-margin-top` when it follows a hash.
 */
export const HOME_SECTION_SCROLL_MARGIN_CLASS = 'scroll-mt-24'

/**
 * The hash that selects a home-page section.
 *
 * @param id - The section.
 * @returns `#<id>`.
 */
export const homeSectionHash = (id: HomeSectionId): string => `#${id}`

/**
 * The home-page address of a section, from any page.
 *
 * @param localePath - The `useLocalePath()` function of the caller.
 * @param id - The section.
 * @returns The localised home path followed by the section hash.
 */
export const homeSectionPath = (localePath: (path: string) => string, id: HomeSectionId): string =>
  `${localePath(SITE_HOME_PATH)}${homeSectionHash(id)}`

/**
 * The section a hash selects, when it names one.
 *
 * @param hash - A route hash such as `#process`, or empty.
 * @returns The matching section, or `undefined`.
 */
export const homeSectionFromHash = (hash: string): HomeSectionId | undefined =>
  Object.values(HomeSectionId).find(id => homeSectionHash(id) === hash)
