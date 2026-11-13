<script setup lang="ts">
/**
 * Shell of the reports area: the report list as an inner sidebar and the chosen report beside it.
 *
 * From `md` up the list is the icon sidebar; below it the sidebar would eat the width of a phone, so the same
 * list becomes a horizontally scrollable tab strip above the report.
 */
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import RoutedView from '@/components/layout/RoutedView.vue'
import ReportsInnerSidebar from '@/components/reports/ReportsInnerSidebar.vue'
import UiSidebarLayout from '@/components/ui/UiSidebarLayout.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { REPORTS } from '@/lib/reports'
import { AppRoute, reportPagePath } from '@/lib/routes'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Current route, whose path names the open report. */
const route = useRoute()

/** Router that opens the report a tab stands for. */
const router = useRouter()

/** One tab per report; labels are read each time, so a language switch applies. */
const tabs = computed<UiTabItem[]>(() =>
  REPORTS.map(report => ({ value: report.slug, label: report.label, testid: `reports-tab-${report.slug}` })),
)

/** Slug of the open report, or an empty string outside any report (no tab selected). */
const activeSlug = computed<string>(() => {
  const prefix = `${AppRoute.Reports}/`
  return route.path.startsWith(prefix) ? route.path.slice(prefix.length) : ''
})

/**
 * Opens the report of the chosen tab.
 *
 * @param slug - Slug of the report.
 */
const openReport = (slug: string): void => {
  void router.push(reportPagePath(slug))
}
</script>

<template>
  <UiSidebarLayout data-testid="reports-layout">
    <template #sidebar>
      <ReportsInnerSidebar class="max-md:hidden" />
    </template>
    <UiTabs
      :items="tabs"
      :model-value="activeSlug"
      hide-scrollbar
      class="mb-4 md:hidden"
      data-testid="reports-tabs"
      @update:model-value="openReport"
    />
    <RoutedView />
  </UiSidebarLayout>
</template>
