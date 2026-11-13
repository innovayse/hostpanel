<template>
  <UiCard v-if="renewResult" data-testid="domain-renew-success">
    <UiEmptyState :icon="CheckCircle" :title="$t('client.domains.renewOrderPlaced')" :description="$t('client.domains.renewOrderDesc')">
      <UiButton :to="clientInvoicePath(renewResult.invoiceId)" data-testid="domain-renew-pay">
        <CreditCard :size="14" :stroke-width="2" class="mr-2" />
        {{ $t('client.domains.renewPayInvoice') }}
      </UiButton>
    </UiEmptyState>
  </UiCard>

  <UiCard v-else data-testid="domain-renew">
    <UiCardHeader :title="$t('client.domains.tabRenew')" :icon="RefreshCw" />

    <UiStack direction="row" align="center" justify="between" class="pb-4 border-b border-gray-100 dark:border-white/10 mb-5">
      <UiStack gap="none">
        <UiText weight="semibold">{{ fullName }}</UiText>
        <UiText size="xs" tone="muted">{{ $t('client.domains.renewExpiry') }} {{ formatDate(domain.expiresAt) }}</UiText>
      </UiStack>
      <UiBadge :variant="standing.tone" size="sm">{{ standing.label }}</UiBadge>
    </UiStack>

    <UiForm data-testid="domain-renew-form" :error="renewError" spacing="sm" @submit="placeRenewal">
      <UiSelect
        :model-value="years"
        :label="$t('client.domains.renewPeriodLabel')"
        :placeholder="$t('client.domains.renewPeriodPlaceholder')"
        :options="periodOptions"
        size="sm"
        testid="domain-renew-period"
        @update:model-value="years = Number($event)"
      />

      <UiCard padding="sm" data-testid="domain-renew-summary">
        <UiHeading :level="3" size="subsection" class="!text-sm mb-3">{{ $t('client.domains.renewOrderSummary') }}</UiHeading>
        <UiStack gap="sm">
          <UiKeyValue :label="$t('client.domains.renewDomain')" :value="fullName" />
          <UiKeyValue :label="$t('client.domains.renewPeriod')" :value="`${years} ${$t('client.domains.renewYear')}`" />
          <UiKeyValue :label="$t('client.domains.renewAmount')" :value="formatCurrency(domain.recurringAmount * years, { code: domain.priceCurrency })" strong />
        </UiStack>
      </UiCard>

      <UiSelect
        v-if="payOptions.length"
        v-model="payMethod"
        :label="$t('client.domains.renewPaymentLabel')"
        :placeholder="$t('client.domains.renewPaymentPlaceholder')"
        :options="payOptions"
        size="sm"
        testid="domain-renew-payment"
      />

      <template #actions>
        <UiButton type="submit" :loading="renewSaving" :disabled="!payMethod" full-width data-testid="domain-renew-submit">
          <RefreshCw v-if="!renewSaving" :size="14" :stroke-width="2" class="mr-1.5" />
          {{ renewSaving ? $t('client.domains.renewPlacing') : $t('client.domains.renewBtn') }}
        </UiButton>
      </template>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainRenewPanel. Orders the renewal of a domain: period, order summary and payment method;
 * once the order is placed it shows a link to the invoice. The saved payment methods are read on
 * first need; the order, its in-flight flag and the API's refusal belong to the domains store.
 */
import { clientInvoicePath } from '~/constants/clientRoutes'
import { computed, onMounted, ref } from 'vue'
import { CheckCircle, CreditCard, RefreshCw } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'
import type { DomainDetail } from '~/types/domaindetail'
import { DomainStatus } from '~/types/domainstatus'
import { isExpiringSoon } from '~/utils/domainExpiry'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { toPaymentMethodOptions } from '~/utils/paymentMethodOptions'
import { buildYearOptions } from '~/utils/yearOptions'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of DomainRenewPanel. */
interface Props {
  /** The domain being renewed. */
  domain: DomainDetail
  /** The id of the domain. */
  domainId: string
  /** The full domain name. */
  fullName: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the order flags, the saved payment methods and the refusal sentence. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { renewSaving, renewError, renewResult } = storeToRefs(store)

/** The renewal period in years. */
const years = ref(1)

/** The gateway name of the chosen payment method. */
const payMethod = ref('')

/** The period choices. */
const periodOptions = computed(() => buildYearOptions(t('client.domains.renewYear'), t('client.domains.renewYears')))

/** The saved payment methods as select options. */
const payOptions = computed(() => toPaymentMethodOptions(store.paymentMethods))

/** Where the domain stands: past its renewable period, expiring soon, or fine. */
const standing = computed(() => {
  if (props.domain.status === DomainStatus.Expired) {
    return { tone: 'danger' as const, label: t('client.domains.renewPastPeriod') }
  }
  if (isExpiringSoon(props.domain.expiresAt)) {
    return { tone: 'warning' as const, label: t('client.domains.renewExpiringSoon') }
  }
  return { tone: 'default' as const, label: t('client.domains.renewActive') }
})

// The payment methods are read when the panel opens, unless an earlier visit already did.
onMounted(async () => {
  if (!store.paymentMethods.length) {
    await store.loadPaymentMethods()
  }
  const [first] = payOptions.value
  if (first && !payMethod.value) {
    payMethod.value = first.value
  }
})

/**
 * Places the renewal order and shows the invoice raised for it.
 *
 * The success panel is built from an order id and an invoice id, so the store only sets its
 * result from the API's own answer.
 *
 * @returns Resolves when the attempt has ended.
 */
const placeRenewal = async (): Promise<void> => {
  await store.placeRenewal(props.domainId, years.value, payMethod.value)
}
</script>
