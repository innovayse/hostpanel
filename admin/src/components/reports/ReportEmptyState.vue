<script setup lang="ts">
/**
 * The one compact empty state of the report screens: a glass card with an icon, one sentence and a
 * hint to change the filters.
 *
 * `ReportPage` draws it once for a whole report with no data, and `ReportTable` / `ReportChartCard`
 * draw it in their own place when only they are empty (see `lib/reportEmpty`), so no report ever shows
 * an empty chart area, a bare table header or a sideways scrollbar. Nothing else draws an empty report. A report whose
 * empty state has a next step (fetch the figures) puts the button in the `actions` slot.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'

const { t } = useI18n()

/** Props for ReportEmptyState. */
withDefaults(defineProps<{
  /** The sentence (already translated); "No data for this report." when omitted. */
  title?: string
  /** The line under it (already translated); the hint to change the filters when omitted. */
  hint?: string
  /** Prefix of the `data-testid`. */
  testid?: string
}>(), {
  title: undefined,
  hint: undefined,
  testid: 'report-empty',
})

/** Slots of ReportEmptyState. */
defineSlots<{
  /** The call to action under the sentence, when the empty report has a next step. */
  actions?: () => unknown
}>()
</script>

<template>
  <UiCard :data-testid="testid">
    <UiEmptyState compact :title="title ?? t('shared.reportEmptyTitle')" :description="hint ?? t('shared.reportEmptyHint')">
      <template #icon>
        <UiIcon :markup="Icon.Inbox" :size="UiSize.Md" />
      </template>
      <template v-if="$slots.actions" #actions>
        <slot name="actions" />
      </template>
    </UiEmptyState>
  </UiCard>
</template>
