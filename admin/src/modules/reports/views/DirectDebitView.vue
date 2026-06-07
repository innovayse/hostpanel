<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row {
  invoiceId: number; clientName: string; invoiceDate: string; dueDate: string
  subtotal: number; tax: number; credit: number; total: number
  bankName: string | null; bankAccountType: string | null; bankCode: string | null; bankAccountNumber: string | null
}
interface ReportData { rows: Row[] }

const data = ref<ReportData | null>(null)

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>('/reports/direct-debit')
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage
    title="Direct Debit Processing"
    description="This report displays all unpaid invoices assigned to the Direct Debit payment method and the associated bank account details stored for their owners ready for processing."
    :loading :error>

    <template v-if="data">
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice ID</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Invoice Date</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Due Date</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Subtotal</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Tax</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Credit</th>
              <th class="px-4 py-3 text-right  text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Bank Name</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Bank Account Type</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Bank Code</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Bank Account Number</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="12" class="px-4 py-8 text-center text-text-secondary">No Data Found For This Report</td>
            </tr>
            <tr v-for="row in data.rows" :key="row.invoiceId"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 font-mono text-text-muted">{{ row.invoiceId }}</td>
              <td class="px-4 py-2.5 text-text-primary">{{ row.clientName }}</td>
              <td class="px-4 py-2.5 text-center font-mono text-text-secondary">{{ row.invoiceDate }}</td>
              <td class="px-4 py-2.5 text-center font-mono text-text-secondary">{{ row.dueDate }}</td>
              <td class="px-4 py-2.5 text-right text-text-primary">${{ row.subtotal.toFixed(2) }}</td>
              <td class="px-4 py-2.5 text-right" :class="row.tax > 0 ? 'text-status-yellow' : 'text-text-muted'">${{ row.tax.toFixed(2) }}</td>
              <td class="px-4 py-2.5 text-right" :class="row.credit > 0 ? 'text-status-red' : 'text-text-muted'">${{ row.credit.toFixed(2) }}</td>
              <td class="px-4 py-2.5 text-right font-semibold text-status-green">${{ row.total.toFixed(2) }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.bankName ?? '—' }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.bankAccountType ?? '—' }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.bankCode ?? '—' }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.bankAccountNumber ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
    <ReportTimestamp />
  </ReportPage>
</template>
