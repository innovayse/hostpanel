<script setup lang="ts">
/**
 * Upcoming renewals: the services and domains renewing in the next 30, 60 or 90 days, with the staff actions
 * around them. A row can switch automatic renewal on or off and raise the client's renewal invoice by hand; a
 * third list shows domain renewal lines left claimed after the registrar gave no clear answer, which staff
 * release once they have checked the registrar. The window tabs, the cards of the three tables and their column heads are
 * the ones of `UpcomingRenewalsView.schema`, which `UiSkeletonOf` also draws while the view downloads. Reads and writes
 * through `stores/renewals`.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Icon } from '@/lib/icons'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { BILLING_CYCLE_OPTIONS } from '@/lib/constants'
import { formatDate, formatDateTime } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { clientPath, invoicePath } from '@/lib/routes'
import { statusLabel } from '@/lib/tones'
import { schemaHeader } from '@/lib/schemaHeader'
import { RenewalNotice, useRenewalsStore } from '@/stores/renewals'
import type { RenewalWindow } from '@/types/renewal'
import type { ClaimedDomainRenewal } from '@/types/renewal'
import { UiTone } from '@/utils/ui/tone'
import schema, {
  RENEWALS_CLAIMS_PART,
  RENEWALS_CLAIMS_TABLE,
  RENEWALS_DOMAINS_PART,
  RENEWALS_DOMAINS_TABLE,
  RENEWALS_SERVICES_PART,
  RENEWALS_SERVICES_TABLE,
  RENEWALS_WINDOW_TABS_PART,
} from '@/views/UpcomingRenewalsView.schema'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Translation function. */
const { t } = useI18n()

/** Store holding the lists, the window and the action state. */
const store = useRenewalsStore()

/** The claimed line awaiting the release confirmation, null when no dialog is open. */
const releaseTarget = ref<ClaimedDomainRenewal | null>(null)

