<template>
  <UiSkeleton v-if="loading" shape="line" class="w-32" />
  <UiLink v-else-if="target?.external" :href="target.href" external testid="plugin-link">{{ block.label }}</UiLink>
  <UiLink v-else-if="target" :to="target.href" testid="plugin-link">{{ block.label }}</UiLink>
</template>

<script setup lang="ts">
/**
 * PluginLinkBlock - a link, written in the view or read from a GET route.
 *
 * Rendered only when `pluginLinkTarget` admits the address: a host-relative path (one leading `/`, never `//`,
 * no backslash or control character) opens inside the panel; an absolute `https` URL with no user info, on one of
 * the plugin's `externalOrigins`, opens in a new tab with `rel="noopener noreferrer"`. Anything else (`javascript:`,
 * `data:`, `http:`, protocol-relative, another origin, a non-string) renders nothing. The install check refuses the
 * same shapes in the view document itself; this check is what covers an address read from a route at run time.
 */
import { usePluginSource, usePluginView } from '~/composables/usePluginView'
import type { PluginLinkBlock } from '~/types/pluginblock'
import { pluginLinkTarget } from '~/utils/plugins'

/** Props of PluginLinkBlock. */
interface Props {
  /** The block. */
  block: PluginLinkBlock
}

/** The props. */
const props = defineProps<Props>()

/** The view's context: the allowed origins. */
const context = usePluginView()

/** The source read, when the block names one. */
const { value, loading } = usePluginSource(() => props.block.source, () => props.block.id)

/** The checked link, or null when the address may not be linked. */
const target = computed(() =>
  pluginLinkTarget(props.block.source ? value.value : props.block.href, context.externalOrigins))
</script>
