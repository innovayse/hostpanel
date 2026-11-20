<script setup lang="ts">
/**
 * Edit transaction: loads one ledger transaction into the shared form, saves the changes and
 * returns to the ledger once the API accepts them. The client of a transaction cannot change. A
 * refused save shows the API's sentence in the form; a refused load shows it with a Retry instead
 * of the form, so a blank form is never offered for saving.
 */
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import TransactionForm from '@/components/billing/TransactionForm.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { blankTransactionForm, firstInvoiceId, formFromTransaction } from '@/lib/transactionForm'
import { useTransactionsStore } from '@/stores/billingTransactions'
import type { TransactionFormValues } from '@/types/transaction'
import { AppRoute } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema from '@/views/EditTransactionView.schema'

const { t } = useI18n()

const route = useRoute()
const router = useRouter()
const store = useTransactionsStore()
const { clients, load: loadClients } = useClientOptions()

/** The form values. */
const values = ref<TransactionFormValues>(blankTransactionForm())

/** The sentence of a refused load of the transaction, null when it loaded. */
const loadError = ref<string | null>(null)

/**
 * Reads the id of the transaction from the route.
 *
 * @returns The transaction id.
 */
const transactionId = (): number => Number.parseInt(String(route.params.id), 10)

/**
 * Loads the transaction and records whether the load was refused.
 *
 * @returns Resolves when the transaction has settled.
 */
const load = async (): Promise<void> => {
  await store.fetchById(transactionId())
  loadError.value = store.error
}

/** Saves the changes and returns to the ledger when they were accepted. */
const submit = async (): Promise<void> => {
  const ok = await store.update(transactionId(), {
    date: values.value.date ?? '',
    description: values.value.description,
    transactionId: values.value.transactionId,
    invoiceId: firstInvoiceId(values.value.invoiceIds),
    paymentMethod: values.value.paymentMethod,
    amountIn: values.value.amountIn,
    amountOut: values.value.amountOut,
    fees: values.value.fees,
  })
  if (ok) {
    await router.push(AppRoute.BillingTransactions)
  }
}

watch(() => store.currentTransaction, (transaction) => {
  if (transaction !== null) {
    values.value = formFromTransaction(transaction)
  }
})

/** Loads the client options and the transaction. */
onMounted(() => {
  void loadClients()
  void load()
})
</script>

<template>
  <UiStack data-testid="billing-edit-transaction-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />
    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-edit-transaction-load-error"
      @retry="load"
    />
    <TransactionForm
      v-else
      v-model="values"
      :columns="schema.columns"
      lock-client
      :clients="clients"
      :submit-label="t('billing.editTransaction.updateTransaction')"
      :loading="store.loading"
      :error="store.error"
      :field-errors="store.fieldErrors"
      @submit="submit"
    />
  </UiStack>
</template>
