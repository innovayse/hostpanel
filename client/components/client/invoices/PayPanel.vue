<template>
  <!-- A transfer is already chosen: the bank details and the reference replace the payment choices. -->
  <UiStack v-if="awaitingBankTransfer" gap="md" data-testid="invoice-pay-panel">
    <UiAlert v-if="bankTransferError" variant="error" data-testid="invoice-bank-transfer-error">{{ bankTransferError }}</UiAlert>
    <ClientInvoicesBankTransferCard v-if="bankTransfer" :details="bankTransfer" />
  </UiStack>

  <UiCard v-else data-testid="invoice-pay-panel">
    <UiStack gap="lg">
      <UiHeading :level="1" size="subsection">{{ $t('invoicePay.makePayment') }}</UiHeading>

      <UiAlert v-if="payError" variant="error" data-testid="invoice-pay-error">{{ payError }}</UiAlert>

      <!-- Nothing to pay through: say so, instead of an empty card. -->
      <UiAlert v-if="!hostedGatewayModule && !bankTransferOffered" variant="info" data-testid="invoice-pay-unavailable">
        {{ $t('invoicePay.noPaymentOption') }}
      </UiAlert>

      <!-- Gateway payment: whichever gateway plugin the deployment enabled. -->
      <template v-if="hostedGatewayModule">
        <UiText size="sm" tone="muted" data-testid="invoice-pay-hosted-hint">{{ $t('invoicePay.hostedHint') }}</UiText>
        <UiButton full-width :loading="submitting" data-testid="invoice-pay-hosted" @click="payWithHostedGateway">
          {{ $t('invoicePay.payByCardHosted') }}
        </UiButton>
      </template>

      <!-- Manual bank transfer: choosing it moves the invoice to waiting and shows the bank details. -->
      <template v-if="bankTransferOffered">
        <UiText size="sm" tone="muted" data-testid="invoice-pay-bank-transfer-hint">{{ $t('invoicePay.bankTransfer.hint') }}</UiText>
        <UiButton
          full-width
          :variant="bankTransferVariant"
          :loading="submitting"
          data-testid="invoice-pay-bank-transfer"
          @click="payByBankTransfer"
        >
          {{ $t('invoicePay.bankTransfer.choose') }}
        </UiButton>
      </template>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PayPanel - the "Make payment" card of the invoice payment page.
 *
 * Offers a gateway button when a gateway plugin is enabled, which hands the browser to the bank, and a bank transfer button
 * when the admin switched the manual transfer on, which moves the invoice to waiting and shows the bank details. Once a
 * transfer is chosen the panel is the bank details card instead; with nothing to pay through it says that online payment is
 * not available. The in-flight flag and the refusal sentence belong to the invoice payment store; this component only
 * renders them and calls its actions.
 */
import { storeToRefs } from 'pinia'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { redirectToGateway } from '~/utils/externalRedirect'
import { UiButtonVariant } from '~/utils/ui/enums'

/** Props of PayPanel. */
interface Props {
  /** The id of the invoice being paid. */
  invoiceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The invoice payment store, loaded by the page before this panel is shown. */
const paymentStore = useInvoicePaymentStore()
/** Reactive state of the store this component renders. */
const {
  submitting,
  error: payError,
  hostedGatewayModule,
  bankTransferOffered,
  bankTransfer,
  bankTransferError,
  invoice
} = storeToRefs(paymentStore)

/** Whether the invoice already waits for a bank transfer, so its details are shown in place of the payment choices. */
const awaitingBankTransfer = computed<boolean>(() => invoice.value?.isBankTransferPending === true)

/** The bank transfer button is the main action unless a card button sits above it, then it is the quieter one. */
const bankTransferVariant = computed<UiButtonVariant>(() =>
  hostedGatewayModule.value ? UiButtonVariant.Secondary : UiButtonVariant.Primary)

/**
 * Starts a hosted-gateway payment and hands the browser to the bank: a redirect, an auto submitted form or a browser step.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `payError`.
 */
const payWithHostedGateway = async (): Promise<void> => {
  if (!hostedGatewayModule.value) {
    return
  }
  const returnUrl = new URL(localePath(`/payment/result?invoice=${props.invoiceId}`), window.location.origin).toString()
  const start = await paymentStore.startGatewayPayment(props.invoiceId, hostedGatewayModule.value, returnUrl)
  if (start) {
    // The store already refused an unsafe answer; this is the last guard before the browser leaves.
    redirectToGateway(start)
  }
}

/**
 * Chooses the manual bank transfer for this invoice; the store then holds the bank details and the panel shows them.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `payError`.
 */
const payByBankTransfer = async (): Promise<void> => {
  await paymentStore.selectBankTransfer(props.invoiceId)
}
</script>
