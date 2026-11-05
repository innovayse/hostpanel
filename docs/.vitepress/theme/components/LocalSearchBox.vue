<!--
  Hostpanel override of the VitePress local search box.

  Based on: VitePress 1.6.4, `src/client/theme-default/components/VPLocalSearchBox.vue`. The
  version is pinned exactly in `docs/package.json` because this file is a copy: raising VitePress
  means diffing this file against the new original first.

  Wired in by `resolve.alias` in `docs/.vitepress/config.ts`, the documented way to override an
  internal theme component.

  What changed from the original:
  - The relative imports of the theme sources became package imports (`vitepress/dist/client/...`,
    public through the package `exports`), and `useData` comes from `vitepress`.
  - With an empty query the box lists two groups instead of nothing: "Recently viewed" (the last
    five pages read, see `../recentPages.ts`) and "Suggested". Both groups form ONE list for the
    arrow keys, Enter and the footer hints. Their logic lives in `../recentPages.ts`.
  - Selection, key handling and the list ids read the active list (start rows when the query is
    empty, search results otherwise) through `activeCount`.
  Everything about typed queries (index, search options, excerpts, highlighting, detailed view)
  is unchanged.
-->
<script lang="ts" setup>
/**
 * Search box with a start view. Search behaviour is the stock one; the additions are the
 * recently viewed and suggested rows shown while the query is empty.
 */
import localSearchIndex from '@localSearchIndex'
import {
  computedAsync,
  debouncedWatch,
  onKeyStroke,
  useEventListener,
  useLocalStorage,
  useScrollLock,
  useSessionStorage
} from '@vueuse/core'
import { useFocusTrap } from '@vueuse/integrations/useFocusTrap'
import Mark from 'mark.js/src/vanilla.js'
import MiniSearch, { type SearchResult } from 'minisearch'
import { dataSymbol, inBrowser, useData, useRouter, withBase } from 'vitepress'
import { pathToFile } from 'vitepress/dist/client/app/utils.js'
import { escapeRegExp } from 'vitepress/dist/client/shared.js'
import { LRUCache } from 'vitepress/dist/client/theme-default/support/lru.js'
import { createSearchTranslate } from 'vitepress/dist/client/theme-default/support/translation.js'
import {
  computed,
  createApp,
  markRaw,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  shallowRef,
  watch,
  watchEffect,
  type Ref
} from 'vue'
import { buildHomeGroups, buildPageCatalog, getRecentTracker } from '../recentPages'
import type { HomeGroup, SidebarConfig } from '../recentPages'
import HpSearchIcon from './HpSearchIcon.vue'

/** Emits `close` when the box should be dismissed. */
const emit = defineEmits<{
  (e: 'close'): void
}>()

/** Root element of the box; the focus trap works on it. */
const el = shallowRef<HTMLElement>()
/** The list element; highlighting runs inside it. */
const resultsEl = shallowRef<HTMLElement>()

/* Search */

/** Loaders of the search index, one per locale. */
const searchIndexData = shallowRef(localSearchIndex)

// hmr
if (import.meta.hot) {
  import.meta.hot.accept('/@localSearchIndex', (m) => {
    if (m) {
      searchIndexData.value = m.default
    }
  })
}

interface Result {
  title: string
  titles: string[]
  text?: string
}

/** Texts of the modal that the theme options may translate; the two group titles are added here. */
interface ModalTranslations {
  displayDetails?: string
  resetButtonTitle?: string
  backButtonTitle?: string
  noResultsText?: string
  recentTitle?: string
  suggestedTitle?: string
  footer?: {
    selectText?: string
    selectKeyAriaLabel?: string
    navigateText?: string
    navigateUpKeyAriaLabel?: string
    navigateDownKeyAriaLabel?: string
    closeText?: string
    closeKeyAriaLabel?: string
  }
}

