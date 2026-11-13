<template>
  <UiStack data-testid="domain-overview">
    <!-- Hidden while the domain is still pending. -->
    <UiAlert
      v-if="!lockEnabled && !isPending"
      variant="error"
      :icon-size="16"
      :title="$t('client.domains.unlockWarningTitle')"
      data-testid="domain-unlock-warning"
    >
      {{ $t('client.domains.unlockWarningDesc') }}
    </UiAlert>

    <UiCard>
      <UiGrid as="dl" cols="2" gap="lg">
        <UiDescriptionItem :label="$t('client.domains.fieldDomain')" :value="fullName" value-class="font-medium" />
        <UiDescriptionItem :label="$t('client.domains.fieldFirstPayment')" :value="domain.firstPaymentAmount ? money(domain.firstPaymentAmount) : null" />
        <UiDescriptionItem :label="$t('client.domains.fieldRegistrationDate')" :value="formatDate(domain.registeredAt)" />
        <UiDescriptionItem :label="$t('client.domains.fieldRecurring')" :value="`${money(domain.recurringAmount)} / ${domain.registrationPeriod}${$t('client.domains.yearShort')}`" />
        <UiDescriptionItem :label="$t('client.domains.fieldNextDueDate')" :value="formatDate(domain.nextDueDate)" />
        <UiDescriptionItem :label="$t('client.domains.fieldPaymentMethod')" :value="domain.paymentMethod || null" value-class="capitalize" />
        <UiDescriptionItem :label="$t('client.domains.fieldStatus')">
          <ClientStatusBadge :status="domain.status" />
        </UiDescriptionItem>
      </UiGrid>
    </UiCard>

    <!-- Hidden while the domain is still pending. -->
    <UiCard v-if="!isPending && actions.length" padding="md" data-testid="domain-quick-actions">
      <UiHeading :level="3" size="subsection" class="!text-sm mb-3">{{ $t('client.domains.whatToday') }}</UiHeading>
      <UiStack gap="xs" align="start">
        <UiButton
          v-for="action in actions"
          :key="action.tab"
          variant="ghost"
          size="sm"
          :data-testid="`domain-quick-${action.tab}`"
          @click="emit('navigate', action.tab)"
        >
          <ChevronRight :size="14" :stroke-width="2" class="mr-1.5" />
          {{ action.label }}
        </UiButton>
      </UiStack>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * DomainOverview: the domain's details as a description list, a warning while the registrar
 * lock is off, and shortcuts to the other panels. Amounts are formatted in the domain's own
 * currency.
 */
import { computed } from 'vue'
import { ChevronRight } from 'lucide-vue-next'
import type { DomainDetail } from '~/types/domaindetail'
import { Permission } from '~/types/permission'
import { useClientStore } from '~/stores/client'
import { DomainTab } from '~/types/domaintab'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of DomainOverview. */
interface Props {
  /** The domain being shown. */
  domain: DomainDetail
  /** The full domain name. */
  fullName: string
  /** Whether the registrar lock is on. */
  lockEnabled: boolean
  /** Whether the registrar is still registering or transferring the domain. */
  isPending: boolean
}

/** The props of the component. */
const props = defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to open another panel. */
  navigate: [tab: DomainTab]
}>()

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** The shortcuts the person may use: the panel each opens and its sentence. */
const actions = computed(() => [
  { tab: DomainTab.Nameservers, label: t('client.domains.quickNameservers'), permission: Permission.ManageDomainSettings },
  { tab: DomainTab.Contact, label: t('client.domains.quickContact'), permission: Permission.ManageDomainSettings },
  { tab: DomainTab.Lock, label: t('client.domains.quickLock'), permission: Permission.ManageDomainSettings },
  { tab: DomainTab.Renew, label: t('client.domains.quickRenew'), permission: Permission.PlaceNewOrders },
].filter(action => store.mayDo(action.permission)))

/**
 * Formats an amount in the domain's own currency.
 *
 * @param amount - The amount to format.
 * @returns The formatted amount.
 */
const money = (amount: number): string => formatCurrency(amount, { code: props.domain.priceCurrency })
</script>
