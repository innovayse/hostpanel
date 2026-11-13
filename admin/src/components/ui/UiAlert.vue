<script setup lang="ts">
/**
 * Alert banner: an icon and a message in a tinted, bordered card.
 *
 * `variant` is a tone (`error` is accepted as the older name of `danger`). Dangerous tones are
 * announced assertively (`role="alert"`), the rest politely (`role="status"`). The message is the
 * default slot: pass the server's sentence, never compose one.
 */
import { computed } from 'vue'
import { toneAlertClasses, UiTone } from '@/utils/ui/tone'

/** The older name of the `danger` tone, still accepted as a variant. */
const ERROR_ALIAS = 'error'

/** Alert variants: every tone, plus `error` kept as an alias of `danger`. */
type UiAlertVariant = UiTone | typeof ERROR_ALIAS

/** Live-region roles an alert announces itself with. */
const AlertRole = {
  /** Interrupts the reader. */
  Alert: 'alert',
  /** Waits for the reader to pause. */
  Status: 'status',
} as const

/** Props for UiAlert. */
const props = withDefaults(defineProps<{
  /** Visual variant controlling colours. */
  variant?: UiAlertVariant
}>(), {
  variant: UiTone.Warning,
})

/** The tone the variant resolves to. */
const tone = computed<UiTone>(() => (props.variant === ERROR_ALIAS ? UiTone.Danger : props.variant))

/** Colour classes of the tone. */
const toneClasses = computed<string>(() => toneAlertClasses[tone.value])

/** Live-region role: assertive for danger and warning, polite otherwise. */
const role = computed<(typeof AlertRole)[keyof typeof AlertRole]>(() => (tone.value === UiTone.Danger || tone.value === UiTone.Warning ? AlertRole.Alert : AlertRole.Status))

/** Triangle outline used for the warning variant. */
const WARNING_PATH = 'M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z'

/** Circle outline used for every other variant. */
const CIRCLE_PATH = 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z'

/** Outline path of the variant's icon. */
const iconPath = computed<string>(() => (tone.value === UiTone.Warning ? WARNING_PATH : CIRCLE_PATH))
</script>

<template>
  <div
    class="border rounded-card px-5 py-3 flex items-center gap-3"
    :class="toneClasses"
    :role="role"
  >
    <svg class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <path :d="iconPath" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
    <!-- The message takes the rest of the row, so content laid out inside it (a row with an
         action pushed to the far edge) spans the whole alert instead of its own width. -->
    <div class="flex-1 min-w-0 text-sm text-text-primary" data-testid="ui-alert-body">
      <slot />
    </div>
  </div>
</template>