/** Site and theme data of the current page. */
const vitePressData = useData()
/** Keeps keyboard focus inside the box while it is open. */
const { activate } = useFocusTrap(el, {
  immediate: true,
  allowOutsideClick: true,
  clickOutsideDeactivates: true,
  escapeDeactivates: true
})
/** Active locale and theme options. */
const { localeIndex, theme } = vitePressData
/** The search index of the active locale, loaded on demand. */
const searchIndex = computedAsync(async () =>
  markRaw(
    MiniSearch.loadJSON<Result>(
      (await searchIndexData.value[localeIndex.value]?.())?.default,
      {
        fields: ['title', 'titles', 'text'],
        storeFields: ['title', 'titles'],
        searchOptions: {
          fuzzy: 0.2,
          prefix: true,
          boost: { title: 4, text: 2, titles: 1 },
          ...(theme.value.search?.provider === 'local' &&
            theme.value.search.options?.miniSearch?.searchOptions)
        },
        ...(theme.value.search?.provider === 'local' &&
          theme.value.search.options?.miniSearch?.options)
      }
    )
  )
)

/** True when the theme asks not to remember the query. */
const disableQueryPersistence = computed(() => {
  return (
    theme.value.search?.provider === 'local' &&
    theme.value.search.options?.disableQueryPersistence === true
  )
})

/** The query; kept in session storage unless persistence is disabled. */
const filterText = disableQueryPersistence.value
  ? ref('')
  : useSessionStorage('vitepress:local-search-filter', '')

/** Whether results show an excerpt. */
const showDetailedList = useLocalStorage(
  'vitepress:local-search-detailed-list',
  theme.value.search?.provider === 'local' &&
    theme.value.search.options?.detailedView === true
)

/** True when the theme turns the detailed view off. */
const disableDetailedView = computed(() => {
  return (
    theme.value.search?.provider === 'local' &&
    (theme.value.search.options?.disableDetailedView === true ||
      theme.value.search.options?.detailedView === false)
  )
})

/** Text of the search button, also the input placeholder. */
const buttonText = computed(() => {
  const options = theme.value.search?.options ?? theme.value.algolia

  return (
    options?.locales?.[localeIndex.value]?.translations?.button?.buttonText ||
    options?.translations?.button?.buttonText ||
    'Search'
  )
})

watchEffect(() => {
  if (disableDetailedView.value) {
    showDetailedList.value = false
  }
})

/** Current search results (empty while the query is empty). */
const results: Ref<(SearchResult & Result)[]> = shallowRef([])

/** Whether the "no results" line may show for the current query. */
const enableNoResults = ref(false)

watch(filterText, () => {
  enableNoResults.value = false
})

/** Highlighter bound to the list element. */
const mark = computedAsync(async () => {
  if (!resultsEl.value) {
    return
  }
  return markRaw(new Mark(resultsEl.value))
}, null)

/** Excerpts of recently opened pages. */
const cache = new LRUCache<string, Map<string, string>>(16) // 16 files

debouncedWatch(
  () => [searchIndex.value, filterText.value, showDetailedList.value] as const,
  async ([index, filterTextValue, showDetailedListValue], old, onCleanup) => {
    if (old?.[0] !== index) {
      // in case of hmr
      cache.clear()
    }

    let canceled = false
    onCleanup(() => {
      canceled = true
    })

    if (!index) {
      return
    }

    // Search
    results.value = index
      .search(filterTextValue)
      .slice(0, 16) as (SearchResult & Result)[]
    enableNoResults.value = true

    // Highlighting
    const mods = showDetailedListValue
      ? await Promise.all(results.value.map((r) => fetchExcerpt(r.id)))
      : []
    if (canceled) {
      return
    }
    for (const { id, mod } of mods) {
      const mapId = id.slice(0, id.indexOf('#'))
      let map = cache.get(mapId)
      if (map) {
        continue
      }
      map = new Map()
      cache.set(mapId, map)
      const comp = mod.default ?? mod
      if (comp?.render || comp?.setup) {
        const app = createApp(comp)
        // Silence warnings about missing components
        app.config.warnHandler = () => {}
        app.provide(dataSymbol, vitePressData)
        Object.defineProperties(app.config.globalProperties, {
          $frontmatter: {
            get() {
              return vitePressData.frontmatter.value
            }
          },
          $params: {
            get() {
              return vitePressData.page.value.params
            }
          }
        })
        const div = document.createElement('div')
        app.mount(div)
        const headings = div.querySelectorAll('h1, h2, h3, h4, h5, h6')
        headings.forEach((el) => {
          const href = el.querySelector('a')?.getAttribute('href')
          const anchor = href?.startsWith('#') && href.slice(1)
          if (!anchor) {
            return
          }
          let html = ''
          // The sibling walk ends at the next heading, so the non-null assertion cannot be hit at the end.
          while ((el = el.nextElementSibling!) && !/^h[1-6]$/i.test(el.tagName)) {
            html += el.outerHTML
          }
          map!.set(anchor, html)
        })
        app.unmount()
      }
      if (canceled) {
        return
      }
    }

    const terms = new Set<string>()

    results.value = results.value.map((r) => {
      const [id, anchor] = r.id.split('#')
      const map = cache.get(id)
      const text = map?.get(anchor) ?? ''
      for (const term in r.match) {
        terms.add(term)
      }
      return { ...r, text }
    })

    await nextTick()
    if (canceled) {
      return
    }

    await new Promise((r) => {
      mark.value?.unmark({
        done: () => {
          mark.value?.markRegExp(formMarkRegex(terms), { done: r })
        }
      })
    })

    const excerpts = el.value?.querySelectorAll('.result .excerpt') ?? []
    for (const excerpt of excerpts) {
      excerpt
        .querySelector('mark[data-markjs="true"]')
        ?.scrollIntoView({ block: 'center' })
    }
    // FIXME: without this whole page scrolls to the bottom
    resultsEl.value?.firstElementChild?.scrollIntoView({ block: 'start' })
  },
  { debounce: 200, immediate: true }
)

