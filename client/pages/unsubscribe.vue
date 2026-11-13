<template>
  <ClientAuthShell :show-logo="false">
    <div v-if="awaitingConfirmation" class="flex flex-col items-center gap-4 text-center" data-testid="unsubscribe-confirm">
      <UiText tone="muted" size="sm">
        {{ $t('client.unsubscribe.prompt') }}
      </UiText>
      <UiButton :loading="status === UnsubscribeStatus.Pending" full-width data-testid="unsubscribe-confirm-button" @click="confirm">
        {{ $t('client.unsubscribe.confirm') }}
      </UiButton>
    </div>

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
 * Unsubscribe landing page, opened from the link in a campaign mail. Opening it changes nothing, so a link
 * scanner that runs scripts cannot withdraw anyone's consent: the reader confirms with a button, and only
 * then the token from the address goes to the unsubscribe store. The page renders the status and sentence
 * the store holds. Anonymous (no sign-in), carries no navigation link and is kept out of search results.
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

/** Whether the confirm button is shown: before the click, and while its request is open. */
const awaitingConfirmation = computed(() => status.value === UnsubscribeStatus.Idle || status.value === UnsubscribeStatus.Pending)

/**
 * Sends the unsubscribe request for the token in the address. An empty token is sent as it is: the API
 * refuses it with its own sentence, in the reader's language.
 *
 * @returns A promise that settles once the store holds the outcome.
 */
const confirm = async (): Promise<void> => {
  const token = route.query.token
  await unsubscribeStore.unsubscribe(typeof token === 'string' ? token : '')
}
</script>
