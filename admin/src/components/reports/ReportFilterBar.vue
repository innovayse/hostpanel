<script setup lang="ts">
/**
 * Filter card of a report: the controls in the `fields` slot, extra rows in the default slot, and
 * the actions (generate, clear) the screen switches on, on the same row as the fields (`UiFilterCard`). Export and print live in the report
 * header (`ReportActions`). On a phone the card collapses into one Filters button with the count of the filters in force
 * (`activeCount`); it opens the very same controls in the Filters sheet (`UiFilterTrigger`), whose Apply generates the
 * report and whose Clear (when `clearable`) clears it.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiFilterTrigger from '@/components/ui/UiFilterTrigger.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { UiAlignItems } from '@/utils/ui/layout'

const { t } = useI18n()

/** Props for ReportFilterBar. */
withDefaults(defineProps<{
  /** Caption of the main button; "Generate" when omitted. */
  generateLabel?: string
  /** Shows a Clear button. */
  clearable?: boolean
  /** How many filters are in force; the phone's Filters button shows it (hidden at zero). */
  activeCount?: number
}>(), {
  generateLabel: undefined,
  clearable: false,
  activeCount: 0,
})

const emit = defineEmits<{
  /** The operator asked for the report with the chosen filters. */
  generate: []
  /** The operator cleared the filters. */
  clear: []
}>()

/** The shape the screen asks for: a phone folds the card into the Filters sheet. */
const presentation = useDrawerPresentation()
</script>

<template>
  <UiStack v-if="presentation === UiDrawerPresentation.Sheet" class="mb-6" :align="UiAlignItems.Start" data-testid="report-filters">
    <UiFilterTrigger
      :count="activeCount"
      :resettable="clearable"
      data-testid="report-filter-trigger"
      @apply="emit('generate')"
      @reset="emit('clear')"
    >
      <UiStack>
        <slot name="fields" />
        <slot />
      </UiStack>
    </UiFilterTrigger>
  </UiStack>
  <UiFilterCard v-else data-testid="report-filters">
    <template #fields>
      <slot name="fields" />
    </template>

    <slot />

    <template #actions>
      <UiButton :action="UiActionKind.Generate" :variant="UiButtonVariant.Primary" data-testid="report-generate" @click="emit('generate')">
        {{ generateLabel ?? t('shared.generate') }}
      </UiButton>
      <UiButton :action="UiActionKind.Clear" v-if="clearable" :variant="UiButtonVariant.Ghost" data-testid="report-clear" @click="emit('clear')">
        {{ t('shared.clear') }}
      </UiButton>
    </template>
  </UiFilterCard>
</template>
