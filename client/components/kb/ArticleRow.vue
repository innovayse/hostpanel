<template>
  <!-- Knowledgebase article list row -->
  <UiLink
    :to="localePath(siteKnowledgebaseArticlePath(article.id))"
    tone="inherit"
    :underline="false"
    :testid="`kb-article-${article.id}`"
    class="group flex items-start gap-3 px-5 py-4 hover:bg-gray-50 dark:hover:bg-white/5 [.tpl-classic_&]:hover:bg-classic-surface-2"
  >
    <FileText :size="16" :stroke-width="2" class="mt-0.5 flex-shrink-0 text-primary-700 dark:text-primary-400 [.tpl-classic_&]:text-classic-brand" />
    <UiStack gap="none" class="min-w-0">
      <UiText
        :as="UiTextTag.Span"
        size="sm"
        weight="medium"
        tone="inherit"
        truncate
        class="block text-primary-700 transition-colors group-hover:text-primary-800 dark:text-primary-400 dark:group-hover:text-primary-300 [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink [.tpl-classic_&]:group-hover:text-classic-brand"
        data-testid="kb-article-title"
      >
        {{ article.title }}
      </UiText>
      <UiText
        v-if="article.excerpt"
        :as="UiTextTag.Span"
        size="xs"
        tone="muted"
        class="mt-0.5 line-clamp-1 block [.tpl-classic_&]:text-classic-muted"
        data-testid="kb-article-excerpt"
      >
        {{ article.excerpt }}…
      </UiText>
    </UiStack>
  </UiLink>
</template>

<script setup lang="ts">
/**
 * Reusable article list row for the knowledgebase index and category pages.
 * Displays the title and the plain-text excerpt the backend lists. Under the classic template the row is charcoal
 * text on a neutral hover with the brand colour for the icon and the hovered title.
 */

import { FileText } from 'lucide-vue-next'
import { siteKnowledgebaseArticlePath } from '~/constants/siteRoutes'
import type { KbArticleSummary } from '~/types/kbarticlesummary'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of ArticleRow. */
interface Props {
  /** Article data. */
  article: KbArticleSummary
}

defineProps<Props>()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
</script>
