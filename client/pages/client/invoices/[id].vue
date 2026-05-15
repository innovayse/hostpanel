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
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">Invoice #{{ invoice.invoiceid }}</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.invoices.issued') }} {{ invoice.date }}</p>
          </div>
          <div class="flex items-center gap-3">
            <ClientStatusBadge :status="invoice.status" />
            <!-- Pay Now button for unpaid invoices -->
            <NuxtLink
              v-if="invoice.status === 'Unpaid' || invoice.status === 'Overdue'"
              :to="localePath(`/client/invoices/${invoice.invoiceid}/pay`)"
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
              {{ invoice.duedate }}
            </div>
          </div>
          <div v-if="invoice.datepaid && !invoice.datepaid.startsWith('0000')">
            <div class="text-gray-500">{{ $t('client.invoices.datePaid') }}</div>
            <div class="text-gray-900 dark:text-white font-medium mt-0.5">{{ invoice.datepaid }}</div>
          </div>
          <div>
            <div class="text-gray-500">{{ $t('client.invoices.paymentMethod') }}</div>
            <div class="text-white font-medium mt-0.5 capitalize">{{ invoice.paymentmethod || '—' }}</div>
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
          <UiTableRow v-for="item in invoice.items?.item ?? []" :key="item.id">
            <UiTableTd class="text-gray-600 dark:text-gray-300">{{ item.description }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white font-medium">${{ item.amount }}</UiTableTd>
          </UiTableRow>
        </UiTableBody>
        <UiTableFoot>
          <UiTableRow :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.subtotal') }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white">${{ invoice.subtotal }}</UiTableTd>
          </UiTableRow>
          <UiTableRow v-if="parseFloat(invoice.tax) > 0" :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.tax') }}</UiTableTd>
            <UiTableTd align="right" class="text-gray-900 dark:text-white">${{ invoice.tax }}</UiTableTd>
          </UiTableRow>
          <UiTableRow v-if="parseFloat(invoice.credit) > 0" :hoverable="false">
            <UiTableTd class="text-gray-500 dark:text-gray-400">{{ $t('client.invoices.creditApplied') }}</UiTableTd>
            <UiTableTd align="right" class="text-green-400">-${{ invoice.credit }}</UiTableTd>
          </UiTableRow>
          <UiTableRow :hoverable="false" class="border-t border-white/10">
            <UiTableTd class="text-base font-bold text-gray-900 dark:text-white">{{ $t('client.invoices.total') }}</UiTableTd>
            <UiTableTd align="right" class="text-base font-bold text-gray-900 dark:text-white">${{ invoice.total }}</UiTableTd>
          </UiTableRow>
        </UiTableFoot>
      </UiTable>

      <!-- Pay Now banner for unpaid -->
      <UiAlert
        v-if="invoice.status === 'Unpaid' || invoice.status === 'Overdue'"
        class="mb-6"
      >
        {{ $t('client.invoices.unpaidBanner') }}
        <template #action>
          <NuxtLink
            :to="localePath(`/client/invoices/${invoice.invoiceid}/pay`)"
            class="px-5 py-2 rounded-xl bg-green-500 text-white font-bold text-sm hover:bg-green-400 transition-colors flex items-center gap-2 flex-shrink-0"
          >
            <CreditCard :size="16" :stroke-width="2" />
            {{ $t('client.invoices.payAmount') }} ${{ invoice.total }}
          </NuxtLink>
        </template>
      </UiAlert>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, AlertCircle, CreditCard } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const route = useRoute()
const localePath = useLocalePath()
const config = useRuntimeConfig()
const whmcsUrl = config.public.whmcsUrl

const { data: invoice, pending, error } = await useApi(`/api/portal/client/invoices/${route.params.id}`)

const isOverdue = computed(() => {
  if (!invoice.value) return false
  const inv = invoice.value as any
  return inv.status === 'Overdue'
})
</script>
