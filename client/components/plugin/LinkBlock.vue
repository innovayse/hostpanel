<template>
  <UiSkeleton v-if="loading" shape="line" class="w-32" />
  <UiButton
    v-else-if="target?.external"
    :variant="UiButtonVariant.Outline"
    :size="UiSize.Sm"
    :href="target.href"
    target="_blank"
    rel="noopener noreferrer"
    data-testid="plugin-link"
  >
    {{ block.label }}
    <ExternalLink :size="14" :stroke-width="2" class="ml-2" aria-hidden="true" data-testid="plugin-link-external" />
  </UiButton>
  <UiButton v-else-if="target" :variant="UiButtonVariant.Outline" :size="UiSize.Sm" :to="target.href" data-testid="plugin-link">{{ block.label }}</UiButton>
</template>

<script setup lang="ts">
/**
 * PluginLinkBlock - a link, written in the view or read from a GET route, drawn as an outline button; one that leaves
 * the panel carries an external-link mark.
 *
 * Rendered only when `pluginLinkTarget` admits the address: a host-relative path (one leading `/`, never `//`,
 * no backslash or control character) opens inside the panel; an absolute `https` URL with no user info, on one of
 * the plugin's `externalOrigins`, opens in a new tab with `rel="noopener noreferrer"`. Anything else (`javascript:`,
 * `data:`, `http:`, protocol-relative, another origin, a non-string) renders nothing. The install check refuses the
 * same shapes in the view document itself; this check is what covers an address read from a route at run time.
 */
import { ExternalLink } from 'lucide-vue-next'
import { usePluginSource, usePluginView } from '~/composables/usePluginView'
import type { PluginLinkBlock } from '~/types/pluginblock'
import { pluginLinkTarget } from '~/utils/plugins'
import { UiButtonVariant, UiSize } from '~/utils/ui/enums'

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