/** Loads the compiled page module of a result so its excerpt can be shown. */
const fetchExcerpt = async (id: string) => {
  const file = pathToFile(id.slice(0, id.indexOf('#')))
  try {
    if (!file) {
      throw new Error(`Cannot find file for id: ${id}`)
    }
    return { id, mod: await import(/*@vite-ignore*/ file) }
  } catch (e) {
    console.error(e)
    return { id, mod: {} }
  }
}

/* Search input focus */

/** The search input element. */
const searchInput = ref<HTMLInputElement>()
/** True when there is nothing to clear. */
const disableReset = computed(() => {
  return filterText.value?.length <= 0
})
/** Focuses the input and, by default, selects its text. */
const focusSearchInput = (select = true) => {
  searchInput.value?.focus()
  if (select) {
    searchInput.value?.select()
  }
}

onMounted(() => {
  focusSearchInput()
})

/** Focuses the input on a mouse click on the bar. */
const onSearchBarClick = (event: PointerEvent) => {
  if (event.pointerType === 'mouse') {
    focusSearchInput()
  }
}

/* Search keyboard selection */

/** Index of the highlighted row in the active list, or -1. */
const selectedIndex = ref(-1)
/** True while the keyboard moves the highlight, so a resting pointer cannot steal it. */
const disableMouseOver = ref(true)

/* Start view: shown while the query is empty */

/** Pages that exist, read from the sidebar; the source of titles, sections and validity. */
const pageCatalog = computed(() =>
  buildPageCatalog(theme.value.sidebar as SidebarConfig | undefined)
)

/** True while the query is empty, which is when the start view replaces the results. */
const isHome = computed(() => filterText.value.length === 0)

/** The start view groups; starts with the suggested group and gains the recent one on mount. */
const homeGroups = shallowRef<HomeGroup[]>(buildHomeGroups([], pageCatalog.value))

/** Every start view row in one list, so the keys walk both groups as one. */
const homeItems = computed(() => homeGroups.value.flatMap((group) => group.items))

/** Rows in the list the user is moving through: start view rows or search results. */
const activeCount = computed(() =>
  isHome.value ? homeItems.value.length : results.value.length
)

/**
 * Reads the recently viewed pages again. Storage is read here, in the browser, when the box opens.
 */
const refreshHome = () => {
  homeGroups.value = buildHomeGroups(
    getRecentTracker().read(pageCatalog.value),
    pageCatalog.value
  )
}

onMounted(refreshHome)

watch(results, (r) => {
  if (isHome.value) {
    return
  }
  selectedIndex.value = r.length ? 0 : -1
  scrollToSelectedResult()
})

watch(
  [isHome, homeItems],
  () => {
    if (!isHome.value) {
      return
    }
    selectedIndex.value = homeItems.value.length ? 0 : -1
    scrollToSelectedResult()
  }
)

