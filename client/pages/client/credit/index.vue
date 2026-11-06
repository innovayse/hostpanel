<template>
  <UiStack gap="lg" data-testid="client-credit">
    <UiPageHeader :title="$t('client.credit.title')" :description="$t('client.credit.subtitle')" :icon="Wallet" class="!mb-0" />

    <!-- Not a customer account: the API said so with a code, so this is one explanation with somewhere to go. -->
    <UiAlert v-if="store.error" variant="error" data-testid="credit-error">
      <UiStack direction="row" justify="between" align="center" gap="sm" wrap>
        <UiText as="span" size="sm">{{ store.error }}</UiText>
        <UiButton
          :variant="UiButtonVariant.Secondary"
          size="sm"
          :loading="store.loading"
          data-testid="credit-error-retry"
          @click="store.load(store.requestedPage)"
        >
          {{ $t('common.retry') }}
        </UiButton>
      </UiStack>
    </UiAlert>

    <template v-else>
      <ClientCreditBalanceCard :credit="store.credit" :loading="store.loading && !store.credit" />

      <template v-if="store.credit">
        <ClientCreditHistoryTable :entries="store.credit.entries.items" :currency="store.credit.currency" />
        <UiPagination
          v-if="store.credit.entries.totalPages > 1"
          :model-value="store.page"
          :total-pages="store.credit.entries.totalPages"
          :per-page="CREDIT_PAGE_SIZE"
          :page-sizes="[CREDIT_PAGE_SIZE]"
          @update:model-value="store.load"
        />
      </template>
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Credit: the client's balance with the button to add funds, and the history of every movement of the balance, a page at a time.
 *
 * Everything shown is the backend's: the balance, the currency and each line's signed change and resulting balance. Movements older
 * than the history are not listed; the balance stays right regardless. Loading and failure states come from the credit store.
 */
import { Wallet } from 'lucide-vue-next'
import { CREDIT_PAGE_SIZE, useCreditStore } from '~/stores/credit'
import { UiButtonVariant } from '~/utils/ui/enums'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** The credit store that owns the credit and its loading and failure state. */
const store = useCreditStore()

await useAsyncData('client-credit', () => store.load(1))
</script>
