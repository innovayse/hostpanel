/**
 * The page title store: what the header of the screen on display announces to the top bar.
 *
 * A phone has no room for the breadcrumb, whose last step is a generic word ("Details", "List", "Edit"), so the top
 * bar names the page by the heading the screen itself shows. A screen whose header draws its own trail above the
 * title (a record's detail page: Clients, the client, Domains, the domain) owns the trail, and the top bar then draws
 * none from md up, so the same steps are never shown twice. `UiPageHeader` is the one writer: every screen and every
 * loading skeleton draws its heading through it, so the title is the schema's or the navigation's while the screen
 * downloads and the record's own once it has loaded, with no screen announcing anything by hand.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

/** One header on display, with what it announces. */
interface PageHeaderEntry {
  /** Identifies the header that announced itself, so only that header changes or removes its entry. */
  owner: symbol
  /** The heading text, already translated; absent while the header's title is still loading. */
  title?: string
  /** Whether the header draws the trail of the page itself. */
  trail: boolean
}

/**
 * Pinia store holding the headers on display, outermost header first.
 *
 * A screen has one header; the list only keeps two at once while a skeleton and its screen overlap, or when a screen
 * nests a second header, in which case the first one announced is the page's.
 */
export const usePageTitleStore = defineStore('pageTitle', () => {
  /** The headers on display, in the order they announced themselves. */
  const entries = ref<PageHeaderEntry[]>([])

  /** The title of the page on display: the first announced one; undefined when no header shows one. */
  const title = computed<string | undefined>(() => entries.value.find((entry) => entry.title !== undefined)?.title)

  /** Whether a header on display draws the page's trail, so the top bar leaves it out. */
  const ownsTrail = computed<boolean>(() => entries.value.some((entry) => entry.trail))

  /**
   * Whether a header other than the given one draws the page's trail, for a layout that draws its own only when none does.
   *
   * @param owner - The asking header or layout, as the symbol it announced itself with.
   * @returns True when another entry on display draws the trail.
   */
  const trailOwnedElsewhere = (owner: symbol): boolean => entries.value.some((entry) => entry.trail && entry.owner !== owner)

  /**
   * Withdraws a header's announcement, when the header goes away.
   *
   * @param owner - The header, as the symbol it announced itself with.
   */
  const release = (owner: symbol): void => {
    entries.value = entries.value.filter((entry) => entry.owner !== owner)
  }

  /**
   * Sets what a header shows, or withdraws it when it shows neither a title nor a trail (a title still loading).
   *
   * @param owner - The header, as the symbol it announces itself with.
   * @param text - The heading text, or undefined when there is nothing to show yet.
   * @param trail - Whether the header draws the page's trail; false when it draws none.
   */
  const announce = (owner: symbol, text: string | undefined, trail = false): void => {
    const heading = text === undefined || text === '' ? undefined : text
    const index = entries.value.findIndex((entry) => entry.owner === owner)
    if (heading === undefined && !trail) {
      release(owner)
    } else if (index === -1) {
      entries.value.push({ owner, title: heading, trail })
    } else {
      entries.value[index] = { owner, title: heading, trail }
    }
  }

  return { title, ownsTrail, trailOwnedElsewhere, announce, release }
})