/** Scrolls the highlighted row into view. */
const scrollToSelectedResult = () => {
  nextTick(() => {
    const selectedEl = document.querySelector('.result.selected')
    selectedEl?.scrollIntoView({ block: 'nearest' })
  })
}

onKeyStroke('ArrowUp', (event) => {
  event.preventDefault()
  selectedIndex.value--
  if (selectedIndex.value < 0) {
    selectedIndex.value = activeCount.value - 1
  }
  disableMouseOver.value = true
  scrollToSelectedResult()
})

onKeyStroke('ArrowDown', (event) => {
  event.preventDefault()
  selectedIndex.value++
  if (selectedIndex.value >= activeCount.value) {
    selectedIndex.value = 0
  }
  disableMouseOver.value = true
  scrollToSelectedResult()
})

/** The VitePress router, used to open the chosen page. */
const router = useRouter()

onKeyStroke('Enter', (e) => {
  if (e.isComposing) {
    return
  }

  if (e.target instanceof HTMLButtonElement && e.target.type !== 'submit') {
    return
  }

  if (isHome.value) {
    const selectedItem = homeItems.value[selectedIndex.value]
    if (e.target instanceof HTMLInputElement && !selectedItem) {
      e.preventDefault()
      return
    }

    if (selectedItem) {
      router.go(withBase(selectedItem.path))
      emit('close')
    }
    return
  }

  const selectedPackage = results.value[selectedIndex.value]
  if (e.target instanceof HTMLInputElement && !selectedPackage) {
    e.preventDefault()
    return
  }

  if (selectedPackage) {
    router.go(selectedPackage.id)
    emit('close')
  }
})

onKeyStroke('Escape', () => {
  emit('close')
})

// Translations
/** English texts, overridden by the theme options. */
const defaultTranslations: { modal: ModalTranslations } = {
  modal: {
    recentTitle: 'Recently viewed',
    suggestedTitle: 'Suggested',
    displayDetails: 'Display detailed list',
    resetButtonTitle: 'Reset search',
    backButtonTitle: 'Close search',
    noResultsText: 'No results for',
    footer: {
      selectText: 'to select',
      selectKeyAriaLabel: 'enter',
      navigateText: 'to navigate',
      navigateUpKeyAriaLabel: 'up arrow',
      navigateDownKeyAriaLabel: 'down arrow',
      closeText: 'to close',
      closeKeyAriaLabel: 'escape'
    }
  }
}

/** Looks a text up in the theme translations, then in the defaults. */
const translate = createSearchTranslate(defaultTranslations)

// Back

onMounted(() => {
  // Prevents going to previous site
  window.history.pushState(null, '', null)
})

useEventListener('popstate', (event) => {
  event.preventDefault()
  emit('close')
})

/** Lock body */
const isLocked = useScrollLock(inBrowser ? document.body : null)

onMounted(() => {
  nextTick(() => {
    isLocked.value = true
    nextTick().then(() => activate())
  })
})

onBeforeUnmount(() => {
  isLocked.value = false
})

/** Clears the query and puts focus back in the input. */
const resetSearch = () => {
  filterText.value = ''
  nextTick().then(() => focusSearchInput(false))
}

/** Builds the pattern that highlights the matched terms. */
const formMarkRegex = (terms: Set<string>) => {
  return new RegExp(
    [...terms]
      .sort((a, b) => b.length - a.length)
      .map((term) => `(${escapeRegExp(term)})`)
      .join('|'),
    'gi'
  )
}

/** Moves the highlight to the row under the pointer. */
const onMouseMove = (e: MouseEvent) => {
  if (!disableMouseOver.value) {
    return
  }
  const el = (e.target as HTMLElement)?.closest<HTMLAnchorElement>('.result')
  const index = Number.parseInt(el?.dataset.index!)
  if (index >= 0 && index !== selectedIndex.value) {
    selectedIndex.value = index
  }
  disableMouseOver.value = false
}
</script>

