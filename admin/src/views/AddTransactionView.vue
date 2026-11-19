<script setup lang="ts">
/**
 * Add transaction: records a payment in or out of the ledger for a client and returns to the
 * ledger once the API accepts it.
 */
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import TransactionForm from '@/components/billing/TransactionForm.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { blankTransactionForm, firstInvoiceId } from '@/lib/transactionForm'
import { useTransactionsStore } from '@/stores/billingTransactions'
import type { TransactionFormValues } from '@/types/transaction'
import { AppRoute } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema from '@/views/AddTransactionView.schema'

const { t } = useI18n()

const router = useRouter()
const store = useTransactionsStore()
const { clients, load: loadClients } = useClientOptions()

/** The form values. */
const values = ref<TransactionFormValues>(blankTransactionForm())

/** Sends the transaction and returns to the ledger when it was accepted. */
const submit = async (): Promise<void> => {
  const ok = await store.create({
    clientId: Number.parseInt(values.value.clientId, 10),
    date: values.value.date ?? '',
    description: values.value.description,
    transactionId: values.value.transactionId,
    invoiceId: firstInvoiceId(values.value.invoiceIds),
    paymentMethod: values.value.paymentMethod,
    amountIn: values.value.amountIn,
    amountOut: values.value.amountOut,
    fees: values.value.fees,
    addToCredit: values.value.addToCredit,
  })
  if (ok) {
    await router.push(AppRoute.BillingTransactions)
  }
}

onMounted(loadClients)
</script>

<template>
  <UiStack data-testid="billing-add-transaction-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />
    <TransactionForm
      v-model="values"
      :columns="schema.columns"
      :clients="clients"
      :submit-label="t('billing.addTransaction.addTransaction')"
      :loading="store.loading"
      :error="store.error"
      :field-errors="store.fieldErrors"
      @submit="submit"
    />
  </UiStack>
</template>
