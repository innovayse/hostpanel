/**
 * Tests for the migration store parts the shared misc suite does not cover: the list load, the
 * create body defaults, the status poll and the log query. Real `useApi` transport, `fetch`
 * stubbed.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useMigrationStore } from '@/stores/migration'
import { json, problem, stubFetch, stubOffline, callOf } from '../helpers/fetch'
import { MigrationStatus } from '@/types/migration'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('migrationStore.fetchAll', () => {
  it('keeps the jobs and clears loading', async () => {
    stubFetch(json([{ id: 1 }]))
    const store = useMigrationStore()

    await store.fetchAll()
    expect(store.jobs).toEqual([{ id: 1 }])
    expect(store.loading).toBe(false)
  })

  it('keeps the API sentence on refusal', async () => {
    stubFetch(problem('Jobs unavailable.', 503))
    const store = useMigrationStore()

    await store.fetchAll()
    expect(store.error).toBe('Jobs unavailable.')
  })
})

describe('migrationStore.createJob', () => {
  it('sends every export flag, true unless the caller turned it off', async () => {
    const mock = stubFetch(json({ id: 2 }))

    await useMigrationStore().createJob({ sourceUrl: 'https://old.example', exportQuotes: false })
    expect(callOf(mock).body).toMatchObject({
      label: null,
      sourceUrl: 'https://old.example',
      selection: {
        clients: true,
        quotes: false,
        announcements: true,
        downloads: true,
        networkIssues: true,
      },
    })
  })
})

describe('migrationStore polling', () => {
  it('a read that found nothing new leaves the held job objects alone', async () => {
    stubFetch(json({ id: 5, status: 'InProgress', overallPercent: 10 }))
    const store = useMigrationStore()
    store.jobs.push({ id: 5, status: MigrationStatus.InProgress, overallPercent: 10 } as never)
    store.setActiveJob(store.jobs[0] as never)
    const heldInList = store.jobs[0]
    const heldActive = store.activeJob

    await store.pollStatus(5)
    expect(store.jobs[0]).toBe(heldInList)
    expect(store.activeJob).toBe(heldActive)
  })

  it('pollStatus refreshes the list and the watched job when it is that job', async () => {
    stubFetch(json({ id: 5, status: 'Completed' }))
    const store = useMigrationStore()
    store.jobs.push({ id: 5, status: MigrationStatus.InProgress } as never)
    store.setActiveJob(store.jobs[0] as never)

    await store.pollStatus(5)
    expect(store.activeJob).toEqual({ id: 5, status: 'Completed' })
    expect(store.jobs[0]?.status).toBe(MigrationStatus.Completed)
  })

  it('a manual selection survives a poll refresh of another job', async () => {
    vi.useFakeTimers()
    const mock = stubFetch(json({ id: 2, status: 'InProgress' }))
    const store = useMigrationStore()
    store.jobs.push({ id: 1, status: MigrationStatus.Completed } as never, { id: 2, status: MigrationStatus.Pending } as never)
    store.setActiveJob(store.jobs[1] as never)
    store.startPolling(2)

    // The operator chooses the finished first job while the pending one is still being polled.
    store.setActiveJob(store.jobs[0] as never)
    await vi.advanceTimersByTimeAsync(4000)

    expect(store.activeJob?.id).toBe(1)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('a poll tick that answers after the selection moved leaves the selection and the list copy of that job refreshed', async () => {
    stubFetch(json({ id: 2, status: 'InProgress', overallPercent: 50 }))
    const store = useMigrationStore()
    store.jobs.push({ id: 1, status: MigrationStatus.Completed } as never, { id: 2, status: MigrationStatus.Pending } as never)
    store.setActiveJob(store.jobs[0] as never)

    await store.pollStatus(2)
    expect(store.activeJob?.id).toBe(1)
    expect(store.jobs[1]).toMatchObject({ id: 2, overallPercent: 50 })
  })

  it('the watched job is cleared only when it vanishes from the list', async () => {
    stubFetch(new Response(null, { status: 204 }))
    const store = useMigrationStore()
    store.jobs.push({ id: 1 } as never, { id: 2 } as never)
    store.setActiveJob(store.jobs[0] as never)

    await store.deleteJob(2)
    expect(store.activeJob?.id).toBe(1)
    await store.deleteJob(1)
    expect(store.activeJob).toBeNull()
  })

  it('a failed poll leaves the state and no sentence', async () => {
    stubFetch(problem('Gone.', 404))
    const store = useMigrationStore()
    store.setActiveJob({ id: 5, status: MigrationStatus.InProgress } as never)

    await store.pollStatus(5)
    expect(store.activeJob?.status).toBe(MigrationStatus.InProgress)
    expect(store.error).toBeNull()
  })

  it('startPolling reads at once and again every two seconds until stopped', async () => {
    vi.useFakeTimers()
    const mock = stubFetch(json({ id: 5, status: 'InProgress' }))
    const store = useMigrationStore()

    store.startPolling(5)
    await vi.advanceTimersByTimeAsync(0)
    expect(mock).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(2000)
    expect(mock).toHaveBeenCalledTimes(2)
    store.stopPolling()
    await vi.advanceTimersByTimeAsync(4000)
    expect(mock).toHaveBeenCalledTimes(2)
  })
})

describe('migrationStore.fetchLogs', () => {
  it('returns the page it asked for', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50, totalPages: 0 }))

    expect(await useMigrationStore().fetchLogs(5, { page: 1, pageSize: 50 })).toMatchObject({ pageNumber: 1 })
    expect(callOf(mock).url).toBe('/api/admin/migrations/5/logs?page=1&pageSize=50')
  })

  it('returns null with the API sentence on refusal', async () => {
    stubFetch(problem('No such job.', 404))
    const store = useMigrationStore()

    expect(await store.fetchLogs(5, { page: 1, pageSize: 50 })).toBeNull()
    expect(store.jobError).toBe('No such job.')
  })
})

describe('migrationStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('createJob puts the job first and returns it', async () => {
    stubFetch(json({ id: 3 }))
    const store = useMigrationStore()

    expect(await store.createJob({ sourceUrl: 'https://old.example' })).toEqual({ id: 3 })
    expect(store.jobs[0]).toEqual({ id: 3 })
  })

  it('createJob returns null with the API sentence on refusal', async () => {
    stubFetch(problem('Source unreachable.'))
    const store = useMigrationStore()

    expect(await store.createJob({ sourceUrl: 'x' })).toBeNull()
    expect(store.error).toBe('Source unreachable.')
  })

  it('testConnection replaces the job in the list and the active slot', async () => {
    const store = useMigrationStore()
    store.jobs.push({ id: 3, pluginConnected: false } as never)
    store.setActiveJob({ id: 3, pluginConnected: false } as never)
    stubFetch(json({ id: 3, pluginConnected: true }))

    expect((await store.testConnection(3))?.pluginConnected).toBe(true)
    expect(store.jobs[0]?.pluginConnected).toBe(true)
    expect(store.activeJob?.pluginConnected).toBe(true)
  })

  it('startImport returns null offline', async () => {
    stubOffline()
    const store = useMigrationStore()

    expect(await store.startImport(3)).toBeNull()
    expect(store.jobError).toBe('Could not reach the server. Check your connection and try again.')
    expect(store.error).toBeNull()
  })

  it('retryImport returns null offline and stores the queued job on success', async () => {
    stubOffline()
    const store = useMigrationStore()
    expect(await store.retryImport(3)).toBeNull()
    expect(store.jobError).toBe('Could not reach the server. Check your connection and try again.')

    vi.unstubAllGlobals()
    store.jobs.push({ id: 3, status: MigrationStatus.Failed } as never)
    stubFetch(json({ id: 3, status: MigrationStatus.Queued }))
    expect((await store.retryImport(3))?.status).toBe(MigrationStatus.Queued)
    expect(store.jobs[0]?.status).toBe(MigrationStatus.Queued)
  })

  it('deleteJob removes the job on success and keeps it on refusal', async () => {
    const store = useMigrationStore()
    store.jobs.push({ id: 3 } as never)
    stubFetch(problem('Job is running.', 409))
    expect(await store.deleteJob(3)).toBe(false)
    expect(store.jobs).toHaveLength(1)

    stubFetch(json({}))
    expect(await store.deleteJob(3)).toBe(true)
    expect(store.jobs).toHaveLength(0)
  })

  it('fetchLogs returns the page, null when refused', async () => {
    const store = useMigrationStore()
    stubFetch(json({ items: [], totalCount: 0 }))
    expect(await store.fetchLogs(3, { page: 2, pageSize: 50 })).toEqual({ items: [], totalCount: 0 })

    stubFetch(problem('No log.', 404))
    expect(await store.fetchLogs(3, { page: 1, pageSize: 50 })).toBeNull()
    expect(store.jobError).toBe('No log.')
  })
})
