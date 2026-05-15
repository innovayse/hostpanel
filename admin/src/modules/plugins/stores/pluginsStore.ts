import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { PluginListItemDto, PluginActionResultDto } from '../types/plugin.types'

/**
 * Pinia store for managing installed plugins.
 *
 * Handles list, install (multipart ZIP), remove, and restart operations.
 */
export const usePluginsStore = defineStore('plugins', () => {
  const { request } = useApi()

  /** Installed plugin list, populated by {@link fetchAll}. */
  const plugins = ref<PluginListItemDto[]>([])

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Whether a server restart is required for pending plugin changes to take effect.
   * Set to true after a successful install or remove, cleared after restart.
   */
  const requiresRestart = ref(false)

  /**
   * Fetches the list of all installed plugins.
   *
   * @returns Promise that resolves when the list is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      plugins.value = await request<PluginListItemDto[]>('/admin/plugins')
    } catch {
      error.value = 'Failed to load plugins.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Uploads and installs a plugin from a ZIP file.
   *
   * @param file - The ZIP file selected by the admin.
   * @returns Promise that resolves when the install request completes.
   */
  async function install(file: File): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const form = new FormData()
      form.append('file', file)

      const token = localStorage.getItem('admin_token') ?? ''
      const res = await fetch('/api/admin/plugins/install', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        body: form,
      })

      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || `HTTP ${res.status}`)
      }

      const result: PluginActionResultDto = await res.json()
      requiresRestart.value = result.requiresRestart
      await fetchAll()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to install plugin.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Removes an installed plugin by its identifier.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns Promise that resolves when the remove request completes.
   */
  async function remove(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PluginActionResultDto>(`/admin/plugins/${id}`, {
        method: 'DELETE',
      })
      requiresRestart.value = result.requiresRestart
      plugins.value = plugins.value.filter(p => p.id !== id)
    } catch {
      error.value = 'Failed to remove plugin.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Triggers a graceful server restart so installed/removed plugins take effect.
   * After calling this, poll /api/health until the server responds.
   *
   * @returns Promise that resolves when the restart request is sent.
   */
  async function restart(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/admin/plugins/restart', { method: 'POST' })
      requiresRestart.value = false
    } catch {
      // Server may disconnect immediately — that's expected behaviour
      requiresRestart.value = false
    } finally {
      loading.value = false
    }
  }

  return { plugins, loading, error, requiresRestart, fetchAll, install, remove, restart }
})
