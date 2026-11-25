/**
 * Pinia store for migration jobs: the job list, the job being watched, status polling, the
 * connection probe, start and the import log.
 *
 * The only caller of `useMigrationApi`; a refusal is kept as the API's own sentence, never
 * thrown: in `error` for the list and the job lifecycle, in `jobError` for the requests about
 * one job's run (connection check, import start, import log), which the card that made the request
 * shows itself.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useMigrationApi } from '@/composables/apis/useMigrationApi'
import { runAction } from '@/lib/runAction'
import { MigrationStatus } from '@/types/migration'
import type { PaginatedList } from '@/types/paginatedlist'
import type {
  CreateMigrationJobOptions,
  MigrationJob,
  MigrationLogEntry,
  MigrationLogQuery,
} from '@/types/migration'
import { usePoller } from '@/composables/usePoller'
import { isSameJob } from '@/lib/migrationStatus'
import { PollKind } from '@/types/poller'

/** Holds migration jobs and drives their lifecycle from the admin panel. */
export const useMigrationStore = defineStore('migration', () => {
  const api = useMigrationApi()

  /** All migration jobs, newest first. */
  const jobs = ref<MigrationJob[]>([])

  /** The job being watched, null when none. */
  const activeJob = ref<MigrationJob | null>(null)

  /** True while the job list is loading. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused connection check, import start or log read, null when none. */
  const jobError = ref<string | null>(null)

  /** Id of the job the running poll follows, null when not polling. */
  let polledJobId: number | null = null

  /** The shared poller that re-reads the followed job; it pauses with the tab and never overlaps its own reads. */
  const poller = usePoller({
    kind: PollKind.MigrationStatus,
    /** Polls the status of the followed job; nothing to poll while no job is followed. */
    tick: () => (polledJobId === null ? undefined : pollStatus(polledJobId)),
  })

  /**
   * Fetches all migration jobs.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listJobs)
    loading.value = false
    if (result.ok) {
      jobs.value = result.value
    }
  }

  /**
   * Stops the status poll, if one is running.
   *
   * @returns Nothing.
   */
  const stopPolling = (): void => {
    poller.stop()
    polledJobId = null
  }

  /**
   * Deletes a migration job and drops it from the list.
   *
   * @param id - Migration job id.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteJob = async (id: number): Promise<boolean> => {
    const result = await runAction(error, () => api.deleteJob(id))
    if (result.ok) {
      jobs.value = jobs.value.filter(j => j.id !== id)
      if (activeJob.value?.id === id) {
        stopPolling()
        activeJob.value = null
      }
    }
    return result.ok
  }

  /**
   * Creates a migration job and puts it first in the list.
   *
   * @param opts - Label, source URL and the entity groups to export (each defaults to true).
   * @returns The created job, or null when refused (see `error`).
   */
  const createJob = async (opts: CreateMigrationJobOptions): Promise<MigrationJob | null> => {
    const created = await runAction(error, () => api.createJob({
      label: opts.label ?? null,
      sourceUrl: opts.sourceUrl,
      selection: {
        clients: opts.exportClients ?? true,
        invoices: opts.exportInvoices ?? true,
        services: opts.exportServices ?? true,
        domains: opts.exportDomains ?? true,
        tickets: opts.exportTickets ?? true,
        products: opts.exportProducts ?? true,
        orders: opts.exportOrders ?? true,
        transactions: opts.exportTransactions ?? true,
        quotes: opts.exportQuotes ?? true,
        knowledgebase: opts.exportKnowledgebase ?? true,
        contacts: opts.exportContacts ?? true,
        ticketReplies: opts.exportTicketReplies ?? true,
        announcements: opts.exportAnnouncements ?? true,
        downloads: opts.exportDownloads ?? true,
        networkIssues: opts.exportNetworkIssues ?? true,
      },
    }))
    if (!created.ok) {
      return null
    }
    jobs.value.unshift(created.value)
    return created.value
  }

  /**
   * Replaces a job in the list (and the active slot) with a fresh copy from the API. A copy that reads the same as the
   * one held changes nothing, so a poll tick that found no news re-renders nothing.
   *
   * @param job - The job as the API returned it.
   * @returns Nothing.
   */
  const replaceJob = (job: MigrationJob): void => {
    const idx = jobs.value.findIndex(j => j.id === job.id)
    if (idx >= 0 && !isSameJob(jobs.value[idx], job)) {
      jobs.value[idx] = job
    }
    if (activeJob.value?.id === job.id && !isSameJob(activeJob.value, job)) {
      activeJob.value = job
    }
  }

  /**
   * Probes the source plugin connection of a job.
   *
   * @param id - Migration job id.
   * @returns The updated job, or null when refused (see `jobError`).
   */
  const testConnection = async (id: number): Promise<MigrationJob | null> => {
    const result = await runAction(jobError, () => api.testJobConnection(id))
    if (!result.ok) {
      return null
    }
    replaceJob(result.value)
    return result.value
  }

  /**
   * Starts the import of a job.
   *
   * @param id - Migration job id.
   * @returns The updated job, or null when refused (see `jobError`).
   */
  const startImport = async (id: number): Promise<MigrationJob | null> => {
    const result = await runAction(jobError, () => api.startJob(id))
    if (!result.ok) {
      return null
    }
    replaceJob(result.value)
    return result.value
  }

  /**
   * Queues a failed job's import again.
   *
   * @param id - Migration job id.
   * @returns The updated job, or null when refused (see `jobError`).
   */
  const retryImport = async (id: number): Promise<MigrationJob | null> => {
    const result = await runAction(jobError, () => api.retryJob(id))
    if (!result.ok) {
      return null
    }
    replaceJob(result.value)
    return result.value
  }

  /**
   * Re-reads one job and refreshes its copy in the list. The poll only updates data: the job being watched
   * is left as the operator chose it (it is refreshed only when it is this very job), and the poll ends once
   * the job is finished.
   *
   * A failed read ends the poll without surfacing a sentence: the next list load reports it. A read of a job
   * the poll has since moved away from (the operator chose another one meanwhile) never ends the newer poll.
   *
   * @param jobId - Migration job id.
   * @returns Resolves when the read is over.
   */
  const pollStatus = async (jobId: number): Promise<void> => {
    /** Local sink: a failed poll tick only ends the poll. */
    const ignored = ref<string | null>(null)
    const result = await runAction(ignored, () => api.getJob(jobId))
    if (!result.ok) {
      if (polledJobId === jobId) {
        stopPolling()
      }
      return
    }
    replaceJob(result.value)
    const finished = result.value.status === MigrationStatus.Completed || result.value.status === MigrationStatus.Failed
    if (finished && polledJobId === jobId) {
      stopPolling()
    }
  }

  /**
   * Starts polling a job's status, replacing any poll already running.
   *
   * @param jobId - Migration job id.
   * @returns Nothing.
   */
  const startPolling = (jobId: number): void => {
    stopPolling()
    polledJobId = jobId
    poller.start()
  }

  /**
   * Sets the job being watched. A poll that follows another job is stopped: it would only refresh data
   * the operator is no longer looking at.
   *
   * @param job - The job to watch.
   * @returns Nothing.
   */
  const setActiveJob = (job: MigrationJob): void => {
    if (polledJobId !== job.id) {
      stopPolling()
    }
    activeJob.value = job
  }

  /**
   * Loads one page of a job's import log.
   *
   * @param jobId - Migration job id.
   * @param opts - Optional action/entity filters and the page to read.
   * @returns The log page, or null when refused (see `jobError`).
   */
  const fetchLogs = async (jobId: number, opts: MigrationLogQuery): Promise<PaginatedList<MigrationLogEntry> | null> => {
    const result = await runAction(jobError, () => api.getJobLogs(jobId, opts))
    return result.ok ? result.value : null
  }

  return { jobs, activeJob, loading, error, jobError, fetchAll, createJob, deleteJob, pollStatus, startPolling, stopPolling, setActiveJob, testConnection, startImport, retryImport, fetchLogs }
})
