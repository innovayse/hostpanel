<template>
  <UiStack gap="none" data-testid="kb-page">
    <!-- Hero / Search -->
    <UiPageHero
      :badge="t('kb.badge')"
      :title="t('kb.title')"
      :subtitle="t('kb.subtitle')"
    >
      <!-- Search bar -->
      <UiForm
        spacing="none"
        class="mt-10"
        data-testid="kb-search-form"
        @submit="doSearch"
      >
        <UiStack direction="row" gap="xs">
          <UiInput
            v-model="searchQuery"
            class="flex-1"
            :placeholder="t('kb.searchPlaceholder')"
            testid="kb-search-input"
          >
            <template #prefix>
              <Search :size="16" :stroke-width="2" class="text-gray-400" />
            </template>
          </UiInput>
          <UiButton type="submit" size="lg" data-testid="kb-search-submit">
            <Search :size="16" :stroke-width="2" class="mr-2" />
            {{ t('kb.search') }}
          </UiButton>
        </UiStack>
      </UiForm>
    </UiPageHero>

    <!-- Content -->
    <UiSection padding="md">
      <UiStack gap="xl" class="w-full">
        <!-- Search results -->
        <template v-if="searched">
          <UiStack direction="row" justify="between" align="center">
            <UiHeading :level="2" size="subsection" data-testid="kb-search-count">
              {{ searchResults.length }} {{ t('kb.articles') }}
            </UiHeading>
            <UiButton
              variant="ghost"
              size="sm"
              data-testid="kb-clear-search"
              @click="clearSearch"
            >
              <X :size="14" :stroke-width="2" class="mr-1.5" />
              {{ t('kb.backToKb') }}
            </UiButton>
          </UiStack>

          <UiStack v-if="searchPending" gap="sm" data-testid="kb-search-loading">
            <UiSkeleton v-for="i in 5" :key="i" shape="block" class="h-20" />
          </UiStack>

          <UiEmptyState
            v-else-if="!searchResults.length"
            :icon="Search"
            :title="t('kb.noResults')"
            data-testid="kb-search-empty"
          />

          <UiCard
            v-else
            padding="none"
            class="overflow-hidden"
            data-testid="kb-search-results"
          >
            <UiStack gap="none" class="divide-y divide-gray-100 dark:divide-white/5 [.tpl-classic_&]:divide-classic-border">
              <KbArticleRow v-for="article in searchResults" :key="article.id" :article="article" />
            </UiStack>
          </UiCard>
        </template>

        <!-- Default: categories + the first page of articles -->
        <!-- The landing read failed: say why and offer to read again, never the empty text -->
        <UiAlert v-else-if="!landingPending && loadError" variant="error" data-testid="kb-load-error">
          {{ loadError }}
          <template #action>
            <UiButton variant="outline" size="sm" class="shrink-0" data-testid="kb-retry" @click="refreshLanding()">
              {{ t('common.retry') }}
            </UiButton>
          </template>
        </UiAlert>

        <template v-else>
          <!-- Categories grid -->
          <UiStack gap="lg" data-testid="kb-categories">
            <UiHeading :level="2" size="subsection" class="flex items-center gap-2">
              <FolderOpen :size="20" :stroke-width="2" class="text-primary-700 dark:text-primary-400 [.tpl-classic_&]:text-classic-brand" />
              {{ t('kb.categories') }}
            </UiHeading>

            <UiGrid v-if="catPending" cols="2">
              <UiSkeleton v-for="i in 4" :key="i" shape="block" class="h-24" />
            </UiGrid>

            <UiText v-else-if="!categories.length" tone="muted" size="sm">
              {{ t('kb.empty') }}
            </UiText>

            <UiGrid v-else cols="2">
              <UiLinkCard
                v-for="cat in categories"
                :key="cat.name"
                :to="localePath(siteKnowledgebaseCategoryPath(kbCategorySlug(cat.name)))"
                :testid="`kb-category-card-${kbCategorySlug(cat.name)}`"
              >
                <UiStack direction="row" justify="between" align="center">
                  <UiStack direction="row" align="center" gap="sm">
                    <UiTile
                      :icon="Folder"
                      size="xl"
                      class="border border-primary-500/20 bg-primary-500/10 text-primary-700 dark:text-primary-300 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
                    />
                    <UiText
                      as="span"
                      weight="semibold"
                      class="transition-colors group-hover:text-primary-600 dark:group-hover:text-primary-400 group-hover:[.tpl-classic_&]:text-classic-brand"
                    >
                      {{ cat.name }}
                    </UiText>
                  </UiStack>
                  <UiBadge variant="primary" size="sm" rounded class="[.tpl-classic_&]:rounded-md [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand">
                    {{ cat.articleCount }} {{ cat.articleCount === 1 ? t('kb.article') : t('kb.articles') }}
                  </UiBadge>
                </UiStack>
              </UiLinkCard>
            </UiGrid>
          </UiStack>

          <!-- The first page of published articles -->
          <UiStack gap="lg" data-testid="kb-articles">
            <UiHeading :level="2" size="subsection" class="flex items-center gap-2">
              <FileText :size="20" :stroke-width="2" class="text-primary-600 dark:text-primary-400" />
              {{ t('kb.articlesHeading') }}
            </UiHeading>

            <UiStack v-if="artPending" gap="sm">
              <UiSkeleton v-for="i in 5" :key="i" shape="block" class="h-20" />
            </UiStack>

            <UiText v-else-if="!landingArticles.length" tone="muted" size="sm">
              {{ t('kb.empty') }}
            </UiText>

            <UiCard v-else padding="none" class="overflow-hidden">
              <UiStack gap="none" class="divide-y divide-gray-100 dark:divide-white/5 [.tpl-classic_&]:divide-classic-border">
                <KbArticleRow v-for="article in landingArticles" :key="article.id" :article="article" />
              </UiStack>
            </UiCard>
          </UiStack>
        </template>
      </UiStack>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Public knowledgebase index page.
 * Shows the categories grid and the first page of published articles.
 * Supports live search via query param.
 */

