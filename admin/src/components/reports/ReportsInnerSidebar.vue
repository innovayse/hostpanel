<script setup lang="ts">
/**
 * Inner sidebar of the reports area: one icon entry per report under its group heading, the open one marked.
 *
 * Entries carry the short menu caption (the full title stays on the report page, and is the tooltip beside the
 * caption); a search box at the top of the opened sidebar narrows the list, and only while the sidebar is open, so the
 * folded rail always shows every report.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSidebar from '@/components/ui/UiSidebar.vue'
import UiText from '@/components/ui/UiText.vue'
import { filterReports, REPORTS, reportGroupLabel } from '@/lib/reports'
import { Icon, reportIcon } from '@/lib/icons'
import type { UiSidebarItem } from '@/utils/ui/sidebar'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { AppRoute, reportPagePath } from '@/lib/routes'

/** Translation function. */
const { t } = useI18n()

/** Current route, whose path names the open report. */
const route = useRoute()

/** What the operator typed in the search box. */
const query = ref('')

/** Whether the sidebar is open; the search box exists only then, so the folded rail ignores what was typed. */
const open = ref(false)

/** The reports that match the search while the sidebar is open, every report otherwise, in menu order. */
const matching = computed(() => filterReports(REPORTS, open.value ? query.value : ''))

/** Entries of the sidebar; labels are read each time, so a language switch applies. */
const items = computed<UiSidebarItem[]>(() =>
  matching.value.map(report => ({
    key: report.slug,
    label: report.menuLabel,
    title: report.label,
    to: reportPagePath(report.slug),
    icon: reportIcon(report.icon),
    group: reportGroupLabel(report.group),
  })),
)

/** Slug of the report that is open, or undefined outside any report. */
const activeKey = computed<string | undefined>(() => {
  const prefix = `${AppRoute.Reports}/`
  return route.path.startsWith(prefix) ? route.path.slice(prefix.length) : undefined
})
</script>

<template>
  <UiSidebar :items="items" :active-key="activeKey" testid="reports-nav" @update:expanded="open = $event">
    <template #header>
      <UiInput
        v-model="query"
        type="search"
        size="sm"
        :placeholder="t('reports.sidebar.search')"
        data-testid="reports-nav-search"
      >
        <template #icon>
          <UiIcon :markup="Icon.Search" size="sm" />
        </template>
      </UiInput>
      <UiText
        v-if="items.length === 0"
        :tone="UiTextTone.Muted"
        :size="UiTextSize.Xs"
        class="px-1 pt-2"
        data-testid="reports-nav-empty"
      >
        {{ t('reports.sidebar.empty') }}
      </UiText>
    </template>
  </UiSidebar>
</template>
