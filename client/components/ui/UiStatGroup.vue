<template>
  <div :class="['grid grid-cols-2 gap-y-6', columnClass]" data-testid="ui-stat-group">
    <UiStat
      v-for="(stat, index) in stats"
      :key="`${index}-${stat.label}`"
      :value="stat.value"
      :label="stat.label"
      :value-class="valueClass"
      :label-class="labelClass"
      :class="[
        // Dividers sit on the left edge of a cell that has a neighbour before it in its row, so
        // none is left dangling when the cells wrap.
        'max-sm:even:border-l max-sm:even:pl-5 sm:border-l sm:pl-6 sm:first:border-l-0 sm:first:pl-0',
        dividerClass,
      ]"
      data-testid="ui-stat-group-item"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * UiStatGroup. An even grid of stats: two columns on a phone, one equal column per stat from
 * `sm` up (four at most, then it wraps). Dividers are drawn between neighbouring cells only.
 */
import { computed } from 'vue'

/** One figure of the group. */
interface StatItem {
  /** The figure or short text. */
  value: string
  /** What the figure measures. */
  label: string
}

/** Props of UiStatGroup. */
interface Props {
  /** The stats, in reading order. */
  stats: StatItem[]
  /** Colour classes for every value. */
  valueClass?: string
  /** Colour classes for every caption. */
  labelClass?: string
  /** Colour class for the dividers (a `border-*` colour). */
  dividerClass?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  valueClass: '',
  labelClass: 'opacity-70',
  dividerClass: 'border-gray-200 dark:border-white/10',
})

/** Column count from `sm` up; literal classes so Tailwind emits them. */
const COLUMN_CLASSES: Record<number, string> = {
  1: 'sm:grid-cols-1',
  2: 'sm:grid-cols-2',
  3: 'sm:grid-cols-3',
  4: 'sm:grid-cols-4',
}

/** Equal columns, one per stat. */
const columnClass = computed(() => COLUMN_CLASSES[Math.min(Math.max(props.stats.length, 1), 4)])
</script>
