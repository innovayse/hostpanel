<template>
  <UiStack gap="xs" data-testid="usage-bar">
    <UiStack direction="row" justify="between">
      <UiText as="span" size="xs" tone="subtle">{{ label }}</UiText>
      <UiText as="span" size="xs" tone="subtle">{{ usageText }}</UiText>
    </UiStack>
    <UiProgress :value="usagePercent(usedMb, limitMb)" :label="label" :tone="tone" :value-text="usageText" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * UsageBar. One quota of a hosting service (disk or bandwidth): its name, "used / allowance" in
 * megabytes and a progress bar. The bar is read out with the same "used / allowance" text.
 */
import { computed } from 'vue'
import type { UiTone } from '~/utils/ui/enums'
import { usagePercent } from '~/utils/serviceUsage'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Props of UsageBar. */
interface Props {
  /** What is measured, for example "Disk". */
  label: string
  /** Use so far, in megabytes. */
  usedMb: number
  /** The allowance, in megabytes; positive (an unlimited quota draws no bar). */
  limitMb: number
  /** Colour of the bar. */
  tone?: `${UiTone}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  tone: 'primary',
})

/** The "used / allowance" line, shown beside the label and given to the bar as its value text. */
const usageText = computed(() => t('client.services.usageOf', { used: props.usedMb, limit: props.limitMb }))
</script>
