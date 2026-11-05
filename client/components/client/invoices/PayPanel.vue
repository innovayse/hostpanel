<template>
  <UiCard data-testid="invoice-pay-panel">
    <UiStack gap="lg">
      <UiHeading :level="1" size="subsection">{{ $t('invoicePay.makePayment') }}</UiHeading>

      <UiAlert v-if="payError" variant="error" data-testid="invoice-pay-error">{{ payError }}</UiAlert>

      <!-- Nothing to pay through: say so, instead of an empty card. -->
      <UiAlert v-if="!hostedGatewayModule" variant="info" data-testid="invoice-pay-unavailable">
        {{ $t('invoicePay.noPaymentOption') }}
      </UiAlert>

      <!-- Gateway payment: whichever gateway plugin the deployment enabled. -->
      <template v-else>
        <UiText size="sm" tone="muted" data-testid="invoice-pay-hosted-hint">{{ $t('invoicePay.hostedHint') }}</UiText>
        <UiButton full-width :loading="submitting" data-testid="invoice-pay-hosted" @click="payWithHostedGateway">
          {{ $t('invoicePay.payByCardHosted') }}
        </UiButton>
      </template>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PayPanel - the "Make payment" card of the invoice payment page.
 *
 * Offers a gateway button when a gateway plugin is enabled, which hands the browser to the bank;
 * with no gateway it says that online payment is not available. The in-flight flag and the refusal
 * sentence belong to the invoice payment store; this component only renders them and calls its actions.
 */
import { storeToRefs } from 'pinia'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { redirectToExternal } from '~/utils/externalRedirect'

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
const { submitting, error: payError, hostedGatewayModule } = storeToRefs(paymentStore)

/**
 * Starts a hosted-gateway payment and redirects the browser to the bank.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `payError`.
 */
const payWithHostedGateway = async (): Promise<void> => {
  if (!hostedGatewayModule.value) {
    return
  }
  const returnUrl = new URL(localePath(`/payment/result?invoice=${props.invoiceId}`), window.location.origin).toString()
  const redirectUrl = await paymentStore.startGatewayPayment(props.invoiceId, hostedGatewayModule.value, returnUrl)
  if (redirectUrl) {
    // The store already refused an unsafe address; this is the last guard before the browser leaves.
    redirectToExternal(redirectUrl)
  }
}
</script>
