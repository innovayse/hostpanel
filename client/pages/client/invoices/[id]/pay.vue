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
        <UiCard v-if="optionsError && !isBankTransferPending" data-testid="invoice-pay-options-error">
          <UiStack gap="md">
            <UiAlert variant="error">{{ optionsError }}</UiAlert>
            <UiStack direction="row" gap="none">
              <UiButton variant="outline" :loading="retrying" data-testid="invoice-pay-options-retry" @click="retryOptions">
                {{ $t('invoicePay.retry') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiCard>
        <ClientInvoicesPayPanel v-else :invoice-id="invoiceId" />
      </template>
      <template #end>
        <ClientInvoicesPaySummary :invoice="invoice" />
      </template>
    </UiSplit>

    <UiAlert v-if="showSecurityNotice" variant="warning" :icon-size="14" data-testid="invoice-pay-security">{{ $t('invoicePay.securityNotice') }}</UiAlert>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Invoice payment page: the hosted-gateway payment panel beside the invoice recap. The invoice and
 * the available gateways are read through the invoice payment store, which also owns the in-flight
 * flag and the refusal sentence. A gateway list that could not be read shows an error with a retry,
 * which is not the same as "no gateway is enabled" (the panel says that itself).
 */
import { CLIENT_INVOICES_PATH, clientInvoicePath } from '~/constants/clientRoutes'
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
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

// Invoice and gateways are read in parallel. The first plugin-backed
// gateway (not a fixed module id) is the hosted one, so a second gateway plugin is not ignored.
// The bank details are read after the invoice, because only an invoice that waits for a transfer has any.
/** Loading flag of the data this page reads. */
const { pending } = await useAsyncData(
  `client-invoice-pay-${invoiceId}`,
  () => Promise.all([
    paymentStore.loadInvoice(invoiceId).then(() => paymentStore.loadBankTransfer(invoiceId)),
    paymentStore.loadPaymentOptions()
  ])
)

/** The gateway-list refusal and the hosted gateway, if any. */
const { optionsError, hostedGatewayModule } = storeToRefs(paymentStore)

/** Whether the gateway list is being read again after a failed read. */
const retrying = ref(false)

/**
 * Reads the gateway list again after a failed read.
 *
 * @returns Resolves when the read has ended.
 */
const retryOptions = async (): Promise<void> => {
  retrying.value = true
  try {
    await paymentStore.loadPaymentOptions()
  } finally {
    retrying.value = false
  }
}

/** Whether the invoice already waits for a bank transfer, so the payment choices and the gateway notice do not apply. */
const isBankTransferPending = computed(() => invoice.value?.isBankTransferPending === true)

/** The security notice describes the hosted gateway's page, so it is shown only when one exists and the payer can still use it. */
const showSecurityNotice = computed(() =>
  !pending.value && invoice.value !== null && !isInvoicePaid(invoice.value) && !isBankTransferPending.value && hostedGatewayModule.value !== null)

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
