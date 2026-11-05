<script setup lang="ts">
/**
 * Upcoming renewals: the services and domains renewing in the next 30, 60 or 90 days, with the staff actions
 * around them. A row can switch automatic renewal on or off and raise the client's renewal invoice by hand; a
 * third list shows domain renewal lines left claimed after the registrar gave no clear answer, which staff
 * release once they have checked the registrar. Reads and writes through `stores/renewals`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { BILLING_CYCLE_OPTIONS } from '@/lib/constants'
import { formatDate, formatDateTime } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { clientPath, invoicePath } from '@/lib/routes'
import { statusLabel } from '@/lib/tones'
import { RenewalNotice, useRenewalsStore } from '@/stores/renewals'
import { RenewalWindow } from '@/types/renewal'
import type { ClaimedDomainRenewal } from '@/types/renewal'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Translation function. */
const { t } = useI18n()

/** Store holding the lists, the window and the action state. */
const store = useRenewalsStore()

/** The claimed line awaiting the release confirmation, null when no dialog is open. */
const releaseTarget = ref<ClaimedDomainRenewal | null>(null)

/** One tab per window. */
const windowTabs = computed<UiTabItem[]>(() =>
  Object.values(RenewalWindow).map(value => ({
    value,
    label: t(`billing.renewals.window.${value}`),
    testid: `renewals-window-${value}`,
  })),
)

/** The sentence for the result of the last action, null when there is none. */
const noticeMessage = computed<string | null>(() => {
  switch (store.notice) {
    case RenewalNotice.InvoicesRaised:
      return t('billing.renewals.invoicesRaised', { count: store.noticeCount })
    case RenewalNotice.NothingDue:
      return t('billing.renewals.nothingDue')
    case RenewalNotice.ClaimReleased:
      return t('billing.renewals.claimReleased')
    default:
      return null
  }
})

/**
 * Moves to another window.
 *
 * @param value - The tab value, a {@link RenewalWindow}.
 */
const chooseWindow = (value: string): void => {
  void store.setWindow(value as RenewalWindow)
}

/**
 * Confirms the release of the claim the dialog is open for.
 *
 * @returns Promise that resolves once the release and the reload are done.
 */
