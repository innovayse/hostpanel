<template>
  <UiCard padding="sm" :class="['border-l-4', STRIPE_CLASSES[tone]]" data-testid="ui-tone-card" :data-tone="tone">
    <slot />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * UiToneCard - a card with a stripe down its left edge that says how something ended.
 */
import { UiTone } from '~/utils/ui/enums'

/** Props of UiToneCard. */
interface Props {
  /** The stripe's meaning. */
  tone?: UiTone
}

withDefaults(defineProps<Props>(), {
  tone: UiTone.Default,
})

/** Stripe colour per tone, complete literals so Tailwind emits them. */
const STRIPE_CLASSES: Readonly<Record<UiTone, string>> = {
  [UiTone.Default]: 'border-l-gray-400',
  [UiTone.Primary]: 'border-l-primary-500',
  [UiTone.Secondary]: 'border-l-secondary-500',
  [UiTone.Success]: 'border-l-green-500',
  [UiTone.Warning]: 'border-l-yellow-500',
  [UiTone.Danger]: 'border-l-red-500',
  [UiTone.Info]: 'border-l-blue-500',
}
</script>
