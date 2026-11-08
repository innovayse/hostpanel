<script setup lang="ts">
/**
 * VersionSwitcher: the version list in the top bar of the documentation.
 *
 * The list is read at run time from the `versions.json` the site serves next to the docs
 * (`docsVersion.versionsUrl` in the theme data), so a build made long ago shows releases published
 * after it. The button itself is part of the pre-rendered page and carries the label of the build, so
 * the top bar has its final width before the file arrives and nothing moves when it does. It stays
 * inert until the list is read, and it is removed when the file cannot be read or holds a single
 * version. Nothing is rendered when the build has no `versionsUrl`. Choosing a version opens the same
 * page there.
 */
import { useData } from 'vitepress'
import { computed, onMounted, ref } from 'vue'
import { switcherLabel } from '../switcherLabel'
import type { DocsVersionKind } from '../../versions'

/** One entry of `versions.json`. */
interface VersionEntry {
  /** Name shown in the list, for example `v0.3` or `next`. */
  label: string
  /** Absolute path of the version's home page, with a trailing slash. */
  link: string
  /** Whether the entry is the latest release, an older release or the development docs. */
  kind: 'latest' | 'release' | 'next'
}

const { theme, page } = useData()

/** The versions offered, empty until the file is read. */
const versions = ref<VersionEntry[]>([])

/** Whether reading the list failed or it holds a single version, so there is nothing to switch to. */
const unavailable = ref<boolean>(false)

/** Whether the list is open. */
const open = ref<boolean>(false)

/** Version settings the config puts in the theme data. */
const settings = computed(() => theme.value.docsVersion as { kind: string; label: string; versionsUrl: string } | undefined)

/** The entry that is the page being read: the latest by kind, any other by its label. */
const current = computed<VersionEntry | undefined>(() => {
  const kind = settings.value?.kind
  return versions.value.find((entry) => (kind === 'latest' ? entry.kind === 'latest' : entry.label === settings.value?.label))
})

/** Text on the button, the same before and after the list is read (see `switcherLabel`). */
const currentText = computed<string>(() =>
  switcherLabel((settings.value?.kind ?? 'latest') as DocsVersionKind, settings.value?.label ?? '', current.value?.label),
)

/** Whether the list has been read and holds something to choose from. */
const ready = computed<boolean>(() => versions.value.length > 1)

/** Path of the open page below the site base, so the same page can be opened in another version. */
const pagePath = computed<string>(() => {
  const withoutExtension = page.value.relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')
  return withoutExtension
})

/**
 * Address of the open page in a version.
 * @param entry The version to open.
 * @returns The path of the same page there.
 */
const hrefOf = (entry: VersionEntry): string => `${entry.link}${pagePath.value}`

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
    const body = (await response.json()) as { versions?: VersionEntry[] }
    versions.value = Array.isArray(body.versions) ? body.versions : []
    unavailable.value = versions.value.length < 2
  } catch {
    versions.value = []
    unavailable.value = true
  }
}

onMounted(load)
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
        <a :href="hrefOf(entry)" :class="{ active: entry.label === current?.label }" @click="open = false">
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
