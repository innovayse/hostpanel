<template>
  <UiCard data-testid="invoice-pay-panel">
    <UiStack gap="lg">
      <UiHeading :level="1" size="subsection">{{ $t('invoicePay.makePayment') }}</UiHeading>

      <UiAlert v-if="paySuccess" variant="success" :title="$t('invoicePay.paymentSuccess')" data-testid="invoice-pay-success">
        <UiLink data-testid="pay-panel-invoice-link" :to="invoicePath">{{ $t('invoicePay.viewInvoice') }}</UiLink>
      </UiAlert>

      <UiAlert v-if="payError" variant="error" data-testid="invoice-pay-error">{{ payError }}</UiAlert>

      <template v-if="!paySuccess">
        <!-- Suppressed entirely when there is no saved card, so the row is not an empty box. -->
        <ClientInvoicesPayMethodSelector
          v-if="savedMethods.length > 0"
          v-model="selectedMethodId"
          :saved-methods="savedMethods"
        />

        <!-- No saved card: say how to pay, or that there is no way to pay online at all. -->
        <UiText v-if="savedMethods.length === 0 && hostedGatewayModule" size="sm" tone="muted" data-testid="invoice-pay-no-cards">
          {{ $t('invoicePay.noSavedCardsHosted') }}
        </UiText>
        <UiAlert v-if="savedMethods.length === 0 && !hostedGatewayModule" variant="info" data-testid="invoice-pay-unavailable">
          {{ $t('invoicePay.noPaymentOption') }}
        </UiAlert>

        <!-- Hosted-gateway payment: whichever gateway plugin the deployment enabled. -->
        <UiButton v-if="hostedGatewayModule" full-width :loading="submitting" data-testid="invoice-pay-hosted" @click="payWithHostedGateway">
          {{ $t('invoicePay.payByCardHosted') }}
        </UiButton>

        <!-- Submit: only when a method is actually selected. -->
        <UiButton v-if="selectedMethodId !== null" size="lg" :loading="submitting" data-testid="invoice-pay-submit" @click="submitPayment">
          <CreditCard v-if="!submitting" :size="18" :stroke-width="2" class="mr-2" />
          {{ submitting ? $t('invoicePay.processing') : $t('invoicePay.submitPayment') }}
        </UiButton>
      </template>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PayPanel - the "Make payment" card of the invoice payment page.
 *
 * Offers the saved cards and a hosted-gateway button when a gateway plugin is enabled. Without a
 * saved card it says to use that button, or, with no gateway either, that online payment is not
 * available. The
 * in-flight flag, the success flag and the refusal sentence belong to the invoice payment store;
 * this component only renders them and calls its actions.
 */
import { clientInvoicePath } from '~/constants/clientRoutes'
import { CreditCard } from 'lucide-vue-next'
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
const { submitting, paySuccess, error: payError, savedMethods, hostedGatewayModule } = storeToRefs(paymentStore)

/** The invoice's own page. */
const invoicePath = computed(() => localePath(clientInvoicePath(props.invoiceId)))

/** The chosen saved method's id, or null when there is no saved card. A hosted-gateway payment is a separate button. */
const selectedMethodId = ref<string | null>(savedMethods.value[0]?.id ?? null)

/**
 * Pays the invoice with the selected saved method through the API. Each click is a new pay
 * action with its own attempt id, so a card the gateway declined can be charged again at once;
 * a retry of the same request carries the same id and is one charge.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `payError`.
 */
const submitPayment = async (): Promise<void> => {
  paymentStore.clearError()

  const method = savedMethods.value.find(saved => saved.id === selectedMethodId.value)
  // Nothing selectable: the submit button is not rendered in this state.
  if (!method) {
    return
  }

  await paymentStore.payWithSavedMethod(props.invoiceId, method.gatewayModuleId, method.id, crypto.randomUUID())
}

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
