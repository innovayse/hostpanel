/**
 * Skeleton schema of the system settings screen: a header over a strip of four tabs.
 *
 * This is the one list of the tabs: the screen draws its strip and its panels from it, and the catalogue in
 * `lib/settingsTabs.ts` only says which cards each tab holds. The keys are the values the address keeps in `?tab=`. The
 * routed child of a tab layout does not apply here, the panels sit inside the screen, so the skeleton shows the header
 * and the strip.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SettingsTab } from '@/types/setting'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the system settings screen is made of. */
const schema: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  header: {
    title: navTitleKey(AppRoute.SettingsGeneral),
  },
  tabs: [
    { key: SettingsTab.General, label: 'settings.tabs.general' },
    { key: SettingsTab.Appearance, label: 'settings.tabs.appearance' },
    { key: SettingsTab.Integrations, label: 'settings.tabs.integrations' },
    { key: SettingsTab.Advanced, label: 'settings.tabs.advanced' },
  ],
}

export default schema
