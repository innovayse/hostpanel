<template>
  <UiStack gap="none" data-testid="announcements-page">
    <!-- Hero -->
    <UiPageHero
      :badge="t('announcements.badge')"
      :title="t('announcements.title')"
      :subtitle="t('announcements.subtitle')"
    />

    <!-- Content -->
    <UiSection padding="md">
      <UiStack class="w-full" gap="lg">
        <!-- Month filter -->
        <UiChipTabs
          v-if="months.length > 1"
          v-model="activeMonth"
          :tabs="monthTabs"
          :label="t('announcements.filterLabel')"
          testid="announcements-months"
        />

        <!-- Loading -->
        <UiStack v-if="pending" gap="md" data-testid="announcements-loading">
          <UiSkeleton v-for="i in 4" :key="i" shape="block" class="h-40" />
        </UiStack>

        <!-- Signed out: the API serves announcements to signed-in members only -->
        <UiEmptyState
          v-else-if="needsSignIn"
          :icon="Megaphone"
          :title="t('announcements.signInRequired')"
          :description="t('announcements.signInRequiredDesc')"
          data-testid="announcements-sign-in"
        >
          <UiButton variant="primary" :to="localePath(`${CLIENT_LOGIN_PATH}?${CLIENT_REDIRECT_QUERY}=${SITE_ANNOUNCEMENTS_PATH}`)" data-testid="announcements-sign-in-link">
            {{ t('common.signIn') }}
          </UiButton>
        </UiEmptyState>

        <!-- Error -->
        <UiAlert v-else-if="error" variant="error" data-testid="announcements-error">
          {{ t('announcements.errorDesc') }}
        </UiAlert>

        <!-- Empty -->
        <UiEmptyState
          v-else-if="!filtered.length"
          :icon="Megaphone"
          :title="t('announcements.empty')"
          data-testid="announcements-empty"
        />

        <!-- List -->
        <UiStack v-else gap="md" data-testid="announcements-list">
          <UiLinkCard
            v-for="item in filtered"
            :key="item.id"
            :to="localePath(siteAnnouncementPath(item.id))"
            :testid="`announcement-${item.id}`"
          >
            <UiText
              :as="UiTextTag.Span"
              size="xs"
              tone="muted"
              class="mb-3 flex items-center gap-2"
            >
              <Calendar :size="13" :stroke-width="2" />
              {{ formatDate(item.publishedAt, locale) }}
            </UiText>
            <UiHeading
              :level="2"
              size="subsection"
              class="mb-2 transition-colors group-hover:text-primary-600 dark:group-hover:text-primary-400 group-hover:[.tpl-classic_&]:text-classic-brand"
            >
              {{ item.title }}
            </UiHeading>
            <UiText
              :as="UiTextTag.Span"
              size="xs"
              tone="primary"
              weight="medium"
              class="inline-flex items-center gap-1.5"
            >
              {{ t('announcements.readMore') }}
              <ArrowRight :size="13" :stroke-width="2" class="transition-transform group-hover:translate-x-1" />
            </UiText>
          </UiLinkCard>
        </UiStack>
      </UiStack>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Public announcements list page.
 * Fetches all announcements from the API and supports month-based filtering.
 */

import { CLIENT_LOGIN_PATH, CLIENT_REDIRECT_QUERY } from '~/constants/clientRoutes'
import { SessionCookie } from '~/constants/sessionCookie'
import { Megaphone, Calendar, ArrowRight } from 'lucide-vue-next'
import { useContentStore } from '~/stores/content'
import { formatDate } from '~/utils/formatDate'
import type { PublishedAnnouncement } from '~/types/publishedannouncement'
import type { ChipTab } from '~/types/chiptab'
import { SITE_ANNOUNCEMENTS_PATH, siteAnnouncementPath } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

/** Month-filter value that shows every announcement. */
const ALL_MONTHS = 'all'

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
/** Translates a key in the active language; `locale` is that language. */
const { t, locale } = useI18n()

useSeoMeta({
  title: t('announcements.title'),
  description: t('announcements.subtitle')
})

// The announcements, read through the content store; the read runs again when the language changes.
/** The content store that owns the lists this page renders. */
const content = useContentStore()
/** Whether the read is still running. */
const { pending } = await useAsyncData('announcements:list', () => content.loadAnnouncements(), { watch: [locale] })

/** The sentence explaining why the list could not be read, or `''`. */
const error = computed(() => content.error)

/** The `authed` cookie the sign-in sets; its absence means the visitor has no session. */
const authed = useCookie(SessionCookie.Authed)

/**
 * Whether the refusal is just "not signed in". The API serves published announcements to signed-in
 * members only, while the site footer links this page to every visitor, so a refusal with no
 * session is an invitation to sign in, not a failure worth a "try again later".
 */
const needsSignIn = computed(() => !!error.value && !authed.value)

/** All announcement items from API */
const items = computed<PublishedAnnouncement[]>(() => content.announcements)

/**
 * The month each announcement belongs to, as a label a person reads.
 *
 * Derived from the timestamp rather than by pattern-matching a formatted string: the backend
 * sends an ISO timestamp, not a month name. Parsing the date also makes the labels follow the
 * chosen language, which a regex over English month names could not do.
 *
 * @param value - The announcement's ISO publication timestamp.
 * @returns The month and year in the active locale, or an empty string when unparseable.
 */
const monthLabel = (value: string | undefined): string => {
  if (!value) {
    return ''
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return ''
  }
  return new Intl.DateTimeFormat(locale.value, { month: 'long', year: 'numeric' }).format(parsed)
}

/**
 * The distinct months present, newest first, with "all" in front.
 *
 * Only offered when there is more than one: a single tab beside "all" is a control that cannot
 * change anything.
 */
const months = computed(() => {
  const seen: string[] = []
  for (const item of items.value) {
    const label = monthLabel(item.publishedAt)
    if (label && !seen.includes(label)) {
      seen.push(label)
    }
  }
  return seen.length > 1 ? [ALL_MONTHS, ...seen] : []
})

/** The month chips: "all" reads as its translated label, every other month as its own name. */
const monthTabs = computed<ChipTab[]>(() => months.value.map(month => ({
  value: month,
  label: month === ALL_MONTHS ? t('announcements.allMonths') : month,
  testid: `announcements-month-${month}`,
})))

/** The month filter currently chosen. */
const activeMonth = ref(ALL_MONTHS)

/** Announcements filtered by the active month selection. */
const filtered = computed(() => {
  if (activeMonth.value === ALL_MONTHS) {
    return items.value
  }
  return items.value.filter(item => monthLabel(item.publishedAt) === activeMonth.value)
})
</script>
