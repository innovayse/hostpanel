<template>
  <UiCard padding="md" data-testid="ui-stat-tile">
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <p class="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ label }}</p>
        <p class="mt-1 text-2xl font-bold text-gray-900 dark:text-white truncate" data-testid="ui-stat-tile-value"><slot name="value">{{ value }}</slot></p>
        <p v-if="hint" class="mt-1 text-xs flex items-center gap-1" :class="trendClass">
          <component :is="trendIcon" v-if="trendIcon" :size="12" :stroke-width="2.5" />
          {{ hint }}
        </p>
      </div>
      <div v-if="icon" class="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0" :class="iconToneClass">
        <component :is="icon" :size="20" :stroke-width="2" />
      </div>
    </div>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * UiStatTile: one headline number with its label, an optional icon and a trend hint
 * (dashboard summary tiles). The `value` slot replaces the plain value, for example with a badge.
 */
import { computed } from 'vue'
import type { Component } from 'vue'
import { ArrowDownRight, ArrowUpRight } from 'lucide-vue-next'
import { UiStatTrend, UiTone } from '~/utils/ui/enums'
import { ICON_TONE_CLASSES } from '~/utils/ui/tones'

/** Props of UiStatTile. */
interface Props {
  /** What the number is. */
  label: string
  /** The number or short text. */
  value: string | number
  /** A lucide icon component. */
  icon?: Component
  /** Colour family of the icon badge. */
  tone?: `${UiTone}`
  /** Supporting line under the value. */
  hint?: string
  /** Direction the value moved; colours and decorates the hint. */
  trend?: `${UiStatTrend}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  icon: undefined,
  tone: UiTone.Primary,
  hint: undefined,
  trend: undefined,
})

/** Class list of the icon badge. */
const iconToneClass = computed(() => ICON_TONE_CLASSES[props.tone as UiTone])

/** Colour of the hint for the trend. */
const trendClass = computed(() => {
  if (props.trend === UiStatTrend.Up) {
    return 'text-green-600 dark:text-green-400'
  }
  if (props.trend === UiStatTrend.Down) {
    return 'text-red-600 dark:text-red-400'
  }
  return 'text-gray-500 dark:text-gray-400'
})

/** Arrow for the trend, none when flat or unset. */
const trendIcon = computed(() => {
  if (props.trend === UiStatTrend.Up) {
    return ArrowUpRight
  }
  if (props.trend === UiStatTrend.Down) {
    return ArrowDownRight
  }
  return null
})
</script>
