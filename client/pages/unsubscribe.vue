<template>
  <ClientAuthShell :show-logo="false">
    <UiText v-if="status === UnsubscribeStatus.Pending" tone="muted" size="sm" class="text-center" data-testid="unsubscribe-pending">
      {{ $t('client.unsubscribe.working') }}
    </UiText>

    <ClientOutcomePanel
      v-else-if="status === UnsubscribeStatus.Done"
      :icon="CheckCircle"
      tone="success"
      :title="$t('client.unsubscribe.doneTitle')"
      :message="$t('client.unsubscribe.doneMessage')"
      data-testid="unsubscribe-done"
    />

    <ClientOutcomePanel
      v-else
      :icon="AlertCircle"
      tone="danger"
      :title="$t('client.unsubscribe.failedTitle')"
      :message="message"
      data-testid="unsubscribe-failed"
    />
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Unsubscribe landing page, opened from the link in a campaign mail: reads the token out of the
 * address and hands it to the unsubscribe store, then renders the status and sentence the store
 * holds. Anonymous (no sign-in), carries no navigation link and is kept out of search results.
 */
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useUnsubscribeStore } from '~/stores/unsubscribe'
import { UnsubscribeStatus } from '~/types/unsubscribestatus'

definePageMeta({ layout: false })

useSeoMeta({ robots: 'noindex, nofollow' })

/** The current route, read for its query. */
const route = useRoute()

/** Unsubscribe store; owns the request, its status and the refusal sentence this file renders. */
const unsubscribeStore = useUnsubscribeStore()

/** Which of the three panels the page shows, and the failure sentence, both owned by the store. */
const { status, error: message } = storeToRefs(unsubscribeStore)

onMounted(async () => {
  const token = route.query.token
  await unsubscribeStore.unsubscribe(typeof token === 'string' ? token : '')
})
</script>
