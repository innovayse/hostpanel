<template>
  <UiBlock>
    <!--
      A GET to the domains page with the name in the query the page reads on load: an Enter pressed before
      the page is hydrated is submitted natively and still lands on the search; once hydrated, the form
      intercepts the submit and searches in place.
    -->
    <UiForm
      data-testid="domain-search-bar-form"
      spacing="none"
      class="mx-auto"
      method="get"
      :action="searchPath"
      @submit="emit('search', query)"
    >
      <UiStack direction="row" wrap justify="center" align="end" gap="xs">
        <UiInput
          v-model="query"
          testid="domain-search-input"
          :size="UiSize.Lg"
          :name="DOMAIN_SEARCH_QUERY_PARAM"
          :disabled="!hasZones"
          :placeholder="t('aurora.domainSearch.placeholder')"
          class="min-w-[200px] flex-[1_1_260px]"
        />
        <UiButton
          type="submit"
          variant="bare"
          :disabled="!hasZones"
          data-testid="domain-search-submit"
          :size="UiSize.Lg"
          :class="BRAND_BUTTON"
        >
          {{ t('ui.searchButton') }}
        </UiButton>
      </UiStack>
    </UiForm>

    <!--
      Categories come from the registrar data, so the filter reflects the zones an
      operator actually sells rather than a fixed list. Hidden when the backend
      supplies none, which is also when there is nothing to filter.
    -->
    <UiChipTabs
      v-if="categories.length > 1"
      class="mt-4"
      :tabs="categoryTabs"
      :model-value="active"
      :label="t('aurora.tldTable.filterLabel')"
      testid="domain-categories"
      @update:model-value="value => emit('update:category', value)"
    />
  </UiBlock>
</template>

<script setup lang="ts">
/**
 * aurora domain search bar: the wide field and category filter that head the
 * domains page. Presentation only: it emits the term and the chosen category.
 */
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { BRAND_BUTTON } from '~/templates/aurora/buttons'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import type { ChipTab } from '~/types/chiptab'
import { tldCategoryLabel } from '~/utils/tldCategory'
import { UiSize } from '~/utils/ui/enums'

/** Props with their defaults applied. */
const props = withDefaults(defineProps<{
  /** Category names offered by the filter, including the "all" sentinel. */
  categories?: string[]
  /** Currently selected category. */
  active?: string
  /** False when no extension is priced; the field then explains itself upstream. */
  hasZones?: boolean
  /** Text the field starts with. */
  initialQuery?: string
}>(), { categories: () => [], active: ALL_CATEGORY, hasZones: true, initialQuery: '' })

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor submitted a term. */
  search: [term: string]
  /** The visitor picked a category. */
  'update:category': [category: string]
}>()

/** Translator for the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The domains page in the active language: where a submit that runs before hydration lands. */
const searchPath = localePath(SITE_DOMAINS_PATH)

/** The category chips: the sentinel reads "all", every other category its translated (or capitalised) name. */
const categoryTabs = computed<ChipTab[]>(() => props.categories.map(entry => ({
  value: entry,
  label: entry === ALL_CATEGORY ? t('aurora.tldTable.all') : tldCategoryLabel(entry, t),
  testid: `domain-category-${entry}`,
})))

/** What the visitor has typed (v-model of the field). */
const query = ref(props.initialQuery)

// A search started from elsewhere (a name in the address, the home page search) lands here
// after the field was drawn, so the field follows the name the store last searched for.
watch(() => props.initialQuery, (next) => {
  query.value = next
})
</script>
