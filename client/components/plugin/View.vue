<template>
  <UiStack gap="lg" data-testid="plugin-view">
    <PluginBlock v-for="entry in entries" :key="entry.key" :block="entry.block" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginView - renders a plugin view document (schema 1), block by block, with the host's own components.
 *
 * It owns the view's context for the blocks below it: the plugin id, the values a route's `{param}` segments are
 * filled from (the page's path parameters or the slot's verified context), the origins a link may open, the
 * refresh signals an action or a form sends to other blocks, and how a row or an action opens another page of
 * the plugin. A block names a route by `{ method, path }` exactly as the manifest declares it; the host checked
 * every such reference at install, and the server checks the caller again on every call, so nothing here decides
 * who may call what.
 */
import { providePluginView } from '~/composables/usePluginView'
import { clientPluginPagePath } from '~/constants/clientRoutes'
import type { PluginBlock as PluginBlockData, PluginPageLink } from '~/types/pluginblock'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginView } from '~/types/pluginview'
import { fillPluginPath, pluginLinkParams } from '~/utils/plugins'

/** Props of PluginView. */
interface Props {
  /** The plugin id. */
  pluginId: string
  /** The view document. */
  view: PluginView
  /** The values route and page parameters are filled from, by parameter name. */
  values: Record<string, string>
  /** The only origins an absolute link may open; an empty list allows host-relative links only. */
  externalOrigins?: string[]
  /** Told of each block's first read as it starts, so a slot widget can tell a slow widget from a ready one. */
  track?: (read: Promise<void>) => void
}

/** The props. */
const props = withDefaults(defineProps<Props>(), {
  externalOrigins: () => [],
  track: undefined,
})

/** The plugin store: the index's pages a link may open. */
const store = usePluginsStore()

/** How many times each block id was asked to read again; a block watches its own count. */
const refreshes = ref<Record<string, number>>({})

/**
 * Asks the blocks with these ids to read again.
 *
 * @param ids - The block ids.
 * @returns Nothing.
 */
const refresh = (ids: string[]): void => {
  const next = { ...refreshes.value }
  for (const id of ids) {
    next[id] = (next[id] ?? 0) + 1
  }
  refreshes.value = next
}

/**
 * Opens a page of the plugin, its parameters read from a row or a reply.
 *
 * The page must be one the index offered the reader, and every `{param}` of its path must be filled; otherwise
 * nothing happens, so a view can never send the reader to an address it composed itself.
 *
 * @param link - The page link.
 * @param row - The row or reply its parameters are read from.
 * @returns Nothing.
 */
const open = (link: PluginPageLink, row: unknown): void => {
  const page = store.pages.find(candidate => candidate.pluginId === props.pluginId && candidate.id === link.page)
  const values = pluginLinkParams(link, row)
  if (!page || !values) {
    return
  }
  const path = fillPluginPath(page.path, values)
  if (path !== null) {
    void navigateTo(clientPluginPagePath(props.pluginId, path))
  }
}

providePluginView({
  pluginId: props.pluginId,
  values: props.values,
  externalOrigins: props.externalOrigins,
  refreshes,
  refresh,
  open,
  track: (read: Promise<void>): void => {
    props.track?.(read)
  },
})

/** The blocks with a key each: the block's id, else its kind and place, which a view document never reorders. */
const entries = computed(() => props.view.blocks.map((block: PluginBlockData, position: number) => ({
  key: block.id ?? `${block.kind}-${position}`,
  block,
})))
</script>
