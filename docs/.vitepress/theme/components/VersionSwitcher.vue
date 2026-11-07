<script setup lang="ts">
/**
 * VersionSwitcher: the version list in the top bar of the documentation.
 *
 * The list is read at run time from the `versions.json` the site serves next to the docs
 * (`docsVersion.versionsUrl` in the theme data), so a build made long ago shows releases published
 * after it. Nothing is rendered when the build is unversioned, the file cannot be read or it holds
 * fewer than two usable versions.
 *
 * The file is not trusted: an entry whose link is not a path below the root of the docs is dropped
 * (see `versionLinks.ts`). Choosing a version opens the same page there when it exists, and the home
 * page of that version when it does not; which one is decided when the list is opened.
 */
import { useData } from 'vitepress'
import { computed, onMounted, ref, watch } from 'vue'
import { docsRootOf, hrefFor, pageExists, pageIn, parseVersions } from '../versionLinks'
import type { Probe, VersionEntry } from '../versionLinks'

const { theme, page, site } = useData()

/** The versions offered, empty until the file is read. */
const versions = ref<VersionEntry[]>([])

/** Whether the list is open. */
const open = ref<boolean>(false)

/** The link to use for each version label once the open page was looked for there; the home page until then. */
const checked = ref<Record<string, string>>({})

/** Version settings the config puts in the theme data. */
const settings = computed(() => theme.value.docsVersion as { kind: string; label: string; versionsUrl: string } | undefined)

/** Root of the whole docs site, from the base the open build is served under. */
const root = computed<string>(() => docsRootOf(site.value.base, settings.value?.kind ?? 'latest', settings.value?.label ?? ''))

/** The entry that is the page being read: the latest by kind, any other by its label. */
const current = computed<VersionEntry | undefined>(() => {
  const kind = settings.value?.kind
  return versions.value.find((entry) => (kind === 'latest' ? entry.kind === 'latest' : entry.label === settings.value?.label))
})

/** Text on the button. */
const currentText = computed<string>(() => current.value?.label ?? settings.value?.label ?? '')

/** Path of the open page below the site base, so the same page can be opened in another version. */
const pagePath = computed<string>(() => {
  const withoutExtension = page.value.relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')
  return withoutExtension
})

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
const probe: Probe = (url, init) => fetch(url, { method: init.method, signal: init.signal })

/** Looks for the open page in every other version and keeps the link to use for each. */
const lookForPage = async (): Promise<void> => {
  const path = pagePath.value
  const others = versions.value.filter((entry) => entry.label !== current.value?.label)
  const results = await Promise.all(others.map(async (entry) => [entry.label, hrefFor(entry, path, await pageExists(pageIn(entry, path), probe))] as const))
  // The reader may have opened another page while the answers came in.
  if (path === pagePath.value) {
    checked.value = Object.fromEntries(results)
  }
}

/** Reads `versions.json`; any failure leaves the switcher hidden, as the docs read fine without it. */
const load = async (): Promise<void> => {
  const url = settings.value?.versionsUrl
  if (!url) {
    return
  }
  try {
    const response = await fetch(url)
    if (!response.ok) {
      return
    }
    versions.value = parseVersions(await response.json(), root.value)
  } catch {
    versions.value = []
  }
}

onMounted(load)

// Opening the list looks for the open page in the other versions; a new page starts from the home pages again.
watch(open, (isOpen) => {
  if (isOpen) {
    void lookForPage()
  }
})
watch(pagePath, () => {
  checked.value = {}
})
</script>

<template>
  <div v-if="versions.length > 1" class="version-switcher" @keydown.esc="open = false">
    <button
      type="button"
      class="version-switcher-button"
      aria-haspopup="listbox"
      :aria-expanded="open"
      :aria-label="`Documentation version: ${currentText}`"
      @click="open = !open"
    >
      {{ currentText }}
      <span class="version-switcher-caret" aria-hidden="true"></span>
    </button>
    <ul v-if="open" class="version-switcher-list" role="listbox">
      <li v-for="entry in versions" :key="entry.label" role="option" :aria-selected="entry.label === current?.label">
        <!-- `target` keeps the router of the theme out: another version is another build with its own base, so its pages are not pages of this one and must load as a new document. -->
        <a :href="hrefOf(entry)" target="_self" :class="{ active: entry.label === current?.label }" @click="open = false">
          {{ entry.label }}<span v-if="entry.kind === 'latest'" class="version-switcher-tag">latest</span>
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
