/**
 * Pinia store for managing installed plugins.
 *
 * Handles list, install (multipart ZIP), remove, and restart operations. The only caller of
 * `usePluginsApi`; a refused load is kept as the API's own sentence in `error`, a refused install or
 * remove in `actionError`, never thrown.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { usePluginsApi } from '@/composables/apis/usePluginsApi'
import { runAction } from '@/lib/runAction'
import type { PluginListItemDto } from '@/types/plugin'

/** Holds the installed plugin list and whether a restart is pending. */
export const usePluginsStore = defineStore('plugins', () => {
  const api = usePluginsApi()

  /** Installed plugin list, populated by {@link fetchAll}. */
  const plugins = ref<PluginListItemDto[]>([])

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused install or remove, null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Whether a server restart is required for pending plugin changes to take effect.
   * Set to true after a successful install or remove, cleared after restart.
   */
  const requiresRestart = ref(false)

  /**
   * Fetches the list of all installed plugins.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listPlugins)
    loading.value = false
    if (result.ok) {
      plugins.value = result.value
    }
  }

  /**
   * Uploads and installs a plugin from a ZIP file, then refreshes the list.
   *
   * @param file - The ZIP file selected by the admin.
   * @returns True when installed; false when refused (see `actionError`).
   */
  const install = async (file: File): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, () => api.installPlugin(file))
    loading.value = false
    if (result.ok) {
      requiresRestart.value = result.value.requiresRestart
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Removes an installed plugin by its identifier.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns True when removed; false when refused (see `actionError`).
   */
  const remove = async (id: string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, () => api.removePlugin(id))
    loading.value = false
    if (result.ok) {
      // Only ever raised here: a removal that needs no restart (the files were already gone) must not hide the banner
      // an earlier install or removal still needs.
      if (result.value.requiresRestart) {
        requiresRestart.value = true
      }
      plugins.value = plugins.value.filter(p => p.id !== id)
    }
    return result.ok
  }

  /**
   * Triggers a graceful server restart so installed/removed plugins take effect.
   * After calling this, poll GET /health until the server responds.
   *
   * @returns Resolves when the restart request is over.
   */
  const restart = async (): Promise<void> => {
    loading.value = true
    /** Local sink: a server that drops the connection while restarting is expected, not an error. */
    const ignored = ref<string | null>(null)
    await runAction(ignored, api.restartServer)
    requiresRestart.value = false
    loading.value = false
  }

  return { plugins, loading, error, actionError, requiresRestart, fetchAll, install, remove, restart }
})
