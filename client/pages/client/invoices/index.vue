<template>
  <UiStack gap="lg">
    <UiPageHeader :title="$t('client.invoices.title')" :description="$t('client.invoices.subtitle')" :icon="FileText" class="!mb-0" />

    <UiTabs v-model="activeTab" :tabs="tabs" />

    <UiAlert v-if="paymentStore.pdfError" variant="error" data-testid="invoices-pdf-error">{{ paymentStore.pdfError }}</UiAlert>

    <!-- Not a customer account. Checked before the failure branch: this is a state the
         API reported, not a fault, and it carries its own explanation and a way out. -->
    <ClientNoProfileNotice v-if="!store.invoicesLoading && store.clientProfileMissing" />

    <!-- Failed. Checked before the table, whose empty state a failure is otherwise
         indistinguishable from: the list is empty either way. -->
    <UiAlert v-else-if="store.invoicesError" variant="error" data-testid="invoices-error">{{ store.invoicesError }}</UiAlert>

    <UiTable
      v-else
      scrollable
      :label="$t('client.invoices.title')"
      :columns="6"
      :loading="store.invoicesLoading"
      :empty="!filteredInvoices.length"
      :empty-text="$t('client.invoices.empty')"
      :empty-icon="FileText"
      data-testid="invoices-table"
    >
      <template #head>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.invoices.colHash') }}</UiTableTh>
            <UiTableTh>{{ $t('client.invoices.colDate') }}</UiTableTh>
            <UiTableTh>{{ $t('client.invoices.colDueDate') }}</UiTableTh>
            <UiTableTh align="right">{{ $t('client.invoices.colAmount') }}</UiTableTh>
            <UiTableTh align="center">{{ $t('client.invoices.colStatus') }}</UiTableTh>
            <UiTableTh actions />
          </UiTableRow>
        </UiTableHead>
      </template>
      <UiTableBody>
        <UiTableRow v-for="invoice in filteredInvoices" :key="invoice.id" :to="invoicePath(invoice.id)" :data-testid="`invoice-row-${invoice.id}`">
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" weight="medium">#{{ invoice.id }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ formatDate(invoice.invoiceDate) }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" :tone="isOverdue(invoice) ? 'danger' : 'subtle'" :weight="isOverdue(invoice) ? 'medium' : 'normal'">
              {{ formatDate(invoice.dueDate) }}
            </UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiText :as="UiTextTag.Span" size="sm" weight="semibold">{{ formatCurrency(invoice.total, { code: invoice.currency }) }}</UiText>
          </UiTableTd>
          <UiTableTd align="center">
            <ClientStatusBadge :status="invoice.status" />
          </UiTableTd>
          <UiTableTd align="right">
            <UiRowActions :actions="rowActions(invoice)" :testid="`invoice-actions-${invoice.id}`" />
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Invoice list: filter tabs (all / unpaid / paid) over the client's invoices with amount, dates
 * and status, each row opening the invoice and offering view, PDF, print and pay in its menu. Loading, missing-profile and failure states come
 * from the client store.
 */
import { clientInvoicePath, clientInvoicePayPath } from '~/constants/clientRoutes'
import { CreditCard, Download, Eye, FileText, Printer } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { useInvoicePaymentStore } from '~/stores/invoicepayment'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { UiRowAction } from '~/types/uirowaction'
import { ListFilter } from '~/types/listfilter'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { isInvoiceDownloadable, isInvoiceOutstanding, isInvoiceOverdue as isOverdue, isInvoicePaid } from '~/utils/invoice'
import { UiTextTag } from '~/utils/ui/enums'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The client store that owns the invoice list and its loading and failure state. */
const store = useClientStore()

/** The invoice payment store that downloads and prints an invoice's PDF. */
const paymentStore = useInvoicePaymentStore()

await useAsyncData('client-invoices', () => store.fetchInvoices(true))

/** The active filter tab, a `ListFilter` value (a string so the tab bar can v-model it). */
const activeTab = ref<string>(ListFilter.All)

/**
 * The filter tabs.
 *
 * The unpaid tab means "still owed", not "status is literally Unpaid". There is no Overdue
 * tab, so counting the status alone would leave overdue invoices reachable only from All,
 * hiding them from the customer who came here to see what they owe, and from the dashboard
 * card that links straight to this tab. The paid tab carries no count.
 */
const tabs = computed(() => [
  { value: ListFilter.All, label: t('client.invoices.tabAll'), badge: store.invoices.length },
  { value: ListFilter.Current, label: t('client.invoices.tabUnpaid'), badge: store.invoices.filter(isInvoiceOutstanding).length },
  { value: ListFilter.Finished, label: t('client.invoices.tabPaid') },
])

/** The rows the active tab shows. The unpaid tab spans both owing statuses; see {@link tabs}. */
const filteredInvoices = computed(() => {
  if (activeTab.value === ListFilter.Current) {
    return store.invoices.filter(isInvoiceOutstanding)
  }
  if (activeTab.value === ListFilter.Finished) {
    return store.invoices.filter(isInvoicePaid)
  }
  return store.invoices
})

/**
 * The page of one invoice, with the active locale's prefix.
 *
 * @param id - Invoice id.
 * @returns The path the row and its View action open.
 */
const invoicePath = (id: number): string => localePath(clientInvoicePath(id))

/**
 * The actions of one invoice row: view, the PDF to download or print once issued, and pay while money is owed.
 *
 * @param invoice - The invoice the row shows.
 * @returns The actions of the row's menu, in menu order.
 */
const rowActions = (invoice: ClientInvoice): UiRowAction[] => {
  const id = String(invoice.id)
  const actions: UiRowAction[] = [{ key: 'view', label: t('common.view'), icon: Eye, to: invoicePath(invoice.id) }]
  if (isInvoiceDownloadable(invoice)) {
    actions.push(
      { key: 'download', label: t('client.invoices.downloadPdf'), icon: Download, onSelect: () => paymentStore.downloadInvoicePdf(id) },
      { key: 'print', label: t('common.print'), icon: Printer, onSelect: () => paymentStore.printInvoicePdf(id) },
    )
  }
  if (isInvoiceOutstanding(invoice)) {
    actions.push({ key: 'pay', label: t('client.invoices.pay'), icon: CreditCard, to: localePath(clientInvoicePayPath(id)) })
  }
  return actions
}
</script>
