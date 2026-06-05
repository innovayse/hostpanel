<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row { serviceId: number; clientName: string; productName: string; domain: string | null; nextDueDate: string | null; suspendReason: string | null }

const rows = ref<Row[]>([])

async function load() {
  loading.value = true; error.value = null
  try {
    rows.value = await request<Row[]>('/reports/product-suspensions')
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Product Suspensions" description="This report allows you to review all suspended products and the reasons specified for their suspensions." :loading :error>
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Service ID</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Client Name</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product Name</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Domain</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Next Due Date</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Suspend Reason</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0">
            <td colspan="6" class="px-5 py-8 text-center text-text-secondary">No Data Found For This Report</td>
          </tr>
          <tr v-for="row in rows" :key="row.serviceId"
            class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td class="px-5 py-2.5 font-mono text-text-muted">{{ row.serviceId }}</td>
            <td class="px-5 py-2.5 text-text-primary">{{ row.clientName }}</td>
            <td class="px-5 py-2.5 text-text-primary">{{ row.productName }}</td>
            <td class="px-5 py-2.5 text-text-secondary">{{ row.domain ?? '—' }}</td>
            <td class="px-5 py-2.5 font-mono" :class="row.nextDueDate ? 'text-status-red' : 'text-text-muted'">{{ row.nextDueDate ?? '—' }}</td>
            <td class="px-5 py-2.5 text-text-secondary italic">{{ row.suspendReason ?? '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <ReportTimestamp />
  </ReportPage>
</template>
