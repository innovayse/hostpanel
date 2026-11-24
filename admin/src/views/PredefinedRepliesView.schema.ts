/**
 * Skeleton schema of the predefined replies screen: a header over the strip of the three tabs, the add category card (the
 * tab the screen opens on), the breadcrumb line and the card of the categories table.
 *
 * The screen is a composite. `PredefinedRepliesView` draws the tab strip, the add forms (only the one of the chosen tab,
 * the reply one being {@link REPLY_FORM_PART}), the cards of the tables and their column heads from this data, and
 * `UiSkeletonOf` draws the same blocks while the view chunk is downloading, with the add category tab chosen.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { PredefinedRepliesTab } from '@/types/supporttab'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable, TabsSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** Key of the card of the categories table, the suffix of its `card-<key>` slot. */
export const REPLIES_CATEGORIES_CARD = 'categories'

/** Key of the card of the replies table. */
export const REPLIES_REPLIES_CARD = 'replies'

/** The strip of the three tabs, one per {@link PredefinedRepliesTab}. */
export const REPLIES_TABS_PART: TabsSkeletonPart = {
  kind: SkeletonPartKind.Tabs,
  tabs: [
    { key: PredefinedRepliesTab.Category, label: 'support.predefinedReplies.addCategory' },
    { key: PredefinedRepliesTab.Reply, label: 'support.predefinedReplies.addReply' },
    { key: PredefinedRepliesTab.Search, label: 'shared.search' },
  ],
}

/** The add category card: the name of the category. */
export const CATEGORY_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [[{ key: 'category', columns: 1, fields: [{ key: 'name', label: 'shared.categoryName', span: SkeletonSpan.Full }] }]],
}

/** The add reply card, drawn when its tab is chosen: name, category and the content of the reply. */
export const REPLY_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'reply',
        columns: 1,
        fields: [
          { key: 'name', label: 'shared.name', span: SkeletonSpan.Full },
          { key: 'categoryId', label: 'shared.category', span: SkeletonSpan.Full },
          { key: 'content', label: 'support.predefinedReplies.content', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

/** The categories table: the name and the row actions. */
export const REPLIES_CATEGORIES_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The replies table: the name, its category and the row actions. */
export const REPLIES_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
    { key: 'category', label: 'shared.category' },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The table of the search results: the name, the category and the content of each reply found. */
export const REPLIES_SEARCH_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name' },
    { key: 'category', label: 'shared.category' },
    { key: 'content', label: 'support.predefinedReplies.content', size: UiColumnSize.Grow },
  ],
}

/** The card of the categories table. */
export const REPLIES_CATEGORIES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: REPLIES_CATEGORIES_CARD, table: REPLIES_CATEGORIES_TABLE }]],
}

/** The card of the replies table, drawn when the reply tab is chosen. */
export const REPLIES_REPLIES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: REPLIES_REPLIES_CARD, table: REPLIES_TABLE }]],
}

/** What the predefined replies screen is made of; the breadcrumb line is a placeholder of the one line above the categories. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.SupportPredefinedReplies), subtitle: 'support.predefinedReplies.managePredefinedTicketReplyTemplates' },
  parts: [REPLIES_TABS_PART, CATEGORY_FORM_PART, { kind: SkeletonPartKind.Lines }, REPLIES_CATEGORIES_PART],
}

export default schema
