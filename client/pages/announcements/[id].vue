<template>
  <UiSection padding="lg" data-testid="announcement-page">
    <UiStack class="w-full" gap="lg">
      <!-- Back link -->
      <UiLink
        :to="localePath(SITE_ANNOUNCEMENTS_PATH)"
        tone="muted"
        :underline="false"
        class="inline-flex items-center gap-2 self-start text-sm"
        testid="announcement-back"
      >
        <ArrowLeft :size="15" :stroke-width="2" />
        {{ t('announcements.backToList') }}
      </UiLink>

      <!-- Loading -->
      <UiStack v-if="pending" gap="md" data-testid="announcement-loading">
        <UiSkeleton shape="block" class="h-8 w-2/3" />
        <UiSkeleton class="w-1/4" />
        <UiSkeleton :lines="6" class="mt-8" />
      </UiStack>

      <!-- Content -->
      <UiStack v-else-if="item" as="article" gap="lg">
        <!-- Date -->
        <UiText
          as="span"
          size="xs"
          tone="muted"
          class="flex items-center gap-2"
          data-testid="announcement-date"
        >
          <Calendar :size="13" :stroke-width="2" />
          {{ t('announcements.published') }}: {{ formatDate(item.publishedAt, locale) }}
        </UiText>

        <!-- Title -->
        <UiHeading :level="1" data-testid="announcement-heading">
          {{ item.title }}
        </UiHeading>

        <UiDivider />

        <!-- Body (HTML from the API, rebuilt from an allow-list of tags) -->
        <UiRichText :html="item.content" />
      </UiStack>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Public announcement detail page.
 * Loads a single announcement by id from the public detail endpoint and renders the full HTML body.
 * An announcement that does not exist or is not published is a real 404; a read that fails any
 * other way keeps the upstream status (502 without an answer).
 */

import { ArrowLeft, Calendar } from 'lucide-vue-next'
import { useContentStore } from '~/stores/content'
import { SeoPageType } from '~/types/seopagetype'
import { formatDate } from '~/utils/formatDate'
import { pageLoadError } from '~/utils/pageLoadError'
import { detailTitle, plainDescription } from '~/utils/seoTitle'
import { SITE_ANNOUNCEMENTS_PATH } from '~/constants/siteRoutes'
import type { PublishedAnnouncement } from '~/types/publishedannouncement'

/** The reader's language, so the publication date is written the way they read dates. */
const { t, locale } = useI18n()

/** The current route. */
const route = useRoute()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

/** The content store that owns the announcements this page renders. */
const content = useContentStore()
// The announcement is read by id, so one outside the first page of the list is still found; the
// key follows the route, and a language change reads it again.
/** Whether the read is still running. */
const { pending } = await useAsyncData(
  () => `announcement:${String(route.params.id)}`,
  () => content.loadAnnouncement(String(route.params.id)),
  { watch: [locale] }
)

/** The announcement the route names, once the store has it. */
const item = computed<PublishedAnnouncement | null>(() => content.announcementFor(String(route.params.id)))

// The announcement is read before the head and the status decision, so a crawler sees the final
// status: 404 when the backend has no such announcement, the upstream status when the read failed.
if (!pending.value && !item.value) {
  throw pageLoadError(content.errorStatus, 'Announcement not found')
}

// Head tags follow the announcement, so a new one or a language change never leaves stale tags.
/** The canonical URL of this announcement in the active language. */
const { canonicalUrl } = useSeo(() => ({
  title: item.value ? detailTitle(item.value.title, t('announcements.title')) : undefined,
  description: item.value ? plainDescription(item.value.content) : undefined,
  type: SeoPageType.Article
}))

/** Builder and publisher of the article structured data. */
const { articleSchema, injectSchema } = useSchemaOrg()
injectSchema(() => item.value
  ? [articleSchema({
    headline: item.value.title,
    description: plainDescription(item.value.content),
    url: canonicalUrl.value,
    datePublished: item.value.publishedAt
  })]
  : [])
</script>
