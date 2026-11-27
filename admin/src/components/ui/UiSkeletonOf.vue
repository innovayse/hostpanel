<script setup lang="ts">
/**
 * The skeleton of a whole screen, drawn from its schema.
 *
 * The router shows it for a route whose view is still downloading, so the shell never sits empty: the sidebar and the
 * top bar stay and the page area holds the screen's own header text, captions, columns and tiles with bars where the
 * values will be. It is made of the kit blocks the real screen is made of (`UiPageHeader`, `UiSchemaForm`,
 * `UiListToolbar`, `UiStatRow`, `UiSkeletonTable`, `UiDetailCards`, `UiTabs`), all in their `loading` state, so the
 * skeleton and the screen are the same shape by construction.
 *
 * What it draws, in order: the header (the real title when the schema has one, a bar when the title is made of data, and
 * a bar for the breadcrumb trail when the schema says the screen has one), then the blocks of the screen as `partsOf` lists them (tiles, form, cards,
 * toolbar, chart, table, tab strip), the same walk for every kind. A tab strip layout draws its header card (identity
 * bars, buttons and key figures) over its real captions, not clickable, with the `outlet` slot under the strip holding the
 * skeleton of the routed child; a vertical one is a rail of icon placeholders beside the outlet, as `UiSidebarLayout`
 * lays out the screen it stands for, and one that sets `stripBelowMd` swaps the rail for the tab strip below `md`.
 *
 * On a phone it folds as the screens do: a table is a stack of cards (`UiTable` in its `loading` state), a form is one
 * column, the key figures are two across (`UiStatRow`), and a bar of actions keeps its first button and one "…" bar
 * (`UiSkeletonActions`).
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiChartSkeleton from '@/components/ui/UiChartSkeleton.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSidebarLayout from '@/components/ui/UiSidebarLayout.vue'
import UiSidebarSkeleton from '@/components/ui/UiSidebarSkeleton.vue'
import UiSkeletonActions from '@/components/ui/UiSkeletonActions.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiSkeletonShape from '@/components/ui/UiSkeletonShape.vue'
import UiSkeletonTable from '@/components/ui/UiSkeletonTable.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { UiChartHeight } from '@/utils/ui/chart'
import { partsOf } from '@/utils/ui/skeletonParts'
import { SkeletonKind, SkeletonPartKind, SkeletonTabsOrientation } from '@/utils/ui/skeletonSchema'
import type { ShapeSkeletonPart, SkeletonPart, SkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiStatRowVariant } from '@/utils/ui/statRow'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

const { t } = useI18n()

/** Props for UiSkeletonOf. */
const props = defineProps<{
  /** The schema of the screen the skeleton stands in for. */
  schema: SkeletonSchema
}>()

/** What the header draws: the real texts the schema names, and the bars for what is made of data. */
const header = computed(() => {
  const source = props.schema.header
  if (source === undefined) {
    return undefined
  }
  return {
    title: source.title === undefined ? undefined : t(source.title),
    subtitle: source.subtitle === undefined ? undefined : t(source.subtitle),
    titleLoading: source.title === undefined,
    subtitleLoading: source.subtitleFromData === true,
    crumbsPending: source.crumbs === true,
    primaryAction: source.primaryAction ?? 0,
    actions: (source.actions ?? 0) + (props.schema.kind === SkeletonKind.Report ? (props.schema.actions?.length ?? 0) : 0),
  }
})

/**
 * Tells whether a part is one of the placeholder shapes (a map, lines, a notice or small print).
 *
 * @param part - A block of the screen.
 * @returns True for a shape, which narrows the part for the template.
 */
const isShape = (part: SkeletonPart): part is ShapeSkeletonPart =>
  part.kind === SkeletonPartKind.Map || part.kind === SkeletonPartKind.Lines || part.kind === SkeletonPartKind.Alert || part.kind === SkeletonPartKind.Note

/** The blocks of the screen under its header. */
const parts = computed(() => partsOf(props.schema))

/** The tab layout of the schema, when the screen is one. */
const tabs = computed(() => (props.schema.kind === SkeletonKind.Tabs ? props.schema : undefined))

/** Whether the tab layout is a rail beside the routed child, not a strip over it. */
const isRail = computed<boolean>(() => tabs.value?.orientation === SkeletonTabsOrientation.Vertical)

/** The tab strip of a layout, with the captions resolved. */
const tabItems = computed<UiTabItem[]>(() => tabItemsOfSchema(tabs.value?.tabs ?? [], t))

/** The entries of the rail of a vertical layout, with the captions resolved. */
const railEntries = computed(() => (tabs.value?.tabs ?? []).map((tab) => ({ key: tab.key, label: t(tab.label) })))
</script>

