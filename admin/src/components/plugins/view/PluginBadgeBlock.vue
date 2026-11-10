<script setup lang="ts">
/**
 * A short status word, written in the view or read from a GET route, in one of the admin's tones. An
 * object read from the route is not a word and draws nothing rather than its JSON.
 */
import { computed } from 'vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiText from '@/components/ui/UiText.vue'
import { usePluginSource } from '@/composables/usePluginView'
import { pluginUiTone } from '@/lib/pluginView'
import type { PluginBadgeBlock } from '@/types/pluginview'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import type { UiTone } from '@/utils/ui/tone'

/** Props for PluginBadgeBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginBadgeBlock
}>()

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The word shown; empty for a missing value or anything that is not a string, number or boolean. */
const text = computed<string>(() => {
  const word = props.block.source ? value.value : props.block.text
  return typeof word === 'string' || typeof word === 'number' || typeof word === 'boolean' ? String(word) : ''
})

/** The badge tone. */
const tone = computed<UiTone>(() => pluginUiTone(props.block.tone))
</script>

<template>
  <UiSkeleton v-if="loading" :lines="1" class="w-20" />
  <UiText v-else-if="error" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-badge-error">{{ error }}</UiText>
  <UiBadge v-else-if="text" :tone="tone" class="self-start" data-testid="plugin-badge">{{ text }}</UiBadge>
</template>
