<template>
  <UiStack align="center" justify="center" class="min-h-[60dvh] px-4" data-testid="order-success">
    <!-- Confirmation card: an intrinsically small card, not page layout. -->
    <UiStack gap="lg" class="w-full max-w-md">
      <ClientOutcomePanel
        :icon="CheckCircle"
        tone="success"
        :heading-level="UiHeadingLevel.H1"
        :title="$t('checkout.success.title')"
        :message="$t('checkout.success.description')"
      />

      <!-- Order details -->
      <UiCard padding="md" data-testid="order-success-details">
        <UiStack gap="sm">
          <ClientSummaryRow :label="$t('checkout.success.orderNumber')" :value="orderNumber" testid="order-success-number" />
          <ClientSummaryRow :label="$t('checkout.success.status')">
            <UiBadge variant="success" rounded>{{ $t('checkout.success.paid') }}</UiBadge>
          </ClientSummaryRow>
          <ClientSummaryRow :label="$t('checkout.success.amount')" :value="amount" testid="order-success-amount" />
          <ClientSummaryRow v-if="domainName" :label="$t('checkout.success.domain')" :value="domainName" testid="order-success-domain" />
          <ClientSummaryRow v-if="domainAction" :label="$t('checkout.success.action')" testid="order-success-action">
            <UiBadge :variant="domainAction === DomainOrderAction.Register ? 'success' : 'info'" rounded>
              {{ domainAction === DomainOrderAction.Register ? $t('checkout.success.registration') : $t('checkout.success.transfer') }}
            </UiBadge>
          </ClientSummaryRow>
        </UiStack>
      </UiCard>

      <!-- Where to go next -->
      <UiStack gap="sm">
        <UiButton
          v-if="domainName"
          :to="localePath(CLIENT_DOMAINS_PATH)"
          size="lg"
          full-width
          data-testid="order-success-manage-domain"
        >
          {{ $t('checkout.success.manageDomain') }}
        </UiButton>
        <UiButton v-if="!digitalOnly" size="lg" full-width data-testid="order-success-setup-service" @click="openServices">
          {{ $t('checkout.success.setupService') }}
        </UiButton>
        <UiButton
          :to="localePath(CLIENT_INVOICES_PATH)"
          variant="subtle"
          size="lg"
          full-width
          data-testid="order-success-invoices"
        >
          {{ $t('checkout.success.viewInvoices') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Payment success page shown after a successful checkout: the order confirmation and links on to
 * the service setup (left out for an order of digital goods only), the domain and the invoices.
 */
import { CLIENT_DOMAINS_PATH, CLIENT_INVOICES_PATH, CLIENT_SERVICES_PATH } from '~/constants/clientRoutes'
import { CheckCircle } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import { DomainOrderAction } from '~/types/domainorderaction'
import { UiHeadingLevel } from '~/utils/ui/enums'
import { formatCurrency } from '~/utils/formatCurrency'

definePageMeta({ middleware: 'client-auth' })

/** What the page shows where a value is missing. */
const DASH = '—'

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
/** The current route, read for its query and path. */
const route = useRoute()
/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()
/** Invoice store: reads the order's invoice, the authority for what was charged. */
const invoiceStore = useInvoicePaymentStore()

/** Order number from query params. */
const orderNumber = computed(() => (route.query.order as string) || DASH)

/** The order's invoice id from query params; absent when the link carries none. */
const invoiceId = computed(() => (route.query.invoice as string) || null)

/** The invoice total in the invoice's own currency; a dash until the invoice is read or when there is none. */
const amount = computed(() => {
  const invoice = invoiceStore.invoice
  return invoice ? formatCurrency(invoice.total, { code: invoice.currency }) : DASH
})

/** Domain name from query params (set when a domain was purchased). */
const domainName = computed(() => (route.query.domain as string) || null)

/** Domain action from query params, when a domain was registered or transferred. */
const domainAction = computed(() => (route.query.action as DomainOrderAction) || null)

/** True when the order held only digital goods: they are delivered, so there is no service to set up. */
const digitalOnly = computed(() => route.query.digital === '1')

onMounted(async () => {
  if (invoiceId.value) {
    await invoiceStore.loadInvoice(invoiceId.value)
  }
})

/**
 * Loads the services afresh, then opens the services page.
 *
 * The order just created one, so the list the store cached before checkout is stale and the
 * services page would otherwise show it without the new service.
 *
 * @returns Nothing.
 */
const openServices = async (): Promise<void> => {
  await store.fetchServices(true)
  await navigateTo(localePath(CLIENT_SERVICES_PATH))
}
</script>