<template>
  <Teleport to="body">
    <div
      ref="el"
      role="button"
      :aria-owns="activeCount ? 'localsearch-list' : undefined"
      aria-expanded="true"
      aria-haspopup="listbox"
      aria-labelledby="localsearch-label"
      class="VPLocalSearchBox"
    >
      <div class="backdrop" @click="$emit('close')" />

      <div class="shell">
        <form
          class="search-bar"
          @pointerup="onSearchBarClick($event)"
          @submit.prevent=""
        >
          <label
            :title="buttonText"
            id="localsearch-label"
            for="localsearch-input"
          >
            <span aria-hidden="true" class="vpi-search search-icon local-search-icon" />
          </label>
          <div class="search-actions before">
            <button
              class="back-button"
              :title="translate('modal.backButtonTitle')"
              @click="$emit('close')"
            >
              <span class="vpi-arrow-left local-search-icon" />
            </button>
          </div>
          <input
            ref="searchInput"
            v-model="filterText"
            :aria-activedescendant="selectedIndex > -1 ? ('localsearch-item-' + selectedIndex) : undefined"
            aria-autocomplete="both"
            :aria-controls="activeCount ? 'localsearch-list' : undefined"
            aria-labelledby="localsearch-label"
            autocapitalize="off"
            autocomplete="off"
            autocorrect="off"
            class="search-input"
            id="localsearch-input"
            enterkeyhint="go"
            maxlength="64"
            :placeholder="buttonText"
            spellcheck="false"
            type="search"
          />
          <div class="search-actions">
            <button
              v-if="!disableDetailedView"
              class="toggle-layout-button"
              type="button"
              :class="{ 'detailed-list': showDetailedList }"
              :title="translate('modal.displayDetails')"
              @click="
                selectedIndex > -1 && (showDetailedList = !showDetailedList)
              "
            >
              <span class="vpi-layout-list local-search-icon" />
            </button>

            <button
              class="clear-button"
              type="reset"
              :disabled="disableReset"
              :title="translate('modal.resetButtonTitle')"
              @click="resetSearch"
            >
              <span class="vpi-delete local-search-icon" />
            </button>
          </div>
        </form>

        <ul
          ref="resultsEl"
          :id="activeCount ? 'localsearch-list' : undefined"
          :role="activeCount ? 'listbox' : undefined"
          :aria-labelledby="activeCount ? 'localsearch-label' : undefined"
          class="results"
          @mousemove="onMouseMove"
        >
          <template v-if="isHome">
            <template v-for="group in homeGroups" :key="group.id">
              <li class="group-title" role="presentation">
                {{ translate(group.id === 'recent' ? 'modal.recentTitle' : 'modal.suggestedTitle') }}
              </li>
              <li
                v-for="item in group.items"
                :key="group.id + item.path"
                :id="'localsearch-item-' + item.index"
                :aria-selected="selectedIndex === item.index ? 'true' : 'false'"
                role="option"
              >
                <a
                  :href="withBase(item.path)"
                  class="result home-result"
                  :class="{ selected: selectedIndex === item.index }"
                  :aria-label="item.section ? item.section + ' > ' + item.title : item.title"
                  @mouseenter="!disableMouseOver && (selectedIndex = item.index)"
                  @focusin="selectedIndex = item.index"
                  @click="$emit('close')"
                  :data-index="item.index"
                >
                  <span v-if="group.id === 'recent'" class="home-icon page-icon">
                    <HpSearchIcon name="file-text" />
                  </span>
                  <span
                    v-else-if="item.icon"
                    class="home-icon"
                    :class="{
                      'tone-blue': item.tone === 'blue',
                      'tone-green': item.tone === 'green',
                      'tone-amber': item.tone === 'amber',
                      'tone-violet': item.tone === 'violet'
                    }"
                  >
                    <HpSearchIcon :name="item.icon" />
                  </span>
                  <span class="home-text">
                    <span class="home-title">{{ item.title }}</span>
                    <span v-if="item.section" class="home-crumb">
                      {{ item.section }} › {{ item.title }}
                    </span>
                  </span>
                  <span v-if="group.id === 'recent'" class="home-chevron">
                    <HpSearchIcon name="chevron-right" />
                  </span>
                </a>
              </li>
            </template>
          </template>
          <template v-else>
          <li
            v-for="(p, index) in results"
            :key="p.id"
            :id="'localsearch-item-' + index"
            :aria-selected="selectedIndex === index ? 'true' : 'false'"
            role="option"
          >
            <a
              :href="p.id"
              class="result"
              :class="{
                selected: selectedIndex === index
              }"
              :aria-label="[...p.titles, p.title].join(' > ')"
              @mouseenter="!disableMouseOver && (selectedIndex = index)"
              @focusin="selectedIndex = index"
              @click="$emit('close')"
              :data-index="index"
            >
              <div>
                <div class="titles">
                  <span class="title-icon">#</span>
                  <span
                    v-for="(t, index) in p.titles"
                    :key="index"
                    class="title"
                  >
                    <span class="text" v-html="t" />
                    <span class="vpi-chevron-right local-search-icon" />
                  </span>
                  <span class="title main">
                    <span class="text" v-html="p.title" />
                  </span>
                </div>

                <div v-if="showDetailedList" class="excerpt-wrapper">
                  <div v-if="p.text" class="excerpt" inert>
                    <div class="vp-doc" v-html="p.text" />
                  </div>
                  <div class="excerpt-gradient-bottom" />
                  <div class="excerpt-gradient-top" />
                </div>
              </div>
            </a>
          </li>
          <li
            v-if="filterText && !results.length && enableNoResults"
            class="no-results"
          >
            {{ translate('modal.noResultsText') }} "<strong>{{ filterText }}</strong
            >"
          </li>
          </template>
        </ul>

        <div class="search-keyboard-shortcuts">
          <span>
            <kbd :aria-label="translate('modal.footer.navigateUpKeyAriaLabel')">
              <span class="vpi-arrow-up navigate-icon" />
            </kbd>
            <kbd :aria-label="translate('modal.footer.navigateDownKeyAriaLabel')">
              <span class="vpi-arrow-down navigate-icon" />
            </kbd>
            {{ translate('modal.footer.navigateText') }}
          </span>
          <span>
            <kbd :aria-label="translate('modal.footer.selectKeyAriaLabel')">
              <span class="vpi-corner-down-left navigate-icon" />
            </kbd>
            {{ translate('modal.footer.selectText') }}
          </span>
          <span>
            <kbd :aria-label="translate('modal.footer.closeKeyAriaLabel')">esc</kbd>
            {{ translate('modal.footer.closeText') }}
          </span>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.VPLocalSearchBox {
  position: fixed;
  z-index: 100;
  inset: 0;
  display: flex;
}

