<script setup lang="ts">
/**
 * Shell of the reports area: the report list as an inner sidebar and the chosen report beside it.
 *
 * From `md` up the list is the icon sidebar; below it, as the schema asks (`stripBelowMd`), the sidebar would eat the
 * width of a phone, so the kit's sidebar layout swaps it for a tab strip of the same reports above the report.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import RoutedView from '@/components/layout/RoutedView.vue'
import ReportsInnerSidebar from '@/components/reports/ReportsInnerSidebar.vue'
import UiSidebarLayout from '@/components/ui/UiSidebarLayout.vue'
import schema from '@/layouts/ReportsLayout.schema'
import { AppRoute, reportPagePath } from '@/lib/routes'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Translation function. */
const { t } = useI18n()

/** Current route, whose path names the open report. */
const route = useRoute()

/** Router that opens the report a tab stands for. */
const router = useRouter()

/** One tab per report, listed by the layout's schema; captions are read each time, so a language switch applies. */
const tabs = computed<UiTabItem[]>(() => tabItemsOfSchema(schema.tabs, t, 'reports-tab-'))

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
  <UiSidebarLayout
    :strip="schema.stripBelowMd ? tabs : undefined"
    :strip-value="activeSlug"
    strip-testid="reports-tabs"
    data-testid="reports-layout"
    @select-strip="openReport"
  >
    <template #sidebar>
      <ReportsInnerSidebar />
    </template>
    <RoutedView />
  </UiSidebarLayout>
</template>
