<script setup lang="ts">
/**
 * Section navigation of the client detail pages: one tab per section (the kit's `UiTabs`), a horizontal strip
 * that scrolls by itself when the sections do not fit, at every screen width. The layout puts it under the
 * client's header card and above the content.
 */
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiTabs from '@/components/ui/UiTabs.vue'
import type { UiTabItem } from '@/utils/ui/tabs'
import { CLIENT_NAV } from '@/lib/clientSections'
import { clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

/** Props for ClientSectionTabs. */
const props = defineProps<{
  /** Client ID for constructing sub-page route paths. */
  clientId: string
}>()

/** Current route, used to mark the active entry. */
const route = useRoute()

/** Router, used to open the section a tab names. */
const router = useRouter()

/** One section of the client as the tabs and the router read it: its key, translated caption and route path. */
interface SectionEntry {
  /** Key of the section; also the tab value. */
  key: string
  /** Translated caption. */
  label: string
  /** Route path of the section for this client. */
  path: string
}

/** The sections in tab order, captions read in the active language. */
const sections = computed<SectionEntry[]>(() =>
  CLIENT_NAV.map(entry => ({
    key: entry.key,
    label: t(entry.label),
    path: clientSectionPath(props.clientId, entry.section),
  })),
)

/** Key of the section whose path the current route starts with. */
const activeKey = computed<string | undefined>(() => {
  const active = sections.value.find(section => route.path.startsWith(section.path))
  return active?.key
})

/** The sections as tabs. */
const tabItems = computed<UiTabItem[]>(() =>
  sections.value.map(section => ({ value: section.key, label: section.label, testid: `client-tab-${section.key}` })),
)

/**
 * Opens the section of the tab the visitor picked.
 *
 * @param key - Key of the picked entry.
 * @returns Resolves when the navigation is over.
 */
const openSection = async (key: string): Promise<void> => {
  const target = sections.value.find(section => section.key === key)
  if (target) {
    await router.push(target.path)
  }
}
</script>

<template>
  <UiTabs
    class="min-w-0"
    :items="tabItems"
    :model-value="activeKey ?? ''"
    :aria-label="t('clients.clientNav.strip')"
    hide-scrollbar
    data-testid="client-nav-strip"
    @update:model-value="openSection"
  />
</template>
