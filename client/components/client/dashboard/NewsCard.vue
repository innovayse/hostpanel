<template>
  <UiCard data-testid="dashboard-news">
    <UiCardHeader :title="$t('client.dashboard.announcements')">
      <UiButton
        v-if="billingPortalUrl"
        size="sm"
        variant="subtle"
        :href="`${billingPortalUrl}/announcements.php`"
        target="_blank"
        data-testid="dashboard-news-all"
      >
        {{ $t('client.dashboard.viewAll') }}
        <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
      </UiButton>
    </UiCardHeader>

    <UiEmptyState v-if="!announcements.length" :icon="Megaphone" :title="$t('client.dashboard.noAnnouncements')" class="!py-6" data-testid="dashboard-news-empty" />

    <UiStack v-else gap="sm">
      <UiStack
        v-for="announcement in announcements.slice(0, 3)"
        :key="announcement.id"
        gap="none"
        class="rounded-xl bg-gray-50 p-3 dark:bg-white/5"
        :data-testid="`dashboard-news-${announcement.id}`"
      >
        <UiText size="sm" weight="medium">{{ announcement.title }}</UiText>
        <UiText size="xs" tone="subtle" class="mt-0.5">{{ formatDate(announcement.publishedAt) }}</UiText>
      </UiStack>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The dashboard's "Recent News" card: the latest three published announcements.
 *
 * The server route behind the store's `recentNews` returns only published rows, with no
 * editorial `isPublished` flag; the card shows each one's title and publication date.
 */
import { ArrowRight, Megaphone } from 'lucide-vue-next'
import { useContentStore } from '~/stores/content'
import { formatDate } from '~/utils/formatDate'

/** Runtime config; its public half carries the auth mode and the portal URLs. */
const config = useRuntimeConfig()

/** Base URL of the legacy billing portal; the "view all" link is hidden when it is not configured. */
const billingPortalUrl = config.public.billingPortalUrl as string | undefined

/** Content store: the announcements the dashboard shows. */
const contentStore = useContentStore()

/** Published announcements for the card. */
const announcements = computed(() => contentStore.recentNews)
</script>
