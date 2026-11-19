<script setup lang="ts">
/**
 * The article list of the knowledge base screen: a search box (title or body, in every language) and the filter dialog
 * (category and published state) with their removable chips, over a paged table of the articles, drafts included. The
 * search text, the filters and the page live in the address (`useListQuery`), so a reload, a shared link and the back
 * button restore the same list, and the API narrows the articles before it pages. A refused load shows the sentence
 * with a Retry. The list loads itself when it appears and whenever the address changes; the screen that saves an
 * article asks it to read the page again (`reload`) and is told when a row's edit is chosen.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useListQuery } from '@/composables/useListQuery'
import { PUBLISHED_FILTER_OPTIONS } from '@/lib/constants'
import { enumValueOf } from '@/lib/enumValue'
import { isPublishedOfFilter } from '@/lib/publishedFilter'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { statusLabel } from '@/lib/tones'
import { ContentLanguage } from '@/locales/contentLanguage'
import { useKnowledgebaseStore } from '@/stores/knowledgebase'
import type { KbArticle, KbArticleListFilters, KbCategory } from '@/types/kbarticle'
import { PublishedFilter } from '@/types/publishedfilter'
import { Icon } from '@/lib/icons'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextSize } from '@/utils/ui/text'

const { t, locale } = useI18n()

/** Props for KnowledgebaseArticleList. */
const props = defineProps<{
  /** The categories the category filter offers. */
  categories: KbCategory[]
}>()

const emit = defineEmits<{
  /** Emitted with the article whose Edit action was chosen. */
  edit: [article: KbArticle]
}>()

/** The filter fields the dialog edits; each is one query key and one `KbArticleListFilters` member. */
const FilterKey = {
  CategoryId: 'categoryId',
  Published: 'published',
} as const

/** Knowledge base store: the page, the loading flag and the refusal sentence of the article list. */
const store = useKnowledgebaseStore()

/** The filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The category in the address as an id, undefined when none is set or the value is not an id. */
const categoryIdOfAddress = idOf(FilterKey.CategoryId)

/** The published state in the address as a filter choice, undefined when none is set or the value is not one. */
const publishedOfAddress = enumOf(FilterKey.Published, PublishedFilter)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<KbArticleListFilters>(() => ({
  search: appliedSearch.value,
  categoryId: categoryIdOfAddress.value,
  isPublished: isPublishedOfFilter(publishedOfAddress.value),
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.CategoryId,
    label: t('shared.category'),
    value: categoryIdOfAddress.value === undefined
      ? undefined
      : (props.categories.find((category) => category.id === categoryIdOfAddress.value)?.name ?? String(categoryIdOfAddress.value)),
  },
  {
    key: FilterKey.Published,
    label: t('shared.status'),
    value: publishedOfAddress.value === undefined ? undefined : statusLabel(PUBLISHED_FILTER_OPTIONS, publishedOfAddress.value),
  },
])

/** Category choices; the empty value means any. */
const categoryOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...props.categories.map((category) => ({ value: String(category.id), label: category.name })),
])

/** Published-state choices; the empty value means any. */
const publishedOptions = computed(() => [{ value: '', label: t('shared.any') }, ...PUBLISHED_FILTER_OPTIONS])

/** True while the first load is in flight and there are no rows yet; the table then draws its placeholder rows. */
const initialLoading = computed<boolean>(() => store.articlesLoading && store.articles.length === 0)

/**
 * Loads the page the address names with the search and the filters it holds.
 *
 * @returns True when loaded; false when refused (see `store.articlesError`).
 */
const load = (): Promise<boolean> => store.fetchArticles(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

/**
 * The title of an article in the language of the admin panel, else in the first language it has.
 *
 * @param article - The article of the row.
 * @returns The title, or the slug when the article somehow has no text.
 */
const titleOf = (article: KbArticle): string => {
  const own = enumValueOf(ContentLanguage, locale.value)
  return (own !== null ? article.translations[own]?.title : undefined) ?? displayTranslation(article.translations)?.title ?? article.slug
}

/**
 * The row actions of an article: edit.
 *
 * @param article - The article of the row.
 * @returns The action list passed to UiRowActions.
 */
const articleActions = (article: KbArticle): UiMenuAction[] => {
  /**
   * Reports that this article's Edit action was chosen.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    emit('edit', article)
  }
  return [{ label: t('shared.edit'), icon: Icon.Edit, testid: `knowledgebase-article-${article.id}-edit`, onSelect: onEdit }]
}

defineExpose({
  /** Reads the page the address names again, after the screen saved an article. */
  reload: load,
})
</script>

<template>
  <UiStack>
    <UiListToolbar
      v-model:search="search"
      search-testid="knowledgebase-articles-search"
      :search-placeholder="t('support.knowledgebase.searchArticles')"
      filter-testid="knowledgebase-articles-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('shared.category')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.categoryId"
              :options="categoryOptions"
              :placeholder="t('shared.any')"
              data-testid="knowledgebase-articles-filter-category"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.published"
              :options="publishedOptions"
              :placeholder="t('shared.any')"
              data-testid="knowledgebase-articles-filter-published"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.articlesError"
      :message="store.articlesError"
      :loading="store.articlesLoading"
      testid="knowledgebase-articles-error"
      @retry="load"
    />

    <UiCard v-else-if="store.articlesLoading || store.articles.length > 0" data-testid="knowledgebase-articles">
      <UiTable :density="UiTableDensity.Comfortable" :loading="initialLoading">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader :size="UiColumnSize.Grow">{{ t('shared.title') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.category') }}</UiTableHeader>
            <UiTableHeader :size="UiColumnSize.Compact" :card-role="UiCardRole.Badge">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="article in store.articles"
            :key="article.id"
            hoverable
            :data-testid="`knowledgebase-article-${article.id}`"
          >
            <UiTableCell>
              <UiStack gap="xs">
                <UiText tone="primary" size="md">{{ titleOf(article) }}</UiText>
                <UiText tone="muted" size="sm">{{ article.slug }}</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ article.categoryName }}</UiTableCell>
            <UiTableCell>
              <UiStatusIcon
                :state="article.isPublished"
                :on-icon="Icon.CheckCircle"
                :off-icon="Icon.Close"
                :on-label="t('shared.published')"
                :off-label="t('support.knowledgebase.draft')"
              />
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="articleActions(article)" :testid="`knowledgebase-article-${article.id}-actions`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Xs" data-testid="knowledgebase-articles-count">
            {{ t('shared.totalcountRecordsFound', { totalCount: store.articlesTotalCount }) }}
          </UiResultCount>
          <UiPagination
            :page="page"
            :total="store.articlesTotalCount"
            :page-size="store.pageSize"
            testid="knowledgebase-articles-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        v-if="narrowed"
        :title="t('shared.noRecordsFound')"
        data-testid="knowledgebase-articles-empty-narrowed"
      />
      <UiEmptyState
        v-else
        :title="t('support.knowledgebase.noArticlesFound')"
        :description="t('support.knowledgebase.createYourFirstArticleAbove')"
        data-testid="knowledgebase-articles-empty"
      />
    </UiCard>
  </UiStack>
</template>
