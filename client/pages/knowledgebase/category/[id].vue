<template>
  <UiSection padding="lg" data-testid="kb-category-page">
    <UiStack class="w-full" gap="lg">
      <!-- Back link -->
      <UiLink
        :to="localePath(SITE_KNOWLEDGEBASE_PATH)"
        tone="muted"
        :underline="false"
        class="inline-flex items-center gap-2 self-start text-sm"
        :title="t('kb.backToKb')"
        :aria-label="t('kb.backToKb')"
        testid="kb-back"
      >
        <ArrowLeft :size="15" :stroke-width="2" />
        {{ t('common.back') }}
      </UiLink>

      <!-- Category header; absent while the categories could not be read -->
      <UiStack v-if="category" direction="row" align="center" gap="md">
        <UiTile
          :icon="Folder"
          size="xl"
          class="border border-primary-500/20 bg-primary-500/10 text-primary-700 dark:text-primary-300 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
        />
        <UiStack gap="none">
          <UiHeading :level="1" size="section" data-testid="kb-category-heading">
            {{ categoryName }}
          </UiHeading>
          <UiText size="sm" tone="muted" data-testid="kb-category-count">
            {{ articles.length }} {{ t('kb.articles') }}
          </UiText>
        </UiStack>
      </UiStack>

      <!-- Loading -->
      <UiStack v-if="pending" gap="sm" data-testid="kb-category-loading">
        <UiSkeleton v-for="i in 6" :key="i" shape="block" class="h-16" />
      </UiStack>

      <!-- The read failed: say why and offer to read again, never the empty text -->
      <UiAlert v-else-if="loadError" variant="error" data-testid="kb-category-error">
        {{ loadError }}
        <template #action>
          <UiButton variant="outline" size="sm" class="shrink-0" data-testid="kb-category-retry" @click="retry()">
            {{ t('common.retry') }}
          </UiButton>
        </template>
      </UiAlert>

      <!-- Empty -->
      <UiEmptyState
        v-else-if="!articles.length"
        :icon="FileText"
        :title="t('kb.empty')"
        data-testid="kb-category-empty"
      />

      <!-- Articles list -->
      <UiCard
        v-else
        padding="none"
        class="overflow-hidden"
        data-testid="kb-category-articles"
      >
        <UiStack gap="none" class="divide-y divide-gray-100 dark:divide-white/5 [.tpl-classic_&]:divide-classic-border">
          <KbArticleRow v-for="article in articles" :key="article.id" :article="article" />
        </UiStack>
      </UiCard>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Knowledgebase category page.
 * Lists all articles of the category the route slug names. An unknown slug is a 404 only once the
 * categories were read; a failed category read shows the API sentence with a retry instead.
 */

import { ArrowLeft, Folder, FileText } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useContentStore } from '~/stores/content'
import type { KbArticleSummary } from '~/types/kbarticlesummary'
import type { KbCategory } from '~/types/kbcategory'
import { SeoPageType } from '~/types/seopagetype'
import { kbCategorySlug } from '~/utils/kbCategory'
import { SITE_KNOWLEDGEBASE_PATH, siteKnowledgebaseCategoryPath } from '~/constants/siteRoutes'

/** The current route. */
const route = useRoute()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
/** Translates a key in the active language; `locale` is that language. */
const { t, locale } = useI18n()

/** The category slug the route names. */
const slug = computed(() => String(route.params.id))

// Categories first: the slug only becomes a category, and so an articles query, once they are known.
/** The content store that owns the lists this page renders. */
const content = useContentStore()

/** Reads the categories again; used by {@link retry} after a failed read. */
const { refresh: refreshCategories } = await useAsyncData('kb:categories', () => content.loadKbCategories(), { watch: [locale] })

/**
 * The sentence for a failed read, kept by the content store (it catches the failure, so the
 * async data never carries an error of its own); empty when the read succeeded.
 */
const { error: loadError } = storeToRefs(content)

/** The category the slug names, or null when no category has that slug or the categories failed to load. */
const found = computed<KbCategory | null>(() => content.categoryBySlug(slug.value))

/**
 * Builds the 404 for a slug that the loaded categories do not contain.
 *
 * @returns The 404 error for this page.
 */
const notFound = () => createError({ statusCode: 404, statusMessage: 'Knowledgebase category not found' })

// 404 only when the categories were read and none has this slug; a failed read is an error state.
if (found.value === null && !loadError.value) {
  throw notFound()
}

/** The last category the route resolved to; kept while a later read fails. */
const resolved = ref<KbCategory | null>(found.value)

/** The category the route names; follows the route when its parameter changes, null until one resolves. */
const category = computed<KbCategory | null>(() => found.value ?? resolved.value)

/** The category's name, as the backend sends it; empty while no category is resolved. */
const categoryName = computed(() => category.value?.name ?? '')

/**
 * Reads the articles of the resolved category; without one there is nothing to ask for.
 *
 * @returns The articles of the category; empty while no category is resolved.
 */
const loadArticles = async (): Promise<KbArticleSummary[]> =>
  category.value ? content.loadCategoryArticles(category.value.name) : []

/** Fetch the articles of this category; the key follows the slug and a language change reads them again. `refresh` reads them again after a failure. */
const { pending, refresh } = await useAsyncData(
  () => `kb:category:${slug.value}`,
  loadArticles,
  { watch: [locale] }
)

/**
 * Reads again after a failure: the categories first when they are what failed, then the
 * articles. A slug the now-loaded categories do not contain becomes the 404 page.
 */
const retry = async (): Promise<void> => {
  if (category.value === null) {
    await refreshCategories()
    if (found.value === null) {
      if (!loadError.value) {
        showError(notFound())
      }
      return
    }
    resolved.value = found.value
  }
  await refresh()
}

/** The articles of this category. */
const articles = computed<KbArticleSummary[]>(() => content.articlesFor(categoryName.value))

/** The page path in the default language, built on the canonical slug of the category. */
const pagePath = computed(() => siteKnowledgebaseCategoryPath(kbCategorySlug(categoryName.value)))

/** Page title: the category inside the knowledgebase. */
const seoTitle = computed(() => t('kb.categorySeoTitle', { category: categoryName.value }))

/** Meta description of the category page. */
const seoDescription = computed(() => t('kb.categorySeoDescription', { category: categoryName.value }))

useSeo({
  title: seoTitle.value,
  description: seoDescription.value,
  type: SeoPageType.Website,
  path: pagePath.value
})

// Breadcrumb structured data: the knowledgebase, then this category.
/** Builder and publisher of the breadcrumb structured data. */
const { breadcrumbSchema, injectSchema } = useSchemaOrg()
injectSchema(breadcrumbSchema([
  { name: t('kb.title'), url: localePath(SITE_KNOWLEDGEBASE_PATH) },
  { name: categoryName.value, url: localePath(pagePath.value) }
]))
</script>
