<script setup lang="ts">
/**
 * Invoice detail: the invoice loaded by id and shown in eight tabs (summary, add payment, options,
 * credit, refund, credit notes, fulfilment, notes). The route decides the mode (`/view` is read-only, `/edit` lets the
 * operator change the invoice) and a button on the page toggles it. Every tab shows the outcome
 * of its own actions next to them, and the PDF download toasts its refusal; the
 * page itself only reports a failed load, with Retry. While the invoice is read the page draws the cards of
 * `InvoiceDetailView.schema` as placeholders, the same blocks the summary tab fills once it has arrived.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import InvoiceCreditTab from '@/components/billing/InvoiceCreditTab.vue'
import InvoiceCreditNotesTab from '@/components/billing/InvoiceCreditNotesTab.vue'
import InvoiceFulfilmentTab from '@/components/billing/InvoiceFulfilmentTab.vue'
import InvoiceNotesTab from '@/components/billing/InvoiceNotesTab.vue'
import InvoiceOptionsTab from '@/components/billing/InvoiceOptionsTab.vue'
import InvoicePaymentTab from '@/components/billing/InvoicePaymentTab.vue'
import InvoiceRefundTab from '@/components/billing/InvoiceRefundTab.vue'
import InvoiceSummaryTab from '@/components/billing/InvoiceSummaryTab.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { useBillingStore } from '@/stores/billing'
import { InvoiceOpenAction, InvoiceTab, InvoiceStatus } from '@/types/invoice'
import type { InvoiceDetail } from '@/types/invoice'
import { saveBlob } from '@/utils/saveBlob'
import { printBlob } from '@/lib/printBlob'
import type { DownloadedFile } from '@/types/download'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'
import { schemaHeader } from '@/lib/schemaHeader'
import schema from '@/views/InvoiceDetailView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** Builds the trail the header draws above the title: Billing, Invoices, the invoice. */
const { crumbs } = usePageCrumbs()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(manageMode.value ? UiActionKind.View : UiActionKind.Manage, { label: manageMode.value ? t('billing.invoiceDetail.viewInvoice') : t('billing.invoiceDetail.manageInvoice'), variant: UiButtonVariant.Primary, testid: 'billing-invoice-toggle-mode', onSelect: toggleMode }),
  headerAction(UiActionKind.Print, { label: t('shared.print'), variant: UiButtonVariant.Secondary, testid: 'billing-invoice-print', onSelect: print }),
  headerAction(UiActionKind.Download, { label: t('shared.pdf'), title: t('billing.actions.downloadPdf'), variant: UiButtonVariant.Secondary, testid: 'billing-invoice-download', onSelect: downloadPdf }),
])

/** Route action that opens the invoice for editing. */
const EDIT_ACTION = InvoiceOpenAction.Edit

const route = useRoute()
const store = useBillingStore()

/** The tab on show. */
const activeTab = ref<string>(InvoiceTab.Summary)

/** True when the operator can change the invoice (the `/edit` route, or toggled on). */
const manageMode = ref(route.params.action === EDIT_ACTION)

/** Toasts the outcome of the PDF download and print. */
const feedback = useOutcomeToast()

/** Id of the invoice from the route. */
const invoiceId = computed<number>(() => Number(route.params.id))

/** The invoice on show, once the store holds the one the route names. */
const invoice = computed<InvoiceDetail | null>(() =>
  store.currentInvoice !== null && store.currentInvoice.id === invoiceId.value ? store.currentInvoice : null,
)

/** True when the draft notice applies: a draft invoice opened for editing. */
const showDraftNotice = computed<boolean>(() => manageMode.value && invoice.value?.status === InvoiceStatus.Draft)

/** The sentence of a failed load, shown with Retry while no invoice is on show; null otherwise. */
const loadError = computed<string | null>(() => (invoice.value === null ? store.error : null))

