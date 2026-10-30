<template>
  <UiStack gap="lg">
    <UiBreadcrumbs :items="crumbs" container-class="mb-0" />

    <UiSplit v-if="pending" ratio="start-wide">
      <template #start>
        <UiStack>
          <UiSkeleton v-for="i in 4" :key="i" shape="block" class="h-14" />
        </UiStack>
      </template>
      <template #end>
        <UiSkeleton shape="block" class="h-80" />
      </template>
    </UiSplit>

    <UiEmptyState v-else-if="!invoice" :icon="AlertCircle" :title="$t('client.invoices.notFound')" />

    <UiEmptyState
      v-else-if="isInvoicePaid(invoice)"
      :icon="CheckCircle"
      :title="$t('invoicePay.alreadyPaid')"
      data-testid="invoice-already-paid"
    >
      <UiButton :to="invoicePath" variant="subtle" data-testid="invoice-view">{{ $t('invoicePay.viewInvoice') }}</UiButton>
    </UiEmptyState>

    <UiSplit v-else ratio="start-wide">
      <template #start>
        <ClientInvoicesPayPanel :invoice-id="invoiceId" />
      </template>
      <template #end>
        <ClientInvoicesPaySummary :invoice="invoice" />
      </template>
    </UiSplit>

    <UiAlert variant="warning" :icon-size="14" data-testid="invoice-pay-security">{{ $t('invoicePay.securityNotice') }}</UiAlert>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Invoice payment page: the payment form beside the invoice recap. The invoice, the saved
 * payment methods and the available gateways are read through the invoice payment store, which
 * also owns the in-flight flag, the success flag and the refusal sentence.
 */
import { CLIENT_INVOICES_PATH, clientInvoicePath } from '~/constants/clientRoutes'
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { isInvoicePaid } from '~/utils/invoice'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()
/** The current route; its params name the record shown. */
const route = useRoute()
/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The id of the invoice being paid. */
const invoiceId = route.params.id as string

/** The invoice payment store; reset so a previous visit's success or error does not linger. */
const paymentStore = useInvoicePaymentStore()
paymentStore.reset()

// Invoice, saved payment methods and gateways are read in parallel. The first plugin-backed
// gateway (not a fixed module id) is the hosted one, so a second gateway plugin is not ignored.
/** Loading flag of the data this page reads. */
const { pending } = await useAsyncData(
  `client-invoice-pay-${invoiceId}`,
  () => Promise.all([paymentStore.loadInvoice(invoiceId), paymentStore.loadPaymentOptions()])
)

/** The invoice being paid; null until loaded or after a failed read. */
const invoice = computed(() => paymentStore.invoice)

/** The invoice's own page. */
const invoicePath = computed(() => localePath(clientInvoicePath(invoiceId)))

/** The breadcrumb trail: invoices, this invoice, the payment step. */
const crumbs = computed(() => [
  { label: t('client.invoices.title'), to: localePath(CLIENT_INVOICES_PATH) },
  { label: t('invoicePay.invoiceNum', { id: invoiceId }), to: invoicePath.value },
  { label: t('invoicePay.makePayment') },
])
</script>
