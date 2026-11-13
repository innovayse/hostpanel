<template>
  <div
    role="progressbar"
    :aria-label="label"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-valuenow="percent"
    :aria-valuetext="valueText"
    data-testid="ui-progress"
    class="h-1.5 w-full rounded-full bg-gray-200 dark:bg-white/10 overflow-hidden"
  >
    <svg class="block h-full w-full" aria-hidden="true" focusable="false">
      <rect
        data-testid="ui-progress-bar"
        :width="`${percent}%`"
        height="100%"
        rx="3"
        :class="['transition-all duration-500', TONE_CLASSES[tone as UiTone]]"
      />
    </svg>
  </div>
</template>

<script setup lang="ts">
/**
 * UiProgress: a thin horizontal bar showing how much of a whole is used or done.
 *
 * The filled part is an SVG rectangle whose `width` attribute is the percentage. An attribute is
 * part of the server-rendered markup, so the bar is drawn at its real width before any script runs
 * and always matches `aria-valuenow`, and the component still carries no inline `style`. The value
 * is clamped to 0-100 and a non-number reads as empty.
 */
import { computed } from 'vue'
import { UiTone } from '~/utils/ui/enums'

/** Props of UiProgress. */
interface Props {
  /** How full the bar is, as a percentage from 0 to 100. */
  value: number
  /** Accessible name of the bar (what is being measured). */
  label: string
  /** Colour of the filled part. */
  tone?: `${UiTone}`
  /** The value as a reader should hear it, e.g. "256 MB / 1024 MB"; the bare percentage when unset. */
  valueText?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  tone: UiTone.Primary,
  valueText: undefined,
})

/** Fill colour per tone. */
const TONE_CLASSES: Record<UiTone, string> = {
  [UiTone.Default]: 'fill-gray-500',
  [UiTone.Primary]: 'fill-primary-500',
  [UiTone.Secondary]: 'fill-secondary-500',
  [UiTone.Success]: 'fill-green-500',
  [UiTone.Warning]: 'fill-yellow-500',
  [UiTone.Danger]: 'fill-red-500',
  [UiTone.Info]: 'fill-blue-500',
}

/** The value as a whole percentage between 0 and 100. */
const percent = computed(() => {
  if (!Number.isFinite(props.value)) {
    return 0
  }
  return Math.round(Math.min(100, Math.max(0, props.value)))
})
</script>
