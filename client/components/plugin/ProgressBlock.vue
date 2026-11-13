<template>
  <UiSkeleton v-if="loading" shape="line" />
  <UiText v-else-if="error" size="sm" tone="muted" data-testid="plugin-progress-error">{{ error }}</UiText>
  <UiProgress v-else :value="percent" :label="block.label" data-testid="plugin-progress" />
</template>

<script setup lang="ts">
/**
 * PluginProgressBlock: a progress bar, its value written in the view or read from a GET route, shown as a share
 * of `max` (100 when absent or not positive) and clamped to 0..100, so a plugin cannot draw past the bar.
 */
import { usePluginSource } from '~/composables/usePluginView'
import type { PluginProgressBlock } from '~/types/pluginblock'

/** Props of PluginProgressBlock. */
interface Props {
  /** The block. */
  block: PluginProgressBlock
}

/** The props. */
const props = defineProps<Props>()

/** The largest value when the view gives none. */
const DEFAULT_MAX = 100

/** The full bar, in percent. */
const FULL = 100

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The value as a percentage, 0 to 100; 0 for anything that is not a finite number. */
const percent = computed(() => {
  const raw = props.block.source ? value.value : props.block.value
  const number = typeof raw === 'number' ? raw : typeof raw === 'string' && raw.trim() !== '' ? Number(raw) : Number.NaN
  const max = props.block.max !== undefined && props.block.max > 0 ? props.block.max : DEFAULT_MAX
  return Number.isFinite(number) ? Math.min(FULL, Math.max(0, (number / max) * FULL)) : 0
})
</script>
