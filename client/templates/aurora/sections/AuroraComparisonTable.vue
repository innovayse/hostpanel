<template>
  <UiStack
    v-if="visible"
    :as="UiStackTag.Section"
    gap="none"
    data-testid="comparison-table"
    class="relative pb-[clamp(56px,8vw,84px)]"
  >
    <UiHeading :level="2" size="bare" :class="[BLOCK_TITLE_CLASS, 'mb-2']">
      {{ t('aurora.comparison.title') }}
    </UiHeading>
    <UiText tone="inherit" class="mb-7 text-mut">{{ t('aurora.comparison.lead') }}</UiText>

    <UiTable scrollable>
      <UiTableHead>
        <UiTableRow :hoverable="false">
          <UiTableTh>{{ t('aurora.comparison.feature') }}</UiTableTh>
          <UiTableTh v-for="plan in plans" :key="plan.id">{{ plan.name }}</UiTableTh>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="row in rows" :key="row.label" :data-testid="`comparison-row-${row.label}`">
          <UiTableTd class="text-mut">{{ row.label }}</UiTableTd>
          <UiTableTd
            v-for="(value, index) in row.values"
            :key="`${row.label}-${plans[index]?.id ?? index}`"
            :class="value ? 'text-tx' : 'text-mut2'"
          >
            <!-- A plan that does not carry this line reads as absent, not as zero. -->
            {{ value || t('aurora.comparison.notIncluded') }}
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora plan comparison table.
 *
 * Presentation only. The rows arrive already aligned to the plan columns from
 * pages/hosting/index.vue, and the whole section disappears when no plan has a
 * specification, so an operator who has entered none sees the page as it was.
 */
import type { PlanCard } from '~/types/plancard'
import type { ComparisonRow } from '~/types/comparisonrow'
import { isComparable } from '~/utils/comparisonRows'
import { BLOCK_TITLE_CLASS } from '~/templates/aurora/typography'
import { UiStackTag } from '~/utils/ui/enums'

/** Component props: the plan columns and the aligned comparison rows. */
const props = withDefaults(defineProps<{
  /** The plans, one column each. */
  plans?: PlanCard[]
  /** Rows in display order, each with one value per plan. */
  rows?: ComparisonRow[]
}>(), { plans: () => [], rows: () => [] })

/** Translator for the active locale. */
const { t } = useI18n()

/** Whether the table adds value: the plans share enough lines to compare. */
const visible = computed(() => isComparable(props.rows))
</script>
