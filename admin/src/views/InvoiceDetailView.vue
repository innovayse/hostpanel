<script setup lang="ts">
/**
 * Invoice detail: the invoice loaded by id and shown in six tabs (summary, add payment, options,
 * credit, refund, notes). The route decides the mode (`/view` is read-only, `/edit` lets the
 * operator change the invoice) and a button on the page toggles it. Every tab shows the outcome
 * of its own actions next to them, and the PDF download shows its refusal beside its button; the
 * page itself only reports a failed load, with Retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import InvoiceCreditTab from '@/components/billing/InvoiceCreditTab.vue'
import InvoiceNotesTab from '@/components/billing/InvoiceNotesTab.vue'
import InvoiceOptionsTab from '@/components/billing/InvoiceOptionsTab.vue'
import InvoicePaymentTab from '@/components/billing/InvoicePaymentTab.vue'
import InvoiceRefundTab from '@/components/billing/InvoiceRefundTab.vue'
import InvoiceSummaryTab from '@/components/billing/InvoiceSummaryTab.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { Icon } from '@/lib/icons'
import { useBillingStore } from '@/stores/billing'
import { InvoiceOpenAction, InvoiceTab, InvoiceStatus } from '@/types/invoice'
import type { InvoiceDetail } from '@/types/invoice'
import { saveBlob } from '@/utils/saveBlob'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Route action that opens the invoice for editing. */
const EDIT_ACTION = InvoiceOpenAction.Edit

const route = useRoute()
const router = useRouter()
const store = useBillingStore()

/** The tab on show. */
const activeTab = ref<string>(InvoiceTab.Summary)

/** True when the operator can change the invoice (the `/edit` route, or toggled on). */
const manageMode = ref(route.params.action === EDIT_ACTION)

/** Sentence of a failed PDF download, shown beside its button; null while there is none. */
const pdfError = ref<string | null>(null)

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

/** Goes back to the previous screen. */
const goBack = (): void => {
  router.back()
}

/** Opens the browser print dialog for the page. */
const print = (): void => {
  window.print()
}

/** Downloads the invoice's PDF and saves it; a refusal is shown beside the button. */
const downloadPdf = async (): Promise<void> => {
  pdfError.value = null
  const file = await store.downloadPdf(invoiceId.value)
  if (file === null) {
    pdfError.value = store.error
    return
  }
  saveBlob(file.blob, file.fileName ?? `invoice-${invoiceId.value}.pdf`)
}

onMounted(load)
watch(invoiceId, load)
</script>

<template>
  <UiStack data-testid="billing-invoice-detail-view">
    <UiPageHeader
      :title="t('billing.invoiceDetail.invoiceId', { id: invoiceId })"
      :subtitle="t('billing.invoiceDetail.manageAndViewInvoiceDetails')"
      data-testid="invoice-detail-title"
    >
      <template #actions>
        <UiButton variant="ghost" data-testid="billing-invoice-back" @click="goBack">
          <UiIcon :markup="Icon.Back" size="sm" />
          {{ t('shared.back') }}
        </UiButton>
        <UiButton variant="primary" data-testid="billing-invoice-toggle-mode" @click="toggleMode">
          {{ manageMode ? t('billing.invoiceDetail.viewInvoice') : t('billing.invoiceDetail.manageInvoice') }}
        </UiButton>
        <UiButton variant="secondary" data-testid="billing-invoice-print" @click="print">
          {{ t('shared.print') }}
        </UiButton>
        <UiButton variant="secondary" data-testid="billing-invoice-download" @click="downloadPdf">
          {{ t('billing.invoiceDetail.downloadPdf') }}
        </UiButton>
        <UiStatusMessage :message="pdfError" :tone="UiTone.Danger" data-testid="billing-invoice-download-feedback" />
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="billing-invoice-error"
      @retry="load"
    />

    <UiSkeleton v-if="invoice === null && store.loading" shape="block" data-testid="billing-invoice-loading" />

    <template v-if="invoice !== null">
      <UiAlert v-if="showDraftNotice" variant="info" data-testid="billing-invoice-draft-banner">
        {{ t('billing.invoiceDetail.thisIsADraftInvoice') }} {{ t('billing.invoiceDetail.theClientIsNotAble') }}
      </UiAlert>

      <UiTabs v-model="activeTab" :items="tabs">
        <InvoiceSummaryTab v-if="activeTab === InvoiceTab.Summary" :invoice="invoice" :readonly="!manageMode" />
        <InvoicePaymentTab v-else-if="activeTab === InvoiceTab.Payment" :invoice="invoice" :readonly="!manageMode" />
        <InvoiceOptionsTab v-else-if="activeTab === InvoiceTab.Options" :invoice="invoice" />
        <InvoiceCreditTab v-else-if="activeTab === InvoiceTab.Credit" :invoice="invoice" :readonly="!manageMode" />
        <InvoiceRefundTab v-else-if="activeTab === InvoiceTab.Refund" :invoice="invoice" :readonly="!manageMode" />
        <InvoiceNotesTab v-else-if="activeTab === InvoiceTab.Notes" :invoice="invoice" :readonly="!manageMode" />
      </UiTabs>
    </template>
  </UiStack>
</template>
