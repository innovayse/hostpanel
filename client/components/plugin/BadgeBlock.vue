<template>
  <UiSkeleton v-if="loading" shape="line" class="w-20" />
  <UiText v-else-if="error" size="sm" tone="muted" data-testid="plugin-badge-error">{{ error }}</UiText>
  <UiBadge v-else-if="text" :variant="tone" class="self-start" data-testid="plugin-badge">{{ text }}</UiBadge>
</template>

<script setup lang="ts">
/**
 * PluginBadgeBlock - a short status word, written in the view or read from a GET route, in one of the host's
 * tones. An object read from the route is not a word and draws nothing rather than its JSON.
 */
import { usePluginSource } from '~/composables/usePluginView'
import type { PluginBadgeBlock } from '~/types/pluginblock'
import { pluginUiTone } from '~/utils/plugins'

/** Props of PluginBadgeBlock. */
interface Props {
  /** The block. */
  block: PluginBadgeBlock
}

/** The props. */
const props = defineProps<Props>()

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The word shown; empty for a missing value or anything that is not a string, number or boolean. */
const text = computed(() => {
  const word = props.block.source ? value.value : props.block.text
  return typeof word === 'string' || typeof word === 'number' || typeof word === 'boolean' ? String(word) : ''
})

/** The badge tone. */
const tone = computed(() => pluginUiTone(props.block.tone))
</script>
