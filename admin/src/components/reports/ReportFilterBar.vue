<script setup lang="ts">
/**
 * Filter card of a report: the controls in the `fields` slot, extra rows in the default slot, and
 * the actions (generate, clear) the screen switches on, on the same row as the fields (`UiFilterCard`). Export and print live in the report
 * header (`ReportActions`).
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'

const { t } = useI18n()

/** Props for ReportFilterBar. */
withDefaults(defineProps<{
  /** Caption of the main button; "Generate" when omitted. */
  generateLabel?: string
  /** Shows a Clear button. */
  clearable?: boolean
}>(), {
  generateLabel: undefined,
  clearable: false,
})

const emit = defineEmits<{
  /** The operator asked for the report with the chosen filters. */
  generate: []
  /** The operator cleared the filters. */
  clear: []
}>()
</script>

<template>
  <UiFilterCard data-testid="report-filters">
    <template #fields>
      <slot name="fields" />
    </template>

    <slot />

    <template #actions>
      <UiButton variant="primary" data-testid="report-generate" @click="emit('generate')">
        {{ generateLabel ?? t('shared.generate') }}
      </UiButton>
      <UiButton v-if="clearable" variant="ghost" data-testid="report-clear" @click="emit('clear')">
        {{ t('shared.clear') }}
      </UiButton>
    </template>
  </UiFilterCard>
</template>
