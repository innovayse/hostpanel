<script setup lang="ts">
/**
 * VersionSwitcher: the version list in the top bar of the documentation.
 *
 * The list is read at run time from the `versions.json` the site serves next to the docs
 * (`docsVersion.versionsUrl` in the theme data), so a build made long ago shows releases published
 * after it. The button itself is part of the pre-rendered page and carries the label of the build, so
 * the top bar has its final width before the file arrives and nothing moves when it does. It stays
 * inert until the list is read, and it is removed when the file cannot be read or holds fewer than two
 * usable versions. Nothing is rendered when the build has no `versionsUrl`.
 *
 * The file is not trusted: an entry whose link is not a path below the root of the docs is dropped
 * (see `versionLinks.ts`). Choosing a version opens the same page there when it exists, and the home
 * page of that version when it does not; which one is decided when the list is opened. The lookup is
 * owned by this component: it is aborted when the list closes, when the route changes and when the
 * component is unmounted, and its final answers (the page is there or it is not) are kept per page; a failed or timed out request is asked again later.
 */
import { useData } from 'vitepress'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { switcherLabel } from '../switcherLabel'
import { docsRootOf, findPages, pageIn, parseVersions, VersionKind } from '../versionLinks'
import type { Probe, VersionEntry } from '../versionLinks'

const { theme, page, site } = useData()

/** The versions offered, empty until the file is read. */
const versions = ref<VersionEntry[]>([])

/** Whether reading the list failed or it holds a single version, so there is nothing to switch to. */
const unavailable = ref<boolean>(false)

/** Whether the list is open. */
const open = ref<boolean>(false)

/** The link to use for each version label once the open page was looked for there; the home page until then. */
const checked = ref<Record<string, string>>({})

/** Final answers of earlier lookups by page address, so a page is asked for once however often the list is opened; a failed lookup is never stored. */
const answers = new Map<string, boolean>()

/** The controller of the lookup in flight, if any; owned by this component and aborted by {@link cancelLookup}. */
let lookup: AbortController | null = null

/** Version settings the config puts in the theme data. */
const settings = computed(() => theme.value.docsVersion as { kind: VersionKind; label: string; versionsUrl: string } | undefined)

/** Root of the whole docs site, from the base the open build is served under. */
const root = computed<string>(() => docsRootOf(site.value.base, settings.value?.kind ?? VersionKind.Latest, settings.value?.label ?? ''))

/** The entry that is the page being read: the latest by kind, any other by its label. */
const current = computed<VersionEntry | undefined>(() => {
  const kind = settings.value?.kind
  return versions.value.find((entry) => (kind === VersionKind.Latest ? entry.kind === VersionKind.Latest : entry.label === settings.value?.label))
})

/** Text on the button, the same before and after the list is read (see `switcherLabel`). */
const currentText = computed<string>(() =>
  switcherLabel(settings.value?.kind ?? VersionKind.Latest, settings.value?.label ?? '', current.value?.label),
)

/** Whether the list has been read and holds something to choose from. */
const ready = computed<boolean>(() => versions.value.length > 1)

/** Path of the open page below the site base, so the same page can be opened in another version. */
const pagePath = computed<string>(() => page.value.relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, ''))

/**
 * Address to open for a version: the open page in the version being read, the same page in another
 * version once it was found there, and the home page of that version until then or when it is missing.
 * @param entry The version to open.
 * @returns The path to link to.
 */
const hrefOf = (entry: VersionEntry): string => {
  if (entry.label === current.value?.label) {
    return pageIn(entry, pagePath.value)
  }
  return checked.value[entry.label] ?? entry.link
}

/** Request used to look for a page; the browser's `fetch`, narrowed to what the check needs. */
const probe: Probe = (url, init) => fetch(url, { method: init.method, signal: init.signal, redirect: init.redirect })

/** Stops the lookup in flight, if any, and its requests. */
const cancelLookup = (): void => {
  lookup?.abort()
  lookup = null
}

