<template>
  <UiCard data-testid="service-details">
    <UiCardHeader :title="$t('client.services.serviceDetails')" :icon="LayoutGrid" />
    <UiGrid as="dl" cols="2" gap="lg">
      <UiDescriptionItem :label="$t('client.services.plan')" :value="service.productName" value-class="font-medium" />
      <UiDescriptionItem :label="$t('client.services.domain')" :value="service.domain || null" />
      <UiDescriptionItem :label="$t('client.services.status')">
        <ClientStatusBadge :status="service.status" />
      </UiDescriptionItem>
      <UiDescriptionItem :label="$t('client.services.billingCycle')" :value="$t(`products.pricing.cycles.${BILLING_CYCLE_I18N_KEY[service.billingCycle]}`)" />
      <UiDescriptionItem :label="$t('client.services.regDate')" :value="formatDate(service.createdAt)" />
      <UiDescriptionItem :label="$t('client.services.nextDueDate')" :value="formatDate(service.nextRenewalAt)" />
      <UiDescriptionItem :label="$t('client.services.billingAmount')" :value="formatCurrency(service.price, currency)" value-class="font-medium" />
      <UiDescriptionItem :label="$t('client.services.firstPayment')" :value="formatCurrency(service.firstPaymentAmount, currency)" />
      <UiDescriptionItem :label="$t('client.services.paymentMethod')" :value="service.paymentMethod" value-class="capitalize" />
    </UiGrid>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServiceDetailsCard: the plan, domain, status, billing cycle, dates and amounts of a service as
 * a description list.
 */
import { LayoutGrid } from 'lucide-vue-next'
import { BILLING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import type { ClientService } from '~/types/clientservice'
import type { CurrencyInfo } from '~/utils/formatCurrency'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'

/** Props of ServiceDetailsCard. */
interface Props {
  /** The service being shown. */
  service: ClientService
  /** What the account says the amounts are denominated in. */
  currency: CurrencyInfo
}

defineProps<Props>()
</script>
