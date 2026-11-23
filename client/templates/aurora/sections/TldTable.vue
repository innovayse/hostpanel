<template>
  <UiStack :as="UiStackTag.Section" gap="none" data-testid="tld-table" class="relative pb-[clamp(56px,8vw,84px)]">
    <UiHeading :level="2" size="bare" :class="[BLOCK_TITLE_CLASS, 'mb-2']">
      {{ t('aurora.tldTable.title') }}
    </UiHeading>
    <UiText tone="inherit" class="mb-7 text-mut">{{ t('aurora.tldTable.lead') }}</UiText>

    <UiTable
      scrollable
      :label="t('aurora.tldTable.title')"
      :columns="5"
      :empty="visibleRows.length === 0"
      :empty-text="t('aurora.tldTable.empty')"
    >
      <template #head>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ t('aurora.tldTable.zone') }}</UiTableTh>
            <UiTableTh>{{ t('aurora.tldTable.register') }}</UiTableTh>
            <UiTableTh>{{ t('aurora.tldTable.renew') }}</UiTableTh>
            <UiTableTh>{{ t('aurora.tldTable.transfer') }}</UiTableTh>
            <UiTableTh>{{ t('aurora.tldTable.note') }}</UiTableTh>
          </UiTableRow>
        </UiTableHead>
      </template>
      <UiTableBody>
        <UiTableRow v-for="row in visibleRows" :key="row.tld" :data-testid="`tld-row-${row.tld}`">
          <UiTableTd class="font-mono text-base text-tx">{{ row.tld }}</UiTableTd>
          <UiTableTd class="font-semibold text-tx">{{ row.register }}</UiTableTd>
          <UiTableTd class="text-tx2">{{ row.renew }}</UiTableTd>
          <UiTableTd class="text-tx2">{{ row.transfer }}</UiTableTd>
          <!-- The registrar's own category tag; the design's editorial note has no field behind it. -->
          <UiTableTd class="text-ac1">{{ row.categories[0] ?? '' }}</UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora TLD price table.
 *
 * Rows and their category tags come from the API, so the table reflects whatever
 * the operator's registrar actually offers. The category filter itself lives in
 * the search bar above, because it narrows the results list as well as this
 * table; the selection arrives here as a prop.
 */
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { BLOCK_TITLE_CLASS } from '~/templates/aurora/typography'
import type { TldPriceRow } from '~/types/tldpricerow'
import { UiStackTag } from '~/utils/ui/enums'

/** Props with their defaults applied. */
const props = withDefaults(defineProps<{
  /** Every priced extension. */
  rows?: TldPriceRow[]
  /** Selected category, or ALL_CATEGORY for the unfiltered view. */
  active?: string
}>(), { rows: () => [], active: ALL_CATEGORY })

/** Translator for the active locale. */
const { t } = useI18n()

/** The rows in the selected category. */
const visibleRows = computed(() =>
  props.active === ALL_CATEGORY
    ? props.rows
    : props.rows.filter(row => row.categories.includes(props.active)))
</script>