/** One tab per window, the captions and values of the schema's strip. */
const windowTabs = computed<UiTabItem[]>(() =>
  tabItemsOfSchema(RENEWALS_WINDOW_TABS_PART.tabs, t, 'renewals-window-'),
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

/**
 * The one row action of a due service or domain: generate the client's invoices now.
 *
 * @param clientId - The client whose due items are invoiced.
 * @param testid - The `data-testid` of the action button.
 * @returns The action list passed to UiRowActions.
 */
const generateActions = (clientId: number, testid: string): UiMenuAction[] => [
  {
    label: t('billing.renewals.generateInvoice'),
    hint: t('billing.renewals.generateInvoiceHint'),
    icon: Icon.File,
    testid,
    disabled: store.acting,
    /**
     * Generates the client's due invoices.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      void store.generateInvoices(clientId)
    },
  },
]

/**
 * The one row action of a claimed renewal: release the claim, confirmed in a dialog first.
 *
 * @param claim - The row.
 * @returns The action list passed to UiRowActions.
 */
const releaseActions = (claim: ClaimedDomainRenewal): UiMenuAction[] => [
  {
    label: t('billing.renewals.releaseClaim'),
    icon: Icon.Undo,
    testid: `renewals-claim-release-${claim.itemId}`,
    disabled: store.acting,
    danger: true,
    /**
     * Opens the release confirmation for this claim.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      releaseTarget.value = claim
    },
  },
]

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack data-testid="renewals-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiTabs
      :items="windowTabs"
      :model-value="store.renewalWindow"
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
      <UiDetailCards :columns="RENEWALS_SERVICES_PART.columns" :loading="store.loading && store.services.length === 0">
        <template #card-services>
          <UiTable
            :empty-text="t('billing.renewals.emptyServices')"
            :empty="store.services.length === 0"
            data-testid="renewals-services-table"
          >
            <UiSchemaTableHead :columns="RENEWALS_SERVICES_TABLE.columns" />
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
                <UiTableCell numeric>{{ formatPrice(service.amount, service.priceCurrency) }}</UiTableCell>
                <UiTableCell>
                  <UiToggle
                    :model-value="service.autoRenew"
                    :disabled="store.acting"
                    :data-testid="`renewals-service-auto-renew-${service.id}`"
                    @update:model-value="store.setServiceAutoRenew(service.id, $event)"
                  />
                </UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="generateActions(service.clientId, `renewals-service-generate-${service.id}`)" :testid="`renewals-service-actions-${service.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
          <UiPagination
            :page="store.servicesPage"
            :total="store.servicesTotal"
            :page-size="store.pageSize"
            testid="renewals-services-pagination"
            @update:page="store.fetchServices"
          />
        </template>
      </UiDetailCards>

      <UiDetailCards :columns="RENEWALS_DOMAINS_PART.columns" :loading="store.loading && store.domains.length === 0">
        <template #card-domains>
          <UiTable
            :empty-text="t('billing.renewals.emptyDomains')"
            :empty="store.domains.length === 0"
            data-testid="renewals-domains-table"
          >
            <UiSchemaTableHead :columns="RENEWALS_DOMAINS_TABLE.columns" />
            <UiTableBody divided>
              <UiTableRow v-for="domain in store.domains" :key="domain.id" hoverable>
                <UiTableCell>
                  <UiLink variant="link" :to="clientPath(domain.clientId)" :data-testid="`renewals-domain-client-${domain.id}`">
                    {{ domain.clientName }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>{{ domain.name }}</UiTableCell>
                <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
                <UiTableCell numeric>{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</UiTableCell>
                <UiTableCell>
                  <UiToggle
                    :model-value="domain.autoRenew"
                    :disabled="store.acting"
                    :data-testid="`renewals-domain-auto-renew-${domain.id}`"
                    @update:model-value="store.setDomainAutoRenew(domain.id, $event)"
                  />
                </UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="generateActions(domain.clientId, `renewals-domain-generate-${domain.id}`)" :testid="`renewals-domain-actions-${domain.id}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
          <UiPagination
            :page="store.domainsPage"
            :total="store.domainsTotal"
            :page-size="store.pageSize"
            testid="renewals-domains-pagination"
            @update:page="store.fetchDomains"
          />
        </template>
      </UiDetailCards>

      <UiDetailCards :columns="RENEWALS_CLAIMS_PART.columns" :loading="store.loading && store.claims.length === 0">
        <template #card-claims>
          <UiText tone="muted" size="sm">{{ t('billing.renewals.claimsNote') }}</UiText>
          <UiTable
            :empty-text="t('billing.renewals.emptyClaims')"
            :empty="store.claims.length === 0"
            data-testid="renewals-claims-table"
          >
            <UiSchemaTableHead :columns="RENEWALS_CLAIMS_TABLE.columns" />
            <UiTableBody divided>
              <UiTableRow v-for="claim in store.claims" :key="claim.itemId" hoverable>
                <UiTableCell>
                  <UiLink variant="link" :to="invoicePath(claim.invoiceId)" :data-testid="`renewals-claim-invoice-${claim.itemId}`">
                    #{{ claim.invoiceId }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>
                  <UiLink variant="link" :to="clientPath(claim.clientId)" :data-testid="`renewals-claim-client-${claim.itemId}`">
                    {{ claim.clientName }}
                  </UiLink>
                </UiTableCell>
                <UiTableCell>{{ claim.description }}</UiTableCell>
                <UiTableCell muted>{{ formatDateTime(claim.claimedAt) }}</UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="releaseActions(claim)" :testid="`renewals-claim-actions-${claim.itemId}`" />
                </UiTableCell>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
        </template>
      </UiDetailCards>
    </template>

    <UiDialog
      variant="confirm"
      :open="releaseTarget !== null"
      :title="t('billing.renewals.releaseTitle')"
      :message="t('billing.renewals.releaseMessage', { line: releaseTarget?.description ?? '' })"
      :confirm-label="t('billing.renewals.releaseClaim')"
      :loading="store.acting"
      tone="danger"
      @confirm="confirmRelease"
      @close="releaseTarget = null"
    />
  </UiStack>
</template>
