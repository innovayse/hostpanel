<script setup lang="ts">
/**
 * A big number without a card: an optional caption, the value in the display font and an optional line under it.
 *
 * `size` picks the value size (`md` is the 30px tile value); `tone` colours the value. The default slot
 * replaces the plain value, for example with a badge. `loading` draws a bar of the value's size in place of the
 * number only: the caption and the line are page text, not data, so they stay and the metric keeps its height.
 */
import { computed } from 'vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { metricSizeClasses } from '@/utils/ui/text'
import { toneTextClasses } from '@/utils/ui/tone'
import type { UiSize } from '@/utils/ui/size'
import type { UiTone } from '@/utils/ui/tone'

/** Props for UiMetric. */
const props = withDefaults(defineProps<{
  /** The number or text to show, already formatted. */
  value: string | number
  /** What the number measures (already translated), above the value. */
  label?: string
  /** Line under the value (already translated). */
  caption?: string
  /** Size of the value. */
  size?: UiSize
  /** Colour of the value. */
  tone?: UiTone
  /** Shows a bar of the value's size in place of the number while it loads; the label and caption stay. */
  loading?: boolean
}>(), {
  label: undefined,
  caption: undefined,
  size: 'md',
  tone: 'neutral',
  loading: false,
})

/** Typography and colour classes of the value. */
const valueClasses = computed<string>(() => `${metricSizeClasses[props.size].value} ${toneTextClasses[props.tone]}`)
</script>

<template>
  <div class="flex flex-col gap-1.5" :aria-busy="loading ? 'true' : undefined">
    <UiText v-if="label" tone="muted" size="sm">
      {{ label }}
    </UiText>
    <UiSkeletonBar v-if="loading" :class="metricSizeClasses[size].bar" aria-hidden="true" />
    <p v-else :class="valueClasses">
      <slot>{{ value }}</slot>
    </p>
    <UiText v-if="caption" tone="muted" size="sm">
      {{ caption }}
    </UiText>
  </div>
</template>
