import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { MigrationJob, MigrationLogPage } from '../types/migration.types'

export const useMigrationStore = defineStore('migration', () => {
  const { request } = useApi()

  const jobs = ref<MigrationJob[]>([])
  const activeJob = ref<MigrationJob | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  let pollTimer: ReturnType<typeof setInterval> | null = null

  async function fetchAll() {
    loading.value = true
    error.value = null
    try {
      jobs.value = await request<MigrationJob[]>('/admin/migrations')
    } catch {
      error.value = 'Failed to load migration jobs.'
    } finally {
      loading.value = false
    }
  }

  async function deleteJob(id: number): Promise<void> {
    try {
      await request(`/admin/migrations/${id}`, { method: 'DELETE' })
      jobs.value = jobs.value.filter(j => j.id !== id)
      if (activeJob.value?.id === id) {
        stopPolling()
        activeJob.value = null
      }
    } catch {
      error.value = 'Failed to delete migration job.'
      throw error.value
    }
  }

  async function createJob(opts: {
    label?: string
    sourceUrl: string
    exportClients?: boolean
    exportInvoices?: boolean
    exportServices?: boolean
    exportDomains?: boolean
    exportTickets?: boolean
    exportProducts?: boolean
    exportOrders?: boolean
    exportTransactions?: boolean
    exportQuotes?: boolean
    exportKnowledgebase?: boolean
    exportContacts?: boolean
    exportTicketReplies?: boolean
  }): Promise<MigrationJob> {
    const job = await request<MigrationJob>('/admin/migrations', {
      method: 'POST',
      body: JSON.stringify({
        label: opts.label ?? null,
        sourceUrl: opts.sourceUrl,
        exportClients:       opts.exportClients       ?? true,
        exportInvoices:      opts.exportInvoices      ?? true,
        exportServices:      opts.exportServices      ?? true,
        exportDomains:       opts.exportDomains       ?? true,
        exportTickets:       opts.exportTickets       ?? true,
        exportProducts:      opts.exportProducts      ?? true,
        exportOrders:        opts.exportOrders        ?? true,
        exportTransactions:  opts.exportTransactions  ?? true,
        exportQuotes:        opts.exportQuotes        ?? true,
        exportKnowledgebase: opts.exportKnowledgebase ?? true,
        exportContacts:      opts.exportContacts      ?? true,
        exportTicketReplies: opts.exportTicketReplies ?? true,
      }),
    })
    jobs.value.unshift(job)
    return job
  }

  async function testConnection(id: number): Promise<MigrationJob> {
    const job = await request<MigrationJob>(`/admin/migrations/${id}/test-connection`, { method: 'POST' })
    const idx = jobs.value.findIndex(j => j.id === id)
    if (idx >= 0) jobs.value[idx] = job
    if (activeJob.value?.id === id) activeJob.value = job
    return job
  }

  async function startImport(id: number): Promise<MigrationJob> {
    const job = await request<MigrationJob>(`/admin/migrations/${id}/start`, { method: 'POST' })
    const idx = jobs.value.findIndex(j => j.id === id)
    if (idx >= 0) jobs.value[idx] = job
    if (activeJob.value?.id === id) activeJob.value = job
    return job
  }

  async function pollStatus(jobId: number) {
    try {
      const job = await request<MigrationJob>(`/admin/migrations/${jobId}`)
      activeJob.value = job

      // Update in list
      const idx = jobs.value.findIndex(j => j.id === jobId)
      if (idx >= 0) jobs.value[idx] = job

      // Stop polling when done
      if (job.status === 'Completed' || job.status === 'Failed') {
        stopPolling()
      }
    } catch {
      stopPolling()
    }
  }

  function startPolling(jobId: number) {
    stopPolling()
    pollStatus(jobId)
    pollTimer = setInterval(() => pollStatus(jobId), 2000)
  }

  function stopPolling() {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  function setActiveJob(job: MigrationJob) {
    activeJob.value = job
  }

  async function fetchLogs(
    jobId: number,
    opts: { action?: string; entityType?: string; page?: number; pageSize?: number } = {}
  ): Promise<MigrationLogPage> {
    const params = new URLSearchParams()
    if (opts.action) params.set('action', opts.action)
    if (opts.entityType) params.set('entityType', opts.entityType)
    params.set('page', String(opts.page ?? 1))
    params.set('pageSize', String(opts.pageSize ?? 50))
    return request<MigrationLogPage>(`/admin/migrations/${jobId}/logs?${params}`)
  }

  return { jobs, activeJob, loading, error, fetchAll, createJob, deleteJob, pollStatus, startPolling, stopPolling, setActiveJob, testConnection, startImport, fetchLogs }
})