/** Looks for the open page in every other version and keeps the link to use for each. */
const lookForPage = async (): Promise<void> => {
  cancelLookup()
  const controller = new AbortController()
  lookup = controller
  const others = versions.value.filter((entry) => entry.label !== current.value?.label)
  const found = await findPages(others, pagePath.value, probe, { signal: controller.signal, cache: answers })
  // An aborted lookup belongs to a list that was closed or to another page; its answers are not used.
  if (!controller.signal.aborted) {
    checked.value = found
    lookup = null
  }
}

/** Opens or closes the list; there is nothing to open before the list has been read. */
const toggle = (): void => {
  if (ready.value) {
    open.value = !open.value
  }
}

/** Reads `versions.json`; any failure removes the switcher, as the docs read fine without it. */
const load = async (): Promise<void> => {
  const url = settings.value?.versionsUrl
  if (!url) {
    return
  }
  try {
    const response = await fetch(url)
    if (!response.ok) {
      unavailable.value = true
      return
    }
    versions.value = parseVersions(await response.json(), root.value)
    unavailable.value = versions.value.length < 2
  } catch {
    versions.value = []
    unavailable.value = true
  }
}

onMounted(load)
onBeforeUnmount(cancelLookup)

// Opening the list looks for the open page in the other versions; closing it stops the lookup.
watch(open, (isOpen) => {
  if (isOpen) {
    void lookForPage()
  } else {
    cancelLookup()
  }
})
// A new page starts from the home pages again and stops the lookup of the old one; an open list looks again.
watch(pagePath, () => {
  cancelLookup()
  checked.value = {}
  if (open.value) {
    void lookForPage()
  }
})
</script>

<template>
  <div v-if="settings?.versionsUrl && !unavailable" class="version-switcher" @keydown.esc="open = false">
    <button
      type="button"
      class="version-switcher-button"
      aria-haspopup="listbox"
      :aria-expanded="open"
      :aria-disabled="!ready"
      :aria-label="`Documentation version: ${currentText}`"
      @click="toggle"
    >
      {{ currentText }}
      <span class="version-switcher-caret" aria-hidden="true"></span>
    </button>
    <ul v-if="open && ready" class="version-switcher-list" role="listbox">
      <li v-for="entry in versions" :key="entry.label" role="option" :aria-selected="entry.label === current?.label">
        <!-- `target` keeps the router of the theme out: another version is another build with its own base, so its pages are not pages of this one and must load as a new document. -->
        <a :href="hrefOf(entry)" target="_self" :class="{ active: entry.label === current?.label }" @click="open = false">
          {{ entry.label }}<span v-if="entry.kind === VersionKind.Latest" class="version-switcher-tag">latest</span>
        </a>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.version-switcher {
  position: relative;
  margin-left: 12px;
}
.version-switcher-button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--vp-c-text-1);
}
.version-switcher-button:hover {
  border-color: var(--vp-c-brand-1);
}
.version-switcher-caret {
  width: 0;
  height: 0;
  border-left: 4px solid transparent;
  border-right: 4px solid transparent;
  border-top: 5px solid var(--vp-c-text-2);
}
.version-switcher-list {
  position: absolute;
  right: 0;
  top: calc(100% + 6px);
  z-index: 30;
  min-width: 140px;
  padding: 6px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 10px;
  background: var(--vp-c-bg-elv);
  box-shadow: var(--vp-shadow-3);
  list-style: none;
  margin: 0;
}
.version-switcher-list a {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 6px 10px;
  border-radius: 6px;
  font-size: 13px;
  color: var(--vp-c-text-1);
  text-decoration: none;
}
.version-switcher-list a:hover {
  background: var(--vp-c-default-soft);
}
.version-switcher-list a.active {
  color: var(--vp-c-brand-1);
  font-weight: 600;
}
.version-switcher-tag {
  color: var(--vp-c-text-3);
  font-size: 11px;
}
</style>