<template>
  <UiSidebarLayout
    v-if="isRail"
    :strip="tabs?.stripBelowMd ? tabItems : undefined"
    strip-inert
    strip-testid="ui-skeleton-tab-strip"
    role="status"
    aria-busy="true"
    data-testid="ui-skeleton-of"
  >
    <template #sidebar>
      <UiSidebarSkeleton :entries="railEntries" />
    </template>
    <span class="sr-only">{{ t('ui.loading') }}</span>
    <slot name="outlet" />
  </UiSidebarLayout>

  <UiStack v-else role="status" aria-busy="true" data-testid="ui-skeleton-of">
    <span class="sr-only">{{ t('ui.loading') }}</span>
    <UiPageHeader
      v-if="header"
      :title="header.title"
      :subtitle="header.subtitle"
      :loading="header.titleLoading"
      :subtitle-loading="header.subtitleLoading"
      :crumbs-pending="header.crumbsPending"
    >
      <template v-if="header.actions > 0" #actions>
        <UiSkeletonActions :count="header.actions" :primary="header.primaryAction" />
      </template>
    </UiPageHeader>

    <!-- The header card of a tab layout: identity, buttons and key figures -->
    <UiCard v-if="tabs && (tabs.stats || tabs.actions)" padding="md" data-testid="ui-skeleton-header-card">
      <UiStack>
        <UiStack direction="row" align="center" justify="between" gap="md">
          <UiStack direction="row" align="center" gap="md" class="min-w-0 flex-1" aria-hidden="true">
            <UiSkeletonBar class="h-6 w-10 shrink-0 rounded-full" />
            <UiStack gap="xs" class="min-w-0 flex-1">
              <UiSkeletonBar class="h-7 w-56 max-w-full rounded-md" />
              <UiSkeletonBar class="h-4 w-40 rounded-md" />
            </UiStack>
          </UiStack>
          <UiSkeletonActions :count="tabs.actions ?? 0" compact />
        </UiStack>
        <UiStatRow v-if="tabs.stats" :stats="tabs.stats" :variant="UiStatRowVariant.Inline" loading />
      </UiStack>
    </UiCard>

    <template v-for="(part, index) in parts" :key="index">
      <UiSchemaForm v-if="part.kind === SkeletonPartKind.Form" :columns="part.columns" loading />
      <UiDetailCards v-else-if="part.kind === SkeletonPartKind.Cards" :columns="part.columns" :ratio="part.ratio" loading />
      <UiListToolbar v-else-if="part.kind === SkeletonPartKind.Toolbar" :search="part.toolbar.search ? '' : undefined" loading>
        <template v-if="part.toolbar.filters?.length" #default>
          <UiField v-for="filter in part.toolbar.filters" :key="filter" :label="t(filter)" loading />
        </template>
        <template v-if="part.toolbar.actions" #actions>
          <UiSkeletonActions :count="part.toolbar.actions" />
        </template>
      </UiListToolbar>
      <UiStatRow v-else-if="part.kind === SkeletonPartKind.Stats" :stats="part.stats" :columns="part.columns" loading />
      <UiCard v-else-if="part.kind === SkeletonPartKind.Chart" padding="md" data-testid="ui-skeleton-chart">
        <UiChartSkeleton :kind="part.chart.kind" :height="part.chart.height ?? UiChartHeight.Md" />
      </UiCard>
      <UiCard v-else-if="part.kind === SkeletonPartKind.Table" clip>
        <UiSkeletonTable :table="part.table" />
      </UiCard>
      <UiGrid v-else-if="part.kind === SkeletonPartKind.Tiles" min-item-width="md" gap="md" data-testid="ui-skeleton-tiles">
        <UiCard v-for="n in part.count" :key="n" padding="md" container aria-hidden="true">
          <UiStack gap="md">
            <UiStack gap="xs">
              <UiSkeletonBar class="h-3 w-16 rounded" />
              <UiSkeletonBar class="h-5 w-40 max-w-full rounded-md" />
              <UiSkeletonBar class="h-3.5 w-28 rounded-md" />
            </UiStack>
            <UiProgress :value="0" label="" loading />
            <UiGrid :columns="2" gap="sm" container>
              <UiStack v-for="fact in part.facts" :key="fact" direction="row" align="center" gap="sm">
                <UiSkeletonBar class="h-4 w-4 shrink-0 rounded" />
                <UiStack gap="xs" class="min-w-0 flex-1">
                  <UiSkeletonBar class="h-3 w-12 rounded" />
                  <UiSkeletonBar class="h-3.5 w-20 rounded-md" />
                </UiStack>
              </UiStack>
            </UiGrid>
            <UiStack gap="xs">
              <UiSkeletonBar class="h-5 w-20 rounded-full" />
              <UiSkeletonBar class="h-3 w-32 rounded" />
            </UiStack>
          </UiStack>
        </UiCard>
      </UiGrid>
      <UiSkeletonShape v-else-if="isShape(part)" :kind="part.kind" />
      <UiTabs v-else :items="tabItemsOfSchema(part.tabs, t)" model-value="" inert />
    </template>

    <UiTabs v-if="tabs" :items="tabItems" model-value="" inert>
      <slot name="outlet" />
    </UiTabs>
  </UiStack>
</template>
