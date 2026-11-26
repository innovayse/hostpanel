/**
 * Skeleton schema of the downloads screen: a header over the strip of the two add tabs, the add category card (the tab
 * the screen opens on), the breadcrumb line, the card of the categories at the level browsed and the card of the files.
 *
 * The screen is a composite. `DownloadsView` draws the tab strip, both add forms (only the one of the chosen tab, the
 * download one being {@link DOWNLOAD_FILE_FORM_PART}) and the cards with the heads of their tables from this data, and
 * `UiSkeletonOf` draws the same blocks while the view chunk is downloading, with the add category tab chosen.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { DownloadsTab } from '@/types/supporttab'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable, TabsSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** Key of the card of the categories table, the suffix of its `card-<key>` slot. */
export const DOWNLOAD_CATEGORIES_CARD = 'categories'

/** Key of the card of the files table. */
export const DOWNLOAD_FILES_CARD = 'files'

/** The strip of the two add tabs, one per {@link DownloadsTab}. */
export const DOWNLOADS_TABS_PART: TabsSkeletonPart = {
  kind: SkeletonPartKind.Tabs,
  tabs: [
    { key: DownloadsTab.Category, label: 'shared.createCategory' },
    { key: DownloadsTab.Download, label: 'support.downloads.createDownload' },
  ],
}

/** The add category card: its name, the hidden flag and its description, one under the other. */
export const DOWNLOAD_CATEGORY_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'category',
        columns: 1,
        fields: [
          { key: 'name', label: 'shared.categoryName', span: SkeletonSpan.Full },
          { key: 'hidden', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'description', label: 'shared.description', span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

/** The add download card, drawn when its tab is chosen: type, title, description, file name, category and three flags. */
export const DOWNLOAD_FILE_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'download',
        columns: 1,
        fields: [
          { key: 'type', label: 'shared.type', span: SkeletonSpan.Full },
          { key: 'title', label: 'shared.title', span: SkeletonSpan.Full },
          { key: 'description', label: 'shared.description', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'filename', label: 'support.downloads.filename', span: SkeletonSpan.Full },
          { key: 'categoryId', label: 'shared.category', span: SkeletonSpan.Full },
          { key: 'clientsOnly', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'productDownload', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'hidden', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

/** The categories table: name, description, status and the row actions. */
export const DOWNLOAD_CATEGORIES_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
    { key: 'description', label: 'shared.description' },
    { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    { key: 'actions', actions: true },
  ],
}

/** The files table: title, description, download count, status and the row actions. */
export const DOWNLOAD_FILES_TABLE: SkeletonTable = {
  columns: [
    { key: 'title', label: 'shared.title', size: UiColumnSize.Grow },
    { key: 'description', label: 'shared.description' },
    { key: 'downloads', label: 'support.downloads.downloads', numeric: true, size: UiColumnSize.Compact },
    { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    { key: 'actions', actions: true },
  ],
}

/** The card of the categories at the level browsed. */
export const DOWNLOAD_CATEGORIES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: DOWNLOAD_CATEGORIES_CARD, title: 'shared.categories', table: DOWNLOAD_CATEGORIES_TABLE }]],
}

/** The card of the files of the level browsed. */
export const DOWNLOAD_FILES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: DOWNLOAD_FILES_CARD, title: 'support.downloads.files', table: DOWNLOAD_FILES_TABLE }]],
}

/** What the downloads screen is made of; the breadcrumb line is a placeholder of text made of the categories browsed. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.SupportDownloads), subtitle: 'support.downloads.manageDownloadableFilesAndCategories' },
  parts: [
    DOWNLOADS_TABS_PART,
    DOWNLOAD_CATEGORY_FORM_PART,
    { kind: SkeletonPartKind.Lines },
    DOWNLOAD_CATEGORIES_PART,
    DOWNLOAD_FILES_PART,
  ],
}

export default schema
