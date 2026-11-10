<script setup lang="ts">
/**
 * A link, written in the view or read from a GET route. Rendered only when `pluginLinkTarget` admits the
 * address: a host-relative path opens inside the panel; an absolute `https` URL without user info on one
 * of the plugin's `externalOrigins` opens in a new tab with `rel="noopener noreferrer"`. Anything else
 * renders nothing. The install check refuses the same shapes in the document itself; this check covers an
 * address read from a route at run time.
 */
import { computed } from 'vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import { usePluginSource, usePluginView } from '@/composables/usePluginView'
import { pluginLinkTarget } from '@/lib/pluginView'
import type { PluginLinkBlock, PluginLinkTarget } from '@/types/pluginview'

/** Props for PluginLinkBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginLinkBlock
}>()

/** The view's context: the allowed origins. */
const context = usePluginView()

/** The source read, when the block names one. */
const { value, loading } = usePluginSource(() => props.block.source, () => props.block.id)

/** The checked link, or null when the address may not be linked. */
const target = computed<PluginLinkTarget | null>(() =>
  pluginLinkTarget(props.block.source ? value.value : props.block.href, context.externalOrigins))
</script>

<template>
  <UiSkeleton v-if="loading" :lines="1" class="w-32" />
  <UiLink v-else-if="target?.external" :href="target.href" external class="self-start" data-testid="plugin-link">{{ block.label }}</UiLink>
  <UiLink v-else-if="target" :to="target.href" class="self-start" data-testid="plugin-link">{{ block.label }}</UiLink>
</template>
