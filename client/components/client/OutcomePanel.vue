<template>
  <UiStack align="center" gap="sm" class="py-4 text-center" data-testid="outcome-panel">
    <UiStack align="center" justify="center" gap="none" class="mb-2 h-16 w-16 rounded-2xl" :class="toneClass">
      <component :is="icon" :size="32" :stroke-width="1.5" />
    </UiStack>
    <UiHeading :level="headingLevel" size="subsection">{{ title }}</UiHeading>
    <UiText v-if="message" tone="muted" size="sm" class="leading-relaxed">{{ message }}</UiText>
    <slot />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * A centred outcome inside a card: a tinted icon, a heading, one explanatory line and an
 * optional action below.
 *
 * Replaces the hand-drawn icon square the reset, invitation and forgot-password pages each
 * repeated for their success, invalid-link and sign-in-required states.
 */
import type { Component } from 'vue'
import { UiHeadingLevel, UiTone } from '~/utils/ui/enums'
import { ICON_TONE_CLASSES } from '~/utils/ui/tones'

/** Props of ClientOutcomePanel. */
interface Props {
  /** Lucide icon component drawn in the tinted square. */
  icon: Component
  /** Heading of the outcome. */
  title: string
  /** The explanatory sentence under the heading. */
  message?: string
  /** Tone of the icon square. */
  tone?: `${UiTone}`
  /** Level of the heading, so a page whose whole message is this panel can make it its h1. */
  headingLevel?: UiHeadingLevel
}

/** Props with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  message: undefined,
  tone: UiTone.Primary,
  headingLevel: UiHeadingLevel.H2
})

/** Tint classes of the icon square. */
const toneClass = computed(() => ICON_TONE_CLASSES[props.tone as UiTone])
</script>
