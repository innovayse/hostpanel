/**
 * Type sizes of links, declared once so no two screens draw the same kind of link in two sizes.
 *
 * The rule: a navigation entry is 14px, a sub-menu entry 13px, a card action link ("View all") 13px
 * (`UiLink` `size="sm"`), and a link inside running text inherits the body size (14px) and sets none.
 */

/** Type of a navigation entry (sidebar, section sidebar): 14px, medium weight. */
export const navItemTextClasses = 'text-sm font-medium'

/** Type of a sub-menu entry under a navigation entry: 13px, medium weight. */
export const navSubItemTextClasses = 'text-[0.8125rem] font-medium'
