<script setup lang="ts">
/**
 * A labelled value, written in the view or read from a GET route, formatted with the operator's locale
 * and shown as text. A failed read shows the API's sentence in place of the value, so one failing block
 * never hides the rest of the page. `format: badge` draws the word as a badge above its label.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import { usePluginSource } from '@/composables/usePluginView'
import { formatPluginValue } from '@/lib/pluginView'
import { PluginValueFormat } from '@/types/pluginview'
import type { PluginStatBlock } from '@/types/pluginview'
import { UiCardPadding } from '@/utils/ui/card'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for PluginStatBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginStatBlock
}>()

/** The operator's locale. */
const { locale } = useI18n()

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The value as text. */
const formatted = computed<string>(() =>
  formatPluginValue(props.block.source ? value.value : props.block.value, props.block.format, locale.value))

/** Whether the value is drawn as a badge: asked for by the view and read without a failure. */
const badge = computed<boolean>(() => props.block.format === PluginValueFormat.Badge && !error.value)
</script>

<template>
  <UiSkeleton v-if="loading" :lines="2" data-testid="plugin-stat-loading" />
  <UiCard v-else-if="badge" :padding="UiCardPadding.Md" data-testid="plugin-stat">
    <UiStack :gap="UiGap.Sm">
      <UiBadge class="self-start" data-testid="plugin-stat-badge">{{ formatted }}</UiBadge>
      <UiText :size="UiTextSize.Sm" :tone="UiTextTone.Muted">{{ block.label }}</UiText>
    </UiStack>
  </UiCard>
  <UiStatTile v-else :label="block.label" :value="error || formatted" data-testid="plugin-stat" />
</template>
