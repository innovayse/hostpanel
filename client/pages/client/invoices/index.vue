<template>
  <div>
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.invoices.title') }}</h1>
      <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.invoices.subtitle') }}</p>
    </div>

    <!-- Filter tabs -->
    <UiTabs
      :tabs="tabs.map(t => ({ value: t.key, label: t.label, badge: t.count !== null ? t.count : undefined }))"
      v-model="activeTab"
      class="mb-6"
    />

    <!-- Loading -->
    <div v-if="store.invoicesLoading" class="space-y-3">
      <div v-for="i in 5" :key="i" class="h-16 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <!-- Not a customer account. Checked before the failure branch: this is a state the
         API reported, not a fault, and it carries its own explanation and a way out. -->
    <ClientNoProfileNotice v-else-if="store.clientProfileMissing" />

    <!-- Failed. Checked before the empty state, which a failure is otherwise
         indistinguishable from: the list is empty either way. -->
    <UiAlert v-else-if="store.invoicesError" variant="error">{{ store.invoicesError }}</UiAlert>

    <!-- Empty -->
    <div v-else-if="!filteredInvoices.length" class="text-center py-20">
      <FileText :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
      <p class="text-gray-400">{{ $t('client.invoices.empty') }}</p>
    </div>

    <!-- Invoices list -->
    <UiTable v-else>
      <UiTableHead>
        <UiTableRow :hoverable="false">
          <UiTableTh>{{ $t('client.invoices.colHash') }}</UiTableTh>
          <UiTableTh>{{ $t('client.invoices.colDate') }}</UiTableTh>
          <UiTableTh>{{ $t('client.invoices.colDueDate') }}</UiTableTh>
          <UiTableTh align="right">{{ $t('client.invoices.colAmount') }}</UiTableTh>
          <UiTableTh align="center">{{ $t('client.invoices.colStatus') }}</UiTableTh>
          <UiTableTh />
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="invoice in filteredInvoices" :key="invoice.id">
          <UiTableTd class="text-gray-900 dark:text-white font-medium">#{{ invoice.id }}</UiTableTd>
          <UiTableTd class="text-gray-500 dark:text-gray-400">{{ formatDate(invoice.invoiceDate) }}</UiTableTd>
          <UiTableTd :class="isOverdue(invoice) ? 'text-red-400 font-medium' : 'text-gray-500 dark:text-gray-400'">{{ formatDate(invoice.dueDate) }}</UiTableTd>
          <UiTableTd align="right" class="text-gray-900 dark:text-white font-semibold">{{ formatCurrency(invoice.total, { code: store.user?.currency }) }}</UiTableTd>
          <UiTableTd align="center">
            <ClientStatusBadge :status="invoice.status" />
          </UiTableTd>
          <UiTableTd align="right">
            <NuxtLink
              :to="`/client/invoices/${invoice.id}`"
              class="px-3 py-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-xs hover:border-cyan-500/30 hover:text-gray-900 dark:hover:text-white transition-all"
            >
              {{ $t('client.invoices.view') }}
            </NuxtLink>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>
  </div>
</template>

<script setup lang="ts">
import { FileText } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { isInvoiceOutstanding, isInvoiceOverdue as isOverdue } from '~/utils/invoice'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const store = useClientStore()

await useAsyncData('client-invoices', () => store.fetchInvoices(true))

const activeTab = ref('all')

/**
 * The filter tabs.
 *
 * The unpaid tab means "still owed", not "status is literally Unpaid". There is no Overdue
 * tab, so counting the status alone left overdue invoices reachable only from All — hiding
 * them from the customer who came here to see what they owe, and from the dashboard card
 * that links straight to this tab.
 */
const tabs = computed(() => [
  { key: 'all',    label: t('client.invoices.tabAll'),    count: store.invoices.length },
  { key: 'Unpaid', label: t('client.invoices.tabUnpaid'), count: store.invoices.filter(isInvoiceOutstanding).length },
  { key: 'Paid',   label: t('client.invoices.tabPaid'),   count: null }
])

/** The rows the active tab shows. The unpaid tab spans both owing statuses; see {@link tabs}. */
const filteredInvoices = computed(() => {
  if (activeTab.value === 'all') return store.invoices
  if (activeTab.value === 'Unpaid') return store.invoices.filter(isInvoiceOutstanding)

  return store.invoices.filter(i => i.status === activeTab.value)
})


</script>
