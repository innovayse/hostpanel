<template>
  <div class="flex min-w-0 flex-col justify-end" data-testid="ui-stat">
    <p
      class="line-clamp-2 break-words text-xl font-bold leading-tight sm:text-2xl"
      :class="valueClass"
      :title="String(value)"
      data-testid="ui-stat-value"
    >
      {{ value }}
    </p>
    <p
      class="mt-2 line-clamp-2 break-words text-sm leading-snug"
      :class="labelClass"
      :title="label"
      data-testid="ui-stat-label"
    >
      {{ label }}
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * UiStat: one headline figure with its caption. Every stat has the same type sizes, so a row
 * of them reads evenly; a long text value wraps to two lines and is then cut, with the full text
 * in the `title`. The cell bottom-aligns its content, so captions in a row line up even when one
 * value wraps. Colour is inherited (currentColor) unless a screen passes `valueClass` and
 * `labelClass`, which is how each template applies its own tokens.
 */

/** Props of UiStat. */
interface Props {
  /** The figure or short text, for example `99.9%` or `Europe`. */
  value: string | number
  /** What the figure measures. */
  label: string
  /** Colour classes for the value; empty inherits the surrounding text colour. */
  valueClass?: string
  /** Colour classes for the caption; empty inherits the surrounding text colour. */
  labelClass?: string
}

withDefaults(defineProps<Props>(), {
  valueClass: '',
  labelClass: 'opacity-70',
})
</script>