import { Search, FolderOpen, Folder, FileText, X } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useContentStore } from '~/stores/content'
import type { KbArticleSummary } from '~/types/kbarticlesummary'
import type { KbCategory } from '~/types/kbcategory'
import { kbCategorySlug } from '~/utils/kbCategory'
import { siteKnowledgebaseCategoryPath } from '~/constants/siteRoutes'

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
/** The current route. */
const route = useRoute()
/** Router, used to keep the search in the URL. */
const router = useRouter()
/** Translates a key in the active language; `locale` is that language. */
const { t, locale } = useI18n()

useSeoMeta({
  title: t('kb.title'),
  description: t('kb.subtitle')
})

// Categories and the first page of articles, read through the content store; a language change reads them again.
/** The content store that owns the lists this page renders. */
const content = useContentStore()
/** Whether the landing read is still running, and the action that reads it again. */
const { pending: landingPending, refresh: refreshLanding } = await useAsyncData(
  'kb:landing',
  () => Promise.all([content.loadKbCategories(), content.loadLandingArticles()]),
  { watch: [locale] }
)

/**
 * The sentence for a failed read, kept by the content store (it catches the failure, so the
 * async data never carries an error of its own); empty when the read succeeded.
 */
const { error: loadError } = storeToRefs(content)

/** The categories grid and the article list are loaded together, so they share one pending flag. */
const catPending = landingPending

/** Same flag for the article list. */
const artPending = landingPending

/** All top-level categories */
const categories = computed<KbCategory[]>(() => content.kbCategories)

/** The first page of published articles, no category filter. */
const landingArticles = computed<KbArticleSummary[]>(() => content.landingArticles)

// ── Search ──────────────────────────────────────────────────────────────────

/** The text in the search box; starts from `?q=` so a shared link repeats the search. */
const searchQuery = ref(String(route.query.q ?? ''))

/** Whether the results view (rather than the landing view) is showing. */
const searched = ref(!!route.query.q)

/** Whether a search is in flight. */
const searchPending = computed(() => content.searching)

/** The articles the last search found. */
const searchResults = computed<KbArticleSummary[]>(() => content.searchResults)

/** Clear search and return to default view */
const clearSearch = (): void => {
  searchQuery.value = ''
  searched.value = false
  content.clearSearch()
  router.replace({ query: {} })
}

/** Run search and update URL query string */
const doSearch = async (): Promise<void> => {
  if (!searchQuery.value.trim()) {
    clearSearch()
    return
  }
  searched.value = true
  router.replace({ query: { q: searchQuery.value } })
  await content.searchArticles(searchQuery.value)
}

// Re-run search if page is loaded with ?q= param
onMounted(async () => {
  if (route.query.q) {
    await content.searchArticles(searchQuery.value)
  }
})
</script>
