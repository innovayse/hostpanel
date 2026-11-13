<template>
  <div>
    <dt class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-1">
      {{ label }}
    </dt>
    <dd class="text-gray-900 dark:text-white text-sm" :class="valueClass">
      <slot>{{ value ?? '—' }}</slot>
    </dd>
  </div>
</template>

<script setup lang="ts">
/**
 * DescriptionItem (`UiDescriptionItem`): one label/value pair of a description list; place it
 * inside a `dl`-style grid. An empty value shows an em dash; the default slot overrides the value.
 */

/** Props of DescriptionItem. */
interface Props {
  /** The term, shown small and uppercase. */
  label: string
  /** The value; `null` or `undefined` shows an em dash. */
  value?: string | number | null
  /** Extra classes of the value element. */
  valueClass?: string
}

withDefaults(defineProps<Props>(), {
  value: undefined,
  valueClass: '',
})
</script>
