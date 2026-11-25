<script setup lang="ts">
/**
 * Alert banner: an icon and a message in a tinted, bordered card.
 *
 * `variant` is a tone. Dangerous tones are
 * announced assertively (`role="alert"`), the rest politely (`role="status"`). The message is the
 * default slot: pass the server's sentence, never compose one.
 */
import { computed } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'
import { toneAlertClasses, UiTone } from '@/utils/ui/tone'

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
  variant?: `${UiTone}`
}>(), {
  variant: UiTone.Warning,
})

/** The tone the variant names. */
const tone = computed<UiTone>(() => props.variant as UiTone)

/** Colour classes of the tone. */
const toneClasses = computed<string>(() => toneAlertClasses[tone.value])

/** Live-region role: assertive for danger and warning, polite otherwise. */
const role = computed<(typeof AlertRole)[keyof typeof AlertRole]>(() => (tone.value === UiTone.Danger || tone.value === UiTone.Warning ? AlertRole.Alert : AlertRole.Status))

/** Markup of the variant's icon: a triangle for a warning, a circle for every other variant. */
const iconMarkup = computed<string>(() => (tone.value === UiTone.Warning ? Icon.AlertTriangle : Icon.Alert))
</script>

<template>
  <div
    class="border rounded-card px-5 py-3 flex items-center gap-3"
    :class="toneClasses"
    :role="role"
  >
    <UiIcon :markup="iconMarkup" :size="UiSize.Lg" />
    <!-- The message takes the rest of the row, so content laid out inside it (a row with an
         action pushed to the far edge) spans the whole alert instead of its own width. -->
    <div class="flex-1 min-w-0 break-words text-sm text-text-primary" data-testid="ui-alert-body">
      <slot />
    </div>
  </div>
</template>
