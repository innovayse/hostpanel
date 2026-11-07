/**
 * Sidebar section icons for the documentation site.
 * VitePress renders a sidebar item's text as HTML, so a section title can carry a small decorative
 * span. The span only holds a class: the glyph itself is drawn by `theme/custom.css` with a mask,
 * so it takes the text colour and follows the theme, hover and active states. Only top level
 * section titles use it; nothing else reads those titles as plain text.
 */

/** The icons that exist in `theme/custom.css`, one per top level sidebar section. */
export type SidebarIcon =
  | 'rocket'
  | 'wrench'
  | 'briefcase'
  | 'users'
  | 'credit-card'
  | 'server'
  | 'arrow-left-right'
  | 'code'
  | 'git-pull-request'
  | 'book-open'
  | 'key-round'
  | 'globe'
  | 'user'
  | 'user-check'
  | 'shield-check'

/**
 * Escapes the characters that are special in HTML, so a title such as "Servers & SSH" stays valid.
 * @param value Plain title text.
 * @returns The same text, safe to place inside markup.
 */
export const escapeHtml =(value: string): string => {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/**
 * Builds the sidebar title of a section: a decorative icon span followed by the title.
 * @param icon Name of the icon, matching a `.hp-sidebar-icon--<name>` rule in `theme/custom.css`.
 * @param text Plain section title.
 * @returns HTML for the sidebar `text` field.
 */
export const sidebarSectionText = (icon: SidebarIcon, text: string): string => {
  return `<span class="hp-sidebar-icon hp-sidebar-icon--${icon}" aria-hidden="true"></span>${escapeHtml(text)}`
}
