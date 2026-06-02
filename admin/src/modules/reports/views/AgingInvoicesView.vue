<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const rows = ref<{ invoiceId: number; client: string; amount: number; dueDate: string; daysOutstanding: number; bucket: string }[]>([])

const bucketColor = (b: string) => b === '0-30' ? 'text-status-green' : b === '31-60' ? 'text-status-yellow' : b === '61-90' ? 'text-orange-400' : 'text-status-red'
const bucketBg = (b: string) => b === '0-30' ? 'bg-status-green/10 text-status-green' : b === '31-60' ? 'bg-status-yellow/10 text-status-yellow' : b === '61-90' ? 'bg-orange-400/10 text-orange-400' : 'bg-status-red/10 text-status-red'
const dotColor = (b: string) => b === '0-30' ? 'bg-status-green' : b === '31-60' ? 'bg-status-yellow' : b === '61-90' ? 'bg-orange-400' : 'bg-status-red'

async function load() {
  loading.value = true; error.value = null
  try { rows.value = await request<typeof rows.value>('/reports/aging-invoices') }
  catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}
onMounted(load)
</script>

<template>
  <ReportPage title="Aging Invoices" description="Unpaid invoices grouped by days outstanding" :loading :error>
    <div class="flex items-center gap-4 mb-4 text-[0.78rem] text-text-secondary">
      <span v-for="b in [['0-30','bg-status-green'],['31-60','bg-status-yellow'],['61-90','bg-orange-400'],['90+','bg-status-red']]" :key="b[0]" class="flex items-center gap-1.5">
        <span :class="['w-2 h-2 rounded-full', b[1]]" />{{ b[0] }} days
      </span>
    </div>
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <div class="grid grid-cols-6 gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span v-for="h in ['Invoice #','Client','Amount','Due Date','Days Outstanding','Bucket']" :key="h" class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">{{ h }}</span>
      </div>
      <div v-if="rows.length === 0" class="px-5 py-8 text-center text-text-secondary text-[0.82rem]">No aging invoices found.</div>
      <div v-for="row in rows" :key="row.invoiceId" class="grid grid-cols-6 gap-4 px-5 py-3 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors text-[0.82rem] items-center">
        <span class="text-text-muted font-mono">#{{ row.invoiceId }}</span>
        <span class="text-text-primary">{{ row.client }}</span>
        <div class="flex items-center gap-1.5">
          <span :class="['w-2 h-2 rounded-full shrink-0', dotColor(row.bucket)]" />
          <span :class="['font-medium', bucketColor(row.bucket)]">${{ row.amount.toFixed(2) }}</span>
        </div>
        <span class="text-text-secondary">{{ row.dueDate }}</span>
        <span :class="['font-medium', bucketColor(row.bucket)]">{{ row.daysOutstanding }}d</span>
        <span :class="['text-[0.72rem] px-2 py-0.5 rounded-full font-medium', bucketBg(row.bucket)]">{{ row.bucket }}</span>
      </div>
    </div>
  </ReportPage>
</template>
