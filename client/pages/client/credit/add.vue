<template>
  <UiStack gap="lg" data-testid="client-credit-add">
    <UiBackLink :to="CLIENT_CREDIT_PATH" :label="$t('client.credit.add.back')" testid="credit-add-back" />

    <UiPageHeader :title="$t('client.credit.add.title')" :description="$t('client.credit.add.subtitle')" :icon="Wallet" class="!mb-0" />

    <UiAlert v-if="store.error" variant="error" data-testid="credit-add-load-error">{{ store.error }}</UiAlert>

    <UiSkeleton v-else-if="!store.credit" shape="block" class="h-48" data-testid="credit-add-loading" />

    <ClientCreditAddFundsForm
      v-else
      :minimum="store.credit.topUpMinimum"
      :maximum="store.credit.topUpMaximum"
      :currency="store.credit.currency"
      :busy="store.topUpBusy"
      :error="store.topUpError"
      @submit="addFunds"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Add funds: the client chooses an amount inside the admin's limits, the top-up invoice is raised, and the client is sent to pay
 * it on the ordinary invoice payment page (a bank transfer or an installed gateway). The balance rises when that invoice is paid,
 * not before, and the page says so.
 */
import { Wallet } from 'lucide-vue-next'
import { CLIENT_CREDIT_PATH, clientInvoicePayPath } from '~/constants/clientRoutes'
import { useCreditStore } from '~/stores/credit'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The credit store: the limits to hold the amount to, and the creation of the top-up invoice. */
const store = useCreditStore()

await useAsyncData('client-credit-add', () => store.load(1))

/**
 * Raises the top-up invoice for the chosen amount and sends the client to pay it.
 *
 * @param amount - The amount the client chose.
 * @returns Resolves once the client has been sent on, or the refusal is on screen.
 */
const addFunds = async (amount: number): Promise<void> => {
  const invoiceId = await store.createTopUp(amount)
  if (invoiceId !== null) {
    await navigateTo(localePath(clientInvoicePayPath(invoiceId)))
  }
}
</script>
