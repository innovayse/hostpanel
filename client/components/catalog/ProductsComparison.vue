<template>
  <UiStack
    v-if="activeGroup"
    :as="UiStackTag.Section"
    gap="none"
    class="py-8 md:py-20 bg-ui-surface relative overflow-hidden [.tpl-classic_&]:bg-classic-surface-2"
    data-testid="products-comparison"
  >
    <UiDecor class="inset-0">
      <UiDecor class="inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-secondary-500/5" />
      <UiDecor class="top-1/2 right-1/4 w-96 h-96 bg-primary-500/10 rounded-full blur-[150px]" />
    </UiDecor>

    <UiContainer class="relative z-10 w-full">
      <MarketingSectionHeader class="mb-12" :badge="$t('products.comparison.title')" :subtitle="$t('products.comparison.subtitle')">
        <template #title>
          <i18n-t keypath="products.comparison.headline" tag="span">
            <template #perfectMatch>
              <UiText :as="UiTextTag.Span" size="inherit" tone="primary" weight="bold" class="[.tpl-classic_&]:text-classic-brand">{{ $t('products.comparison.perfectMatchHighlight') }}</UiText>
            </template>
          </i18n-t>
        </template>
      </MarketingSectionHeader>

      <UiChipTabs
        v-if="groups.length > 1"
        v-model="selectedId"
        class="mb-8"
        :tabs="tabs"
        :label="$t('products.comparison.groupsLabel')"
        testid="comparison-groups"
      />

      <UiStack
        gap="none"
        class="animate-reveal-up motion-reduce:animate-none"
        :class="revealDelay(200)"
      >
        <UiTable scrollable :label="$t('products.comparison.title')">
          <UiTableHead>
            <UiTableRow :hoverable="false">
              <UiTableTh class="w-1/4 [.tpl-classic_&]:text-classic-ink">{{ $t('products.comparison.headers.feature') }}</UiTableTh>
              <UiTableTh
                v-for="plan in activeGroup.plans"
                :key="plan.id"
                align="center"
                class="align-top"
                :class="plan.isFeatured ? 'bg-gradient-to-b from-primary-500/10 to-transparent [.tpl-classic_&]:bg-none [.tpl-classic_&]:bg-classic-brand-soft' : ''"
                :data-testid="`comparison-plan-${plan.id}`"
              >
                <UiLink :to="localePath(siteConfigurePath(plan.id))" tone="inherit" :underline="false" class="inline-flex flex-col items-center gap-1">
                  <UiText :as="UiTextTag.Span" weight="bold" class="normal-case tracking-normal [.tpl-classic_&]:text-classic-ink">{{ plan.name }}</UiText>
                  <UiText
                    v-if="plan.isFeatured"
                    :as="UiTextTag.Span"
                    size="xs"
                    weight="semibold"
                    tone="inherit"
                    class="px-2 py-0.5 rounded-full bg-primary-500 text-on-primary inline-block normal-case tracking-normal [.tpl-classic_&]:rounded [.tpl-classic_&]:bg-classic-fill [.tpl-classic_&]:text-classic-on-brand"
                    :data-testid="`comparison-popular-${plan.id}`"
                  >{{ $t('products.comparison.headers.popular') }}</UiText>
                  <UiText v-if="plan.price" :as="UiTextTag.Span" size="inherit" weight="bold" tone="inherit" class="text-2xl text-primary-700 dark:text-primary-300 normal-case tracking-normal [.tpl-classic_&]:text-classic-brand" :data-testid="`comparison-price-${plan.id}`">{{ plan.price }}</UiText>
                  <UiText v-if="plan.price" :as="UiTextTag.Span" size="xs" tone="inherit" class="normal-case tracking-normal text-gray-600 dark:text-gray-400 [.tpl-classic_&]:text-classic-muted">{{ $t(`products.pricing.cycles.${PRICING_CYCLE_I18N_KEY[plan.billingCycle]}`) }}</UiText>
                </UiLink>
              </UiTableTh>
            </UiTableRow>
          </UiTableHead>

          <UiTableBody class="bg-ui-raised">
            <UiTableRow
              v-for="(row, rowIndex) in activeGroup.rows"
              :key="row.label"
              group
              :data-testid="`comparison-row-${rowIndex}`"
            >
              <UiTableTd class="font-medium text-gray-900 dark:text-white [.tpl-classic_&]:text-classic-ink">{{ row.label }}</UiTableTd>
              <UiTableTd
                v-for="(value, index) in row.values"
                :key="`${row.label}-${activeGroup.plans[index]?.id ?? index}`"
                align="center"
                :class="[
                  value ? 'text-gray-900 dark:text-white [.tpl-classic_&]:text-classic-ink' : 'text-gray-500 dark:text-gray-400 [.tpl-classic_&]:text-classic-muted',
                  activeGroup.plans[index]?.isFeatured ? 'bg-primary-500/5 [.tpl-classic_&]:bg-classic-brand-soft' : '',
                ]"
              >
                {{ value || $t('products.comparison.notIncluded') }}
              </UiTableTd>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiStack>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The plan-comparison table of the products page: the real plans of one catalogue group as
 * columns (prices in the payer's currency, the recommended plan badged) and their shared
 * specification lines as rows.
 *
 * The page hands over only the groups whose plans are comparable (see `buildComparisonGroups`), so
 * the whole section disappears when none is. With more than one group a chip row switches between
 * them.
 */

import { PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { revealDelay } from '~/utils/reveal'
import type { ChipTab } from '~/types/chiptab'
import type { ComparisonGroup } from '~/types/comparisongroup'
import { siteConfigurePath } from '~/constants/siteRoutes'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Props of ProductsComparison. */
interface Props {
  /** The comparable groups, in display order; the first is shown until another tab is chosen. */
  groups: ComparisonGroup[]
}

/** Props of the component. */
const props = defineProps<Props>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The chosen group id as a string (the tab value); empty while the first group is shown. */
const chosenId = ref('')

/** The chosen tab; falls back to the first group when the chosen one is gone (a currency or language change). */
const selectedId = computed({
  get: () => (props.groups.some(group => String(group.id) === chosenId.value) ? chosenId.value : String(props.groups[0]?.id ?? '')),
  set: (value: string) => {
    chosenId.value = value
  },
})

/** The group whose table is shown; undefined when nothing is comparable. */
const activeGroup = computed(() => props.groups.find(group => String(group.id) === selectedId.value))

/** One chip per group. */
const tabs = computed<ChipTab[]>(() => props.groups.map(group => ({
  value: String(group.id),
  label: group.name,
  testid: `comparison-tab-${group.id}`,
})))
</script>
