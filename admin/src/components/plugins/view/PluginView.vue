<script setup lang="ts">
/**
 * Renders a plugin view document (schema 1), block by block, with the admin's own kit.
 *
 * It owns the view's context for the blocks below it: the plugin id, the values a route's `{param}`
 * segments are filled from (the page's path parameters), the origins a link may open, the refresh signals
 * an action or a form sends to other blocks, how a row or an action opens another page of the plugin, and
 * the follow-ups it keeps running until the view is left. A block names a route by `{ method, path }`
 * exactly as the manifest declares it; the host checked every reference at install and checks the caller
 * again on every call, so nothing here decides who may call what.
 */
import { computed, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PluginBlock from '@/components/plugins/view/PluginBlock.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { providePluginView } from '@/composables/usePluginView'
import { fillPluginPath, pluginLinkParams } from '@/lib/pluginView'
import { pluginPagePath } from '@/lib/routes'
import { usePluginUiStore } from '@/stores/pluginUi'
import type { PluginBlock as PluginBlockData, PluginPageLink, PluginView } from '@/types/pluginview'
import { UiGap } from '@/utils/ui/layout'

/** Props for PluginView. */
const props = withDefaults(defineProps<{
  /** The plugin id. */
  pluginId: string
  /** The view document. */
  view: PluginView
  /** The values route and page parameters are filled from, by parameter name. */
  values?: Record<string, string>
  /** The only origins an absolute link may open; empty allows host-relative links only. */
  externalOrigins?: string[]
}>(), {
  /**
   * No page values by default.
   *
   * @returns An empty map.
   */
  values: () => ({}),
  /**
   * No external origin by default: host-relative links only.
   *
   * @returns An empty list.
   */
  externalOrigins: () => [],
})

/** Router, to open another page of the plugin. */
const router = useRouter()

/** Plugin ui store: the index's pages a link may open. */
const store = usePluginUiStore()

/** How many times each block id was asked to read again; a block watches its own count. */
const refreshes = ref<Record<string, number>>({})

/** The follow-ups running in this view: the function that ends each, by the id of the block that started it. */
const watches = new Map<string, () => void>()

/**
 * Asks the blocks with these ids to read again.
 *
 * @param ids - The block ids.
 * @returns Nothing.
 */
const refresh = (ids: readonly string[]): void => {
  const next = { ...refreshes.value }
  for (const id of ids) {
    next[id] = (next[id] ?? 0) + 1
  }
  refreshes.value = next
}

/**
 * Takes a follow-up into the view's keeping; an earlier watch of the same block ends.
 *
 * @param key - The starting block's id.
 * @param stop - Ends the watch.
 * @returns Lets go of the watch once it ended on its own.
 */
const adopt = (key: string, stop: () => void): (() => void) => {
  watches.get(key)?.()
  watches.set(key, stop)
  return () => {
    // A later watch under the same key is not this one's to drop.
    if (watches.get(key) === stop) {
      watches.delete(key)
    }
  }
}

/**
 * Opens a page of the plugin, its parameters read from a row or a reply. The page must be one the index
 * offered and every `{param}` of its path must be filled; otherwise nothing happens, so a view can never
 * send the operator to an address it composed itself.
 *
 * @param link - The page link.
 * @param row - The row or reply its parameters are read from.
 * @returns Nothing.
 */
const open = (link: PluginPageLink, row: unknown): void => {
  const page = store.pages.find((candidate) => candidate.pluginId === props.pluginId && candidate.id === link.page)
  const values = pluginLinkParams(link, row)
  if (!page || !values) {
    return
  }
  const path = fillPluginPath(page.path, values)
  if (path !== null) {
    void router.push(pluginPagePath(props.pluginId, path))
  }
}

providePluginView({
  pluginId: props.pluginId,
  values: props.values,
  externalOrigins: props.externalOrigins,
  refreshes,
  refresh,
  open,
  adopt,
})

/** The blocks keyed by id or kind and their place, which a view document never reorders. */
const entries = computed<{ key: string; block: PluginBlockData }[]>(() =>
  props.view.blocks.map((block, position) => ({ key: `${block.id ?? block.kind}-${position}`, block })))

onUnmounted(() => {
  for (const stop of watches.values()) {
    stop()
  }
  watches.clear()
})
</script>

<template>
  <UiStack :gap="UiGap.Lg" data-testid="plugin-view">
    <PluginBlock v-for="entry in entries" :key="entry.key" :block="entry.block" />
  </UiStack>
</template>
