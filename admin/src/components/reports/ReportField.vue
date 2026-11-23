<script setup lang="ts">
/**
 * One captioned control of a report filter row.
 */
import { computed } from 'vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiGap } from '@/utils/ui/layout'
import type { ReportFieldWidth } from '@/utils/ui/reportField'
import { UiLabelVariant } from '@/utils/ui/text'

/** Props for ReportField. */
const props = withDefaults(defineProps<{
  /** Caption above the control (already translated). */
  label: string
  /** Width of the field; `grow` takes the free space of the row. */
  width?: ReportFieldWidth
}>(), {
  width: 'md',
})

/** Width classes. */
const widthClasses: Record<ReportFieldWidth, string> = {
  sm: 'w-28 max-sm:w-full',
  md: 'w-44 max-sm:w-full',
  lg: 'w-64 max-sm:w-full',
  grow: 'flex-1 min-w-[12rem] max-sm:w-full',
}

/** Width class of the field. */
const classes = computed<string>(() => widthClasses[props.width])
</script>

<template>
  <UiStack :gap="UiGap.Xs" :class="classes">
    <UiLabel :variant="UiLabelVariant.Field">
      {{ label }}
    </UiLabel>
    <slot />
  </UiStack>
</template>
