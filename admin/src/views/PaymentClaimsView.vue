<script setup lang="ts">
/**
 * Held payment claims: the stored method charges where the gateway took money the invoice could
 * not record (a different amount or currency, or an invoice no longer open for payment). Each row
 * names the invoice, the client, the amount, the gateway and when the claim was written, so an
 * operator can settle it by hand. The list is read-only and paged by the server.
 */
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { formatDateTime } from '@/lib/format'
import { formatCurrencyCode } from '@/lib/money'
import { clientPath, invoicePath } from '@/lib/routes'
import { paymentClaimStatusTone } from '@/lib/tones'
import { useInvoicePaymentClaimsStore } from '@/stores/invoicePaymentClaims'
import { InvoicePaymentClaimStatus } from '@/types/invoicepaymentclaim'

/** Translation function. */
const { t } = useI18n()

/** Store holding the claims, their total and the load state. */
const store = useInvoicePaymentClaimsStore()

/** Current page (1-based). */
const page = ref(1)

/**
 * Loads a page of held claims.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page is loaded.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage(InvoicePaymentClaimStatus.Held, next, DEFAULT_PAGE_SIZE)
}

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="billing-payment-claims-view">
    <UiPageHeader
      :title="t('billing.paymentClaims.heading')"
      :subtitle="t('billing.paymentClaims.subtitle')"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="billing-payment-claims-error"
      @retry="load(page)"
    />

    <UiText v-if="!store.error" tone="muted" size="md" data-testid="billing-payment-claims-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiText>

    <UiCard>
      <UiTable
        :loading="store.loading && store.claims.length === 0"
        :empty="!store.loading && !store.error && store.claims.length === 0"
        :colspan="6"
        data-testid="billing-payment-claims-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.amount') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.paymentClaims.gatewayModule') }}</UiTableHeader>
            <UiTableHeader>{{ t('billing.paymentClaims.created') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="claim in store.claims" :key="claim.id" hoverable>
            <UiTableCell>
              <UiLink
                :to="invoicePath(claim.invoiceId)"
                variant="link"
                :data-testid="`billing-payment-claim-invoice-${claim.id}`"
              >
                #{{ claim.invoiceId }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink
                variant="link"
                :to="clientPath(claim.clientId)"
                :data-testid="`billing-payment-claim-client-${claim.id}`"
              >
                {{ claim.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell numeric>{{ formatCurrencyCode(claim.amount, claim.currency) }}</UiTableCell>
            <UiTableCell>{{ claim.gatewayModuleId }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(claim.createdAt) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="paymentClaimStatusTone(claim.status)">{{ t(`billing.paymentClaims.status.${claim.status}`) }}</UiBadge>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('billing.paymentClaims.empty') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="billing-payment-claims-pagination"
      @update:page="load"
    />
  </UiStack>
</template>
