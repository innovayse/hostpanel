<script setup lang="ts">
/**
 * One labelled fact of a card on the plugin overview: a muted caption over its value. The value is the plain `value`
 * text, or the default slot when it needs more than text (a badge, a Markdown block). A missing value (null) shows the
 * `empty` text instead, so a fact the server never recorded still has its place in the card.
 */
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for PluginOverviewFact. */
withDefaults(defineProps<{
  /** The caption of the fact. */
  label: string
  /** The value as text; null when the server has none, which shows `empty`. Ignored when the slot is used. */
  value?: string | null
  /** The text shown in place of a missing value. */
  empty?: string
  /** Draws the value in a monospace face that breaks anywhere (a digest or a key id). */
  mono?: boolean
}>(), {
  value: null,
  empty: '',
  mono: false,
})
</script>

<template>
  <UiStack gap="xs" data-testid="plugin-overview-fact">
    <UiText tone="muted" size="xs">{{ label }}</UiText>
    <slot>
      <UiText v-if="value !== null" tone="primary" size="sm" :class="mono ? 'font-mono break-all' : 'break-words'">{{ value }}</UiText>
      <UiText v-else tone="muted" size="sm">{{ empty }}</UiText>
    </slot>
  </UiStack>
</template>
