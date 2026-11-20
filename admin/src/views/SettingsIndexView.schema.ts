/**
 * Skeleton schema of the settings index: a header over one card per group of the catalogue.
 *
 * The screen has no data to wait for, it draws the catalogue {@link SETTINGS_INDEX}; so the schema is made from that
 * catalogue and lists nothing of its own. Each group is a card titled with the group's heading, with one row per section
 * captioned with the section's name, so a new section or group shows in the skeleton the moment it is catalogued.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SETTINGS_INDEX } from '@/lib/settingsIndex'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the settings index is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: {
    title: navTitleKey(AppRoute.Settings),
    subtitle: 'settings.index.subtitle',
  },
  columns: [
    SETTINGS_INDEX.map((group) => ({
      key: group.group,
      title: `settings.index.groups.${group.group}`,
      rows: group.entries.map((entry) => `settings.index.items.${entry.item}.title`),
    })),
  ],
}

export default schema
