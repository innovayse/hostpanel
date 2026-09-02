<template>
  <div>
    <NuxtLink to="/client/invoices" class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-8">
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.invoices.backTo') }}
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-32 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
      <div class="h-48 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <!-- Error -->
    <div v-else-if="error || !invoice" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-400">{{ $t('client.invoices.notFound') }}</p>
    </div>

    <div v-else>
      <!-- Invoice header -->
      <UiCard class="mb-6">
        <div class="flex items-start justify-between gap-4 flex-wrap mb-4">
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">{{ $t('client.invoices.heading', { id: invoice.id }) }}</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.invoices.issued') }} {{ formatDate(invoice.invoiceDate) }}</p>
          </div>
          <div class="flex items-center gap-3">
            <ClientStatusBadge :status="invoice.status" />
            <!-- Pay Now button for unpaid invoices -->
            <NuxtLink
              v-if="isOutstanding"
              :to="localePath(`/client/invoices/${invoice.id}/pay`)"
              class="px-5 py-2 rounded-xl bg-green-500 text-white font-semibold text-sm hover:bg-green-400 transition-colors flex items-center gap-2"
            >
              <CreditCard :size="16" :stroke-width="2" />
              {{ $t('client.invoices.payNow') }}
            </NuxtLink>
          </div>
        </div>

        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <div class="text-gray-500">{{ $t('client.invoices.dueDate') }}</div>
            <div class="text-gray-900 dark:text-white font-medium mt-0.5" :class="isOverdue ? 'text-red-400' : ''">
              {{ formatDate(invoice.dueDate) }}
            </div>
          </div>
          <!-- `paidAt` is a moment, not a calendar day, so it is rendered with its time. -->
          <div v-if="formatDateTime(invoice.paidAt) !== EMPTY_DATE">
            <div class="text-gray-500">{{ $t('client.invoices.datePaid') }}</div>
            <div class="text-gray-900 dark:text-white font-medium mt-0.5">{{ formatDateTime(invoice.paidAt) }}</div>
          </div>
          <div>
            <div class="text-gray-500">{{ $t('client.invoices.paymentMethod') }}</div>
            <div class="text-white font-medium mt-0.5 capitalize">{{ invoice.paymentMethod || EMPTY_DATE }}</div>
          </div>
        </div>
      </UiCard>

      <!-- Line items -->
      <UiTable class="mb-6">
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.invoices.colDescription') }}</UiTableTh>
            <UiTableTh align="right">{{ $t('client.invoices.colAmount') }}</UiTableTh>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow v-for="item in invoice.items" :key="item.id">
            <UiTableTd class="text-gray-600 dark:text-gray-300">{{ item.description }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white font-medium">{{ money(item.amount) }}</UiTableTd>
          </UiTableRow>
        </UiTableBody>
        <UiTableFoot>
          <UiTableRow :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.subtotal') }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white">{{ money(invoice.subTotal) }}</UiTableTd>
          </UiTableRow>
          <UiTableRow v-if="invoice.tax > 0" :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.tax') }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white">{{ money(invoice.tax) }}</UiTableTd>
          </UiTableRow>
          <UiTableRow v-if="invoice.credit > 0" :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.creditApplied') }}</UiTableTd>
            <UiTableTd align="right" class="text-green-400">-{{ money(invoice.credit) }}</UiTableTd>
          </UiTableRow>
          <UiTableRow :hoverable="false" class="border-t border-white/10">
            <UiTableTd class="text-base font-bold text-gray-900 dark:text-white">{{ $t('client.invoices.total') }}</UiTableTd>
            <UiTableTd align="right" class="text-base font-bold text-gray-900 dark:text-white">{{ money(invoice.total) }}</UiTableTd>
          </UiTableRow>
        </UiTableFoot>
      </UiTable>

      <!-- Pay Now banner for unpaid -->
      <UiAlert
        v-if="isOutstanding"
        class="mb-6"
      >
        {{ $t('client.invoices.unpaidBanner') }}
        <template #action>
          <NuxtLink
            :to="localePath(`/client/invoices/${invoice.id}/pay`)"
            class="px-5 py-2 rounded-xl bg-green-500 text-white font-bold text-sm hover:bg-green-400 transition-colors flex items-center gap-2 flex-shrink-0"
          >
            <CreditCard :size="16" :stroke-width="2" />
            {{ $t('client.invoices.payAmount') }} {{ money(invoice.total) }}
          </NuxtLink>
        </template>
      </UiAlert>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, AlertCircle, CreditCard } from 'lucide-vue-next'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { useClientStore } from '~/stores/client'
import { formatCurrency } from '~/utils/formatCurrency'
import { EMPTY_DATE, formatDate, formatDateTime } from '~/utils/formatDate'
import { isInvoiceOutstanding, isInvoiceOverdue } from '~/utils/invoice'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const route = useRoute()
const localePath = useLocalePath()
const store = useClientStore()

// Straight from the API composables rather than through a store: this page reads these once
// and owns the results alone, which is the named exception to component -> store -> api.
const { data: invoice, pending, error } = await useBillingApi().loadInvoice(
  () => String(route.params.id)
)

/**
 * Formats one amount on this invoice in the account's billing currency.
 *
 * Every figure on the page used to be prefixed with a literal `$`, which asserted US dollars on
 * a bill that may be in none. The replacement then read `currencycode` / `currencyprefix` /
 * `currencysuffix` off the invoice — three fields `InvoiceDto` does not have and never sent, so
 * it printed no symbol for a different reason.
 *
 * The currency lives on the account, not the invoice: `ClientDto.Currency`, an ISO 4217 code,
 * reaching here as `store.user.currency`. When the account carries none, `formatCurrency` still
 * shows a grouped number with no symbol rather than repeating the old guess.
 *
 * @param amount - The amount the API sent for this line.
 * @returns The formatted amount, or an em dash when the line carries no figure.
 */
const money = (amount: string | number | null | undefined): string =>
  formatCurrency(amount, { code: store.user?.currency })

/** Whether this invoice still has money owing — one definition, in `utils/invoice.ts`. */
const isOutstanding = computed(() => Boolean(invoice.value) && isInvoiceOutstanding(invoice.value!))

/** Whether the due date should be shown in red — one definition, in `utils/invoice.ts`. */
const isOverdue = computed(() => isInvoiceOverdue(invoice.value))
</script>
