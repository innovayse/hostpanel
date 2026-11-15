<template>
  <UiSection padding="lg" data-testid="kb-article-page">
    <UiStack class="w-full" gap="lg">
      <!-- Back link -->
      <UiLink
        :to="localePath(SITE_KNOWLEDGEBASE_PATH)"
        tone="muted"
        :underline="false"
        class="inline-flex items-center gap-2 self-start text-sm"
        testid="kb-back"
      >
        <ArrowLeft :size="15" :stroke-width="2" />
        {{ t('kb.backToKb') }}
      </UiLink>

      <!-- Loading -->
      <UiStack v-if="pending" gap="md" data-testid="kb-article-loading">
        <UiSkeleton shape="block" class="h-8 w-3/4" />
        <UiSkeleton class="w-1/3" />
        <UiSkeleton :lines="8" class="mt-8" />
      </UiStack>

      <!-- Article -->
      <UiStack v-else-if="article" as="article" gap="lg">
        <!-- Meta row -->
        <UiStack direction="row" wrap align="center" gap="sm">
          <UiText
            v-if="article.dateupdated"
            as="span"
            size="xs"
            tone="muted"
            class="flex items-center gap-1"
            data-testid="kb-article-updated"
          >
            <Calendar :size="12" :stroke-width="2" />
            {{ t('kb.lastUpdated') }}: {{ article.dateupdated }}
          </UiText>
          <UiText
            v-if="article.views"
            as="span"
            size="xs"
            tone="muted"
            class="flex items-center gap-1"
            data-testid="kb-article-views"
          >
            <Eye :size="12" :stroke-width="2" />
            {{ article.views }} {{ t('kb.views') }}
          </UiText>
        </UiStack>

        <!-- Title -->
        <UiHeading :level="1" data-testid="kb-article-heading">
          {{ article.title }}
        </UiHeading>

        <UiDivider />

        <!-- HTML body from the API, rebuilt from an allow-list of tags -->
        <UiRichText :html="article.body" />

        <UiDivider />

        <!-- Was this helpful? -->
        <UiStack direction="row" wrap justify="between" align="center">
          <UiText size="sm" tone="muted">
            {{ t('kb.helpful') }}
          </UiText>
          <UiStack direction="row" gap="xs">
            <UiButton
              :variant="vote === KbVote.Yes ? 'primary' : 'subtle'"
              size="sm"
              data-testid="kb-vote-yes"
              @click="vote = KbVote.Yes"
            >
              <ThumbsUp :size="14" :stroke-width="2" class="mr-1.5" />
              {{ t('kb.yes') }}
            </UiButton>
            <UiButton
              :variant="vote === KbVote.No ? 'danger' : 'subtle'"
              size="sm"
              data-testid="kb-vote-no"
              @click="vote = KbVote.No"
            >
              <ThumbsDown :size="14" :stroke-width="2" class="mr-1.5" />
              {{ t('kb.no') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Knowledgebase article detail page.
 * Fetches a single article by ID and renders the full HTML body from the API. An article that does
 * not exist or is not published is a real 404, not a page that says so with a 200; a read that
 * fails any other way keeps the upstream status (502 without an answer), so it is not taken for gone.
 */

import { ArrowLeft, Calendar, Eye, ThumbsUp, ThumbsDown } from 'lucide-vue-next'
import { useContentStore } from '~/stores/content'
import { KbVote } from '~/types/kbvote'
import { SeoPageType } from '~/types/seopagetype'
import { pageLoadError } from '~/utils/pageLoadError'
import { detailTitle } from '~/utils/seoTitle'
import { SITE_KNOWLEDGEBASE_PATH } from '~/constants/siteRoutes'

/** The current route. */
const route = useRoute()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
/** Translates a key in the active language; `locale` is that language. */
const { t, locale } = useI18n()

// The article, read through the content store; the key follows the route, so a new article is
// read, and a language change reads it again.
/** The content store that owns the lists this page renders. */
const content = useContentStore()
/** Whether the read is still running. */
const { pending } = await useAsyncData(
  () => `kb:article:${String(route.params.id)}`,
  () => content.loadArticle(String(route.params.id)),
  { watch: [locale] }
)

/** The article the route names, once the store has it. */
const article = computed(() => content.articleFor(String(route.params.id)))

/** Local helpful vote state (UI only, no API submission) */
const vote = ref<KbVote | null>(null)

// The article is read before the head and the status decision, so a crawler sees the final status:
// 404 when the backend has no such article, the upstream status when the read itself failed.
if (!pending.value && !article.value) {
  throw pageLoadError(content.errorStatus, 'Article not found')
}

// Head tags follow the article, so a new article or a language change never leaves stale tags.
/** The canonical URL of this article in the active language. */
const { canonicalUrl } = useSeo(() => ({
  title: article.value ? detailTitle(article.value.title, t('kb.title')) : undefined,
  description: article.value?.excerpt,
  type: SeoPageType.Article
}))

/** Builder and publisher of the article structured data. */
const { articleSchema, injectSchema } = useSchemaOrg()
injectSchema(() => article.value
  ? [articleSchema({ headline: article.value.title, description: article.value.excerpt, url: canonicalUrl.value })]
  : [])
</script>
