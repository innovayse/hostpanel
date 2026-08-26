/**
 * Composable for fetching the list of Innovayse apps from the central portal API.
 *
 * Apps are loaded lazily on the first call to `open()` and cached for the
 * lifetime of the component. If the API is unreachable the list stays empty
 * and the launcher renders nothing rather than crashing.
 */

/** A single app entry returned by the portal API. */
export interface AppEntry {
  id: string
  name: string
  desc: string
  url: string
  comingSoon: boolean
}

/** Maps app IDs to Lucide icon names. */
const ICON_MAP: Record<string, string> = {
  home:      'lucide:home',
  account:   'lucide:user-circle',
  tasks:     'lucide:list-checks',
  hostpanel: 'lucide:server',
  erp:       'lucide:building-2',
  sheets:    'lucide:table',
  email:     'lucide:mail',
  docs:      'lucide:file-text',
  calendar:  'lucide:calendar',
  drive:     'lucide:hard-drive',
}

/** @returns icon name for the given app id, falling back to a generic box icon. */
export function appIcon(id: string): string {
  return ICON_MAP[id] ?? 'lucide:box'
}

/**
 * Fetches and caches the Innovayse app list from `/api/portal/public/apps`.
 *
 * @example
 * const { apps, fetchApps } = useAppLauncher()
 */
export function useAppLauncher() {
  const config = useRuntimeConfig()
  const apps = ref<(AppEntry & { icon: string })[]>([])

  /** Fetches apps from the portal API and populates `apps`. No-op if already loaded. */
  async function fetchApps() {
    if (apps.value.length > 0) return
    try {
      const data: AppEntry[] = await fetch(`${config.public.mainUrl}/api/portal/public/apps`).then(r => r.json())
      apps.value = data.map(app => ({ ...app, icon: appIcon(app.id) }))
    } catch {
      // API unreachable — leave list empty
    }
  }

  return { apps, fetchApps }
}
