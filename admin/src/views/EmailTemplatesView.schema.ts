/**
 * What the email templates list is made of: a header with its help link, a search box with the group filter, and a table with one row per template.
 * `EmailTemplatesView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from the same
 * data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The email templates list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsEmailTemplates), actions: 1 },
  toolbar: { search: true, filters: ['settings.emailTemplates.groupFilter'] },
  table: {
    columns: [
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'subject', label: 'shared.subject' },
      { key: 'languages', label: 'settings.emailTemplates.languages' },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
