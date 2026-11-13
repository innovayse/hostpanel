<script setup lang="ts">
/**
 * Outcome line of an action that stays in the page.
 *
 * Unlike {@link UiAlert}, which appears together with its text (and may not be announced, since a
 * live region must exist before its content changes), this region is always rendered and only its
 * text changes. With no message it is an empty, invisible region; with one it takes the alert look
 * of its tone. A failure (`danger`) is announced assertively, everything else politely.
 * `data-testid` falls through to the region.
 */
import { computed } from 'vue'
import { toneAlertClasses, UiTone } from '@/utils/ui/tone'

/** How urgently assistive technology announces a change of the text. */
const LivePoliteness = {
  /** Interrupts the reader. */
  Assertive: 'assertive',
  /** Waits for the reader to pause. */
  Polite: 'polite',
} as const

/** Props for UiStatusMessage. */
const props = withDefaults(defineProps<{
  /** The finished sentence to show, or null to show nothing. */
  message?: string | null
  /** Colour and urgency of the sentence. */
  tone?: UiTone
}>(), {
  message: null,
  tone: UiTone.Success,
})

/** True when there is a sentence to show. */
const visible = computed<boolean>(() => props.message !== null && props.message !== '')

/** How urgently assistive technology announces a change of the text. */
const politeness = computed<(typeof LivePoliteness)[keyof typeof LivePoliteness]>(() =>
  props.tone === UiTone.Danger ? LivePoliteness.Assertive : LivePoliteness.Polite,
)

/** Classes of the region: the alert look while a sentence is shown, nothing otherwise. */
const classes = computed<string[]>(() =>
  visible.value ? ['border rounded-card px-5 py-3 text-sm', toneAlertClasses[props.tone]] : [],
)
</script>

<template>
  <div role="status" :aria-live="politeness" aria-atomic="true" :class="classes" :data-tone="visible ? tone : undefined">
    <template v-if="visible">{{ message }}</template>
  </div>
</template>
