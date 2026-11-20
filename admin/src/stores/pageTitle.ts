/**
 * The page title store: the heading of the screen on display, for the top bar of a phone.
 *
 * A phone has no room for the breadcrumb, whose last step is a generic word ("Details", "List", "Edit"), so the top
 * bar names the page by the heading the screen itself shows. `UiPageHeader` is the one writer: every screen and
 * every loading skeleton draws its heading through it, so the title is the schema's or the navigation's while the
 * screen downloads and the record's own once it has loaded, with no screen announcing anything by hand.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

/** One heading on display and the header that draws it. */
interface PageTitleEntry {
  /** Identifies the header that announced the title, so only that header changes or removes it. */
  owner: symbol
  /** The heading text, already translated. */
  title: string
}

/**
 * Pinia store holding the headings on display, outermost header first.
 *
 * A screen has one header; the list only keeps two at once while a skeleton and its screen overlap, or when a screen
 * nests a second header, in which case the first one announced is the page's.
 */
export const usePageTitleStore = defineStore('pageTitle', () => {
  /** The headings on display, in the order their headers announced them. */
  const entries = ref<PageTitleEntry[]>([])

  /** The title of the page on display; undefined when no header shows one. */
  const title = computed<string | undefined>(() => entries.value[0]?.title)

  /**
   * Withdraws a header's heading, when the header goes away.
   *
   * @param owner - The header, as the symbol it announced itself with.
   */
  const release = (owner: symbol): void => {
    entries.value = entries.value.filter((entry) => entry.owner !== owner)
  }

  /**
   * Sets the heading a header shows, or withdraws it when the header shows none (a title still loading).
   *
   * @param owner - The header, as the symbol it announces itself with.
   * @param text - The heading text, or undefined when there is nothing to show yet.
   */
  const announce = (owner: symbol, text: string | undefined): void => {
    const index = entries.value.findIndex((entry) => entry.owner === owner)
    if (text === undefined || text === '') {
      release(owner)
    } else if (index === -1) {
      entries.value.push({ owner, title: text })
    } else {
      entries.value[index] = { owner, title: text }
    }
  }

  return { title, announce, release }
})
