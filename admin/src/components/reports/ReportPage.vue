<script setup lang="ts">
/**
 * Frame every report screen sits in: header (title, description and the `actions` slot for export
 * and print), the filters, then the result.
 *
 * Loading and the refusal sentence come from the reports store, so a screen passes neither. While
 * a report loads the result keeps its layout: the screen's own tiles, charts and tables draw skeletons of
 * themselves (they read the flag this page provides); a failed load shows the API sentence
 * (with a Retry button when the screen passes `retry`) instead of the result, so an empty-state
 * text never stands in for data that could not be read; otherwise the default slot and the
 * "generated on" line show.
 */
import { provide, toRef } from 'vue'
import ReportTimestamp from '@/components/reports/ReportTimestamp.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { REPORT_LOADING } from '@/lib/reportLoading'
import { useReportsStore } from '@/stores/reports'

/** Props for ReportPage. */
defineProps<{
  /** Report title (already translated). */
  title: string
  /** Line under the title (already translated). */
  description?: string
  /** Loads the report again after a failed load; without it the failure shows no Retry button. */
  retry?: () => unknown
}>()

const store = useReportsStore()

/** Tells the report parts below that the report is loading. */
provide(REPORT_LOADING, toRef(store, 'loading'))
</script>

<template>
  <UiStack gap="none" data-testid="report-page">
    <UiPageHeader :title="title" :subtitle="description">
      <template #actions>
        <slot name="actions" />
      </template>
    </UiPageHeader>

    <slot name="filters" />

    <template v-if="store.error && !store.loading">
      <UiErrorState v-if="retry" :message="store.error" testid="report-error" @retry="retry()" />
      <UiAlert v-else variant="danger" data-testid="report-error">
        {{ store.error }}
      </UiAlert>
    </template>
    <UiStack v-else gap="lg" :aria-busy="store.loading ? 'true' : undefined" data-testid="report-body">
      <slot />
      <ReportTimestamp v-if="!store.loading" />
    </UiStack>
  </UiStack>
</template>