.backdrop {
  position: absolute;
  inset: 0;
  background: var(--vp-backdrop-bg-color);
  transition: opacity 0.5s;
}

.shell {
  position: relative;
  padding: 12px;
  margin: 64px auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: var(--vp-local-search-bg);
  width: min(100vw - 60px, 900px);
  height: min-content;
  max-height: min(100vh - 128px, 900px);
  border-radius: 6px;
}

@media (max-width: 767px) {
  .shell {
    margin: 0;
    width: 100vw;
    height: 100vh;
    max-height: none;
    border-radius: 0;
  }
}

.search-bar {
  border: 1px solid var(--vp-c-divider);
  border-radius: 4px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  cursor: text;
}

@media (max-width: 767px) {
  .search-bar {
    padding: 0 8px;
  }
}

.search-bar:focus-within {
  border-color: var(--vp-c-brand-1);
}

.local-search-icon {
  display: block;
  font-size: 18px;
}

.navigate-icon {
  display: block;
  font-size: 14px;
}

.search-icon {
  margin: 8px;
}

@media (max-width: 767px) {
  .search-icon {
    display: none;
  }
}

.search-input {
  padding: 6px 12px;
  font-size: inherit;
  width: 100%;
}

@media (max-width: 767px) {
  .search-input {
    padding: 6px 4px;
  }
}

.search-actions {
  display: flex;
  gap: 4px;
}

@media (any-pointer: coarse) {
  .search-actions {
    gap: 8px;
  }
}

@media (min-width: 769px) {
  .search-actions.before {
    display: none;
  }
}

.search-actions button {
  padding: 8px;
}

.search-actions button:not([disabled]):hover,
.toggle-layout-button.detailed-list {
  color: var(--vp-c-brand-1);
}

.search-actions button.clear-button:disabled {
  opacity: 0.37;
}

.search-keyboard-shortcuts {
  font-size: 0.8rem;
  opacity: 75%;
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  line-height: 14px;
}

.search-keyboard-shortcuts span {
  display: flex;
  align-items: center;
  gap: 4px;
}

@media (max-width: 767px) {
  .search-keyboard-shortcuts {
    display: none;
  }
}