const confirmRelease = async (): Promise<void> => {
  const target = releaseTarget.value
  if (target === null) {
    return
  }
  await store.releaseClaim(target.invoiceId, target.itemId)
  releaseTarget.value = null
}

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack data-testid="renewals-view">
    <UiPageHeader :title="t('billing.renewals.heading')" :subtitle="t('billing.renewals.subtitle')" />

    <UiTabs
      :items="windowTabs"
      :model-value="store.window"
      :aria-label="t('billing.renewals.windowLabel')"
      @update:model-value="chooseWindow"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="renewals-error"
      @retry="store.fetchAll()"
    />

    <UiStatusMessage :message="store.actionError" :tone="UiTone.Danger" data-testid="renewals-action-error" />
    <UiStatusMessage :message="noticeMessage" :tone="UiTone.Success" data-testid="renewals-notice" />

    <template v-if="!store.error">
      <UiStack>
        <UiHeading :level="2">{{ t('billing.renewals.servicesHeading') }}</UiHeading>
        <UiCard>
          <UiTable
            :loading="store.loading && store.services.length === 0"
            :empty="!store.loading && store.services.length === 0"
            :colspan="7"
            data-testid="renewals-services-table"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
                <UiTableHeader>{{ t('billing.renewals.product') }}</UiTableHeader>
                <UiTableHeader>{{ t('billing.renewals.renewsOn') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.amount') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.autoRenew') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow v-for="service in store.services" :key="service.id" hoverable>
                <UiTableCell>
                  <UiLink variant="link" :to="clientPath(service.clientId)" :data-testid="`renewals-service-client-${service.id}`">
                    {{ service.clientName }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>{{ service.productName }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(service.nextRenewalAt) }}</UiTableCell>
                <UiTableCell>{{ statusLabel(BILLING_CYCLE_OPTIONS, service.billingCycle) }}</UiTableCell>
                <UiTableCell>{{ formatPrice(service.amount, service.priceCurrency) }}</UiTableCell>
                <UiTableCell>
                  <UiToggle
                    :model-value="service.autoRenew"
                    :disabled="store.acting"
                    :data-testid="`renewals-service-auto-renew-${service.id}`"
                    @update:model-value="store.setServiceAutoRenew(service.id, $event)"
                  />
                </UiTableCell>
                <UiTableCell>
                  <UiButton
                    :variant="UiButtonVariant.Secondary"
                    size="sm"
                    :disabled="store.acting"
                    :data-testid="`renewals-service-generate-${service.id}`"
                    @click="store.generateInvoices(service.clientId)"
                  >
                    {{ t('billing.renewals.generateInvoice') }}
                  </UiButton>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('billing.renewals.emptyServices') }}
            </template>
          </UiTable>
        </UiCard>
        <UiPagination
          :page="store.servicesPage"
          :total="store.servicesTotal"
          :page-size="store.pageSize"
          testid="renewals-services-pagination"
          @update:page="store.fetchServices"
        />
      </UiStack>

      <UiStack>
        <UiHeading :level="2">{{ t('billing.renewals.domainsHeading') }}</UiHeading>
        <UiCard>
          <UiTable
            :loading="store.loading && store.domains.length === 0"
            :empty="!store.loading && store.domains.length === 0"
            :colspan="6"
            data-testid="renewals-domains-table"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
                <UiTableHeader>{{ t('billing.renewals.expires') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.amount') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.autoRenew') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow v-for="domain in store.domains" :key="domain.id" hoverable>
                <UiTableCell>
                  <UiLink variant="link" :to="clientPath(domain.clientId)" :data-testid="`renewals-domain-client-${domain.id}`">
                    {{ domain.clientName }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>{{ domain.name }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
                <UiTableCell>{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</UiTableCell>
                <UiTableCell>
                  <UiToggle
                    :model-value="domain.autoRenew"
                    :disabled="store.acting"
                    :data-testid="`renewals-domain-auto-renew-${domain.id}`"
                    @update:model-value="store.setDomainAutoRenew(domain.id, $event)"
                  />
                </UiTableCell>
                <UiTableCell>
                  <UiButton
                    :variant="UiButtonVariant.Secondary"
                    size="sm"
                    :disabled="store.acting"
                    :data-testid="`renewals-domain-generate-${domain.id}`"
                    @click="store.generateInvoices(domain.clientId)"
                  >
                    {{ t('billing.renewals.generateInvoice') }}
                  </UiButton>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('billing.renewals.emptyDomains') }}
            </template>
          </UiTable>
        </UiCard>
        <UiPagination
          :page="store.domainsPage"
          :total="store.domainsTotal"
          :page-size="store.pageSize"
          testid="renewals-domains-pagination"
          @update:page="store.fetchDomains"
        />
      </UiStack>

      <UiStack>
        <UiHeading :level="2">{{ t('billing.renewals.claimsHeading') }}</UiHeading>
        <UiText tone="muted" size="sm">{{ t('billing.renewals.claimsNote') }}</UiText>
        <UiCard>
          <UiTable
            :loading="store.loading && store.claims.length === 0"
            :empty="!store.loading && store.claims.length === 0"
            :colspan="5"
            data-testid="renewals-claims-table"
          >
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
                <UiTableHeader>{{ t('billing.renewals.line') }}</UiTableHeader>
                <UiTableHeader>{{ t('billing.renewals.claimedAt') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow v-for="claim in store.claims" :key="claim.itemId" hoverable>
                <UiTableCell>
                  <UiLink variant="link" :to="invoicePath(claim.invoiceId)" :data-testid="`renewals-claim-invoice-${claim.itemId}`">
                    #{{ claim.invoiceId }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>
                  <UiLink variant="link" :to="clientPath(claim.clientId)" :data-testid="`renewals-claim-client-${claim.itemId}`">
                    #{{ claim.clientId }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>{{ claim.description }}</UiTableCell>
                <UiTableCell muted>{{ formatDateTime(claim.claimedAt) }}</UiTableCell>
                <UiTableCell>
                  <UiButton
                    :variant="UiButtonVariant.Danger"
                    size="sm"
                    :disabled="store.acting"
                    :data-testid="`renewals-claim-release-${claim.itemId}`"
                    @click="releaseTarget = claim"
                  >
                    {{ t('billing.renewals.releaseClaim') }}
                  </UiButton>
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
            <template #empty>
              {{ t('billing.renewals.emptyClaims') }}
            </template>
          </UiTable>
        </UiCard>
      </UiStack>
    </template>

    <UiDialog
      variant="confirm"
      :open="releaseTarget !== null"
      :title="t('billing.renewals.releaseTitle')"
      :message="t('billing.renewals.releaseMessage', { line: releaseTarget?.description ?? '' })"
      :confirm-label="t('billing.renewals.releaseClaim')"
      :loading-label="t('billing.renewals.releasing')"
      :loading="store.acting"
      tone="danger"
      @confirm="confirmRelease"
      @close="releaseTarget = null"
    />
  </UiStack>
</template>
