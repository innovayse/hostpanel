/**
 * The "Edit this page" link of the documentation.
 *
 * The link is a plain string pattern, never a function. VitePress writes the theme config into the page as data, and
 * a function in it is written as source text and rebuilt in the browser with `new Function`. A site that sends a
 * Content-Security-Policy without `'unsafe-eval'` (the public site does) refuses that, the data never loads and the
 * whole documentation app fails. `tests/editLink.test.ts` walks the real config and refuses any function in it.
 */
import { FALLBACK_DIR } from './i18n/locales'

/** Address of a page on GitHub; VitePress puts the path of the page's source file in place of `:path`. */
export const EDIT_LINK_PATTERN = 'https://github.com/innovayse/hostpanel/edit/main/docs/:path'

/**
 * The path the edit link points at. A placeholder page of a language that lacks the page is read from the folder the
 * build writes it to, which is not in the repository, so its link goes to the path where the translation is to be
 * created. Every other page keeps its real source path (`en/staff/orders.md`, `ru/staff/orders.md`).
 * @param filePath The page's source path, below the docs root.
 * @returns The path in the repository.
 */
export const repositoryPathOf = (filePath: string): string => {
  const prefix = `${FALLBACK_DIR}/`
  return filePath.startsWith(prefix) ? filePath.slice(prefix.length) : filePath
}