.search-keyboard-shortcuts kbd {
  background: rgba(128, 128, 128, 0.1);
  border-radius: 4px;
  padding: 3px 6px;
  min-width: 24px;
  display: inline-block;
  text-align: center;
  vertical-align: middle;
  border: 1px solid rgba(128, 128, 128, 0.15);
  box-shadow: 0 2px 2px 0 rgba(0, 0, 0, 0.1);
}

.results {
  display: flex;
  flex-direction: column;
  gap: 6px;
  overflow-x: hidden;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.result {
  display: flex;
  align-items: center;
  gap: 8px;
  border-radius: 4px;
  transition: none;
  line-height: 1rem;
  border: solid 2px var(--vp-local-search-result-border);
  outline: none;
}

.result > div {
  margin: 12px;
  width: 100%;
  overflow: hidden;
}

@media (max-width: 767px) {
  .result > div {
    margin: 8px;
  }
}

.titles {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  position: relative;
  z-index: 1001;
  padding: 2px 0;
}

.title {
  display: flex;
  align-items: center;
  gap: 4px;
}

.title.main {
  font-weight: 500;
}

.title-icon {
  opacity: 0.5;
  font-weight: 500;
  color: var(--vp-c-brand-1);
}

.title svg {
  opacity: 0.5;
}

.result.selected {
  --vp-local-search-result-bg: var(--vp-local-search-result-selected-bg);
  border-color: var(--vp-local-search-result-selected-border);
}

.excerpt-wrapper {
  position: relative;
}

.excerpt {
  opacity: 50%;
  pointer-events: none;
  max-height: 140px;
  overflow: hidden;
  position: relative;
  margin-top: 4px;
}

.result.selected .excerpt {
  opacity: 1;
}

.excerpt :deep(*) {
  font-size: 0.8rem !important;
  line-height: 130% !important;
}

.titles :deep(mark),
.excerpt :deep(mark) {
  background-color: var(--vp-local-search-highlight-bg);
  color: var(--vp-local-search-highlight-text);
  border-radius: 2px;
  padding: 0 2px;
}

.excerpt :deep(.vp-code-group) .tabs {
  display: none;
}

.excerpt :deep(.vp-code-group) div[class*='language-'] {
  border-radius: 8px !important;
}

.excerpt-gradient-bottom {
  position: absolute;
  bottom: -1px;
  left: 0;
  width: 100%;
  height: 8px;
  background: linear-gradient(transparent, var(--vp-local-search-result-bg));
  z-index: 1000;
}

.excerpt-gradient-top {
  position: absolute;
  top: -1px;
  left: 0;
  width: 100%;
  height: 8px;
  background: linear-gradient(var(--vp-local-search-result-bg), transparent);
  z-index: 1000;
}

.group-title {
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--vp-c-text-2);
  padding: 6px 12px 0;
}

.result.home-result {
  gap: 12px;
  padding: 8px 12px;
  border-color: transparent;
  border-radius: 8px;
}

.result.home-result.selected {
  background: var(--vp-c-brand-soft);
  border-color: transparent;
}

.home-icon {
  display: flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  font-size: 18px;
  color: var(--tone, var(--vp-c-text-2));
  background: color-mix(in srgb, var(--tone, var(--vp-c-text-2)) 14%, transparent);
}

.home-icon.page-icon {
  --tone: var(--vp-c-text-2);
}

.tone-blue {
  --tone: #3b82f6;
}

.tone-green {
  --tone: #16a34a;
}

.tone-amber {
  --tone: #d97706;
}

.tone-violet {
  --tone: #8b5cf6;
}

.home-text {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.home-title {
  font-weight: 700;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.home-crumb {
  font-size: 0.8rem;
  color: var(--vp-c-text-2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.home-chevron {
  display: flex;
  flex: none;
  font-size: 18px;
  color: var(--vp-c-text-3);
}

.result.home-result.selected .home-title,
.result.home-result.selected .home-chevron {
  color: var(--vp-c-brand-1);
}

.result.selected .titles,
.result.selected .title-icon {
  color: var(--vp-c-brand-1) !important;
}

.no-results {
  font-size: 0.9rem;
  text-align: center;
  padding: 12px;
}

svg {
  flex: none;
}
</style>