/** The tabs; "Options" exists only while managing. */
const tabs = computed<UiTabItem[]>(() => {
  const all: UiTabItem[] = [
    { value: InvoiceTab.Summary, label: t('billing.invoiceDetail.tabSummary'), testid: 'billing-invoice-tab-summary' },
    { value: InvoiceTab.Payment, label: t('billing.invoiceDetail.addPayment'), testid: 'billing-invoice-tab-payment' },
    { value: InvoiceTab.Options, label: t('billing.invoiceDetail.options'), testid: 'billing-invoice-tab-options' },
    { value: InvoiceTab.Credit, label: t('billing.invoiceDetail.tabCredit'), testid: 'billing-invoice-tab-credit' },
    { value: InvoiceTab.Refund, label: t('billing.invoiceDetail.tabRefund'), testid: 'billing-invoice-tab-refund' },
    { value: InvoiceTab.CreditNotes, label: t('billing.invoiceDetail.tabCreditNotes'), testid: 'billing-invoice-tab-credit-notes' },
    { value: InvoiceTab.Fulfilment, label: t('billing.invoiceDetail.tabFulfilment'), testid: 'billing-invoice-tab-fulfilment' },
    { value: InvoiceTab.Notes, label: t('shared.notes'), testid: 'billing-invoice-tab-notes' },
  ]
  return manageMode.value ? all : all.filter((tab) => tab.value !== InvoiceTab.Options)
})

/**
 * Loads the invoice the route names.
 *
 * @returns Resolves when the request has settled.
 */
const load = (): Promise<void> => store.fetchById(invoiceId.value)

/** Toggles between the read-only view and the editing mode. */
const toggleMode = (): void => {
  manageMode.value = !manageMode.value
  if (!manageMode.value && activeTab.value === InvoiceTab.Options) {
    activeTab.value = InvoiceTab.Summary
  }
}

/**
 * Fetches the invoice's PDF; a refusal is toasted with the API's own sentence.
 *
 * @returns The PDF and its name, or null when refused.
 */
const fetchPdf = async (): Promise<DownloadedFile | null> => {
  const file = await store.downloadPdf(invoiceId.value)
  if (file === null) {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
  return file
}

/** Prints the invoice's PDF, the same file the PDF action downloads. */
const print = async (): Promise<void> => {
  const file = await fetchPdf()
  if (file !== null) {
    printBlob(file.blob)
  }
}

/** Downloads the invoice's PDF and saves it. */
const downloadPdf = async (): Promise<void> => {
  const file = await fetchPdf()
  if (file !== null) {
    saveBlob(file.blob, file.fileName ?? `invoice-${invoiceId.value}.pdf`)
  }
}

onMounted(load)
watch(invoiceId, load)
</script>

<template>
  <UiStack data-testid="billing-invoice-detail-view">
    <UiPageHeader :actions="headerActions"
      v-bind="schemaHeader(schema, t, { title: t('billing.invoiceDetail.invoiceId', { id: invoiceId }) })"
      :crumbs="crumbs(t('billing.invoiceDetail.invoiceId', { id: invoiceId }))"
      data-testid="invoice-detail-title"
    />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-invoice-error"
      @retry="load"
    />

    <template v-if="loadError === null">
      <!-- While the invoice is read the page draws the cards of its schema as placeholders; the tabs arrive with the data -->
      <UiDetailCards v-if="invoice === null" :columns="schema.columns" loading />

      <template v-else>
        <UiAlert v-if="showDraftNotice" :variant="UiTone.Info" data-testid="billing-invoice-draft-banner">
          {{ t('billing.invoiceDetail.thisIsADraftInvoice') }} {{ t('billing.invoiceDetail.theClientIsNotAble') }}
        </UiAlert>

        <UiTabs v-model="activeTab" :items="tabs">
          <InvoiceSummaryTab v-if="activeTab === InvoiceTab.Summary" :invoice="invoice" :readonly="!manageMode" />
          <InvoicePaymentTab v-else-if="activeTab === InvoiceTab.Payment" :invoice="invoice" />
          <InvoiceOptionsTab v-else-if="activeTab === InvoiceTab.Options" :invoice="invoice" />
          <InvoiceCreditTab v-else-if="activeTab === InvoiceTab.Credit" :invoice="invoice" />
          <InvoiceRefundTab v-else-if="activeTab === InvoiceTab.Refund" :invoice="invoice" :readonly="!manageMode" />
          <InvoiceCreditNotesTab v-else-if="activeTab === InvoiceTab.CreditNotes" :invoice="invoice" :readonly="!manageMode" />
          <InvoiceFulfilmentTab v-else-if="activeTab === InvoiceTab.Fulfilment" :invoice="invoice" :readonly="!manageMode" />
          <InvoiceNotesTab v-else-if="activeTab === InvoiceTab.Notes" :invoice="invoice" :readonly="!manageMode" />
        </UiTabs>
      </template>
    </template>
  </UiStack>
</template>
