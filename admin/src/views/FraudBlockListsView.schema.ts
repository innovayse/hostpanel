/**
 * Skeleton schema of the fraud block lists screen: a header over the strip of the four lists, the card that adds an entry
 * to the list on show, the search box and the table of the list's entries with its pager.
 *
 * The screen is a composite. `FraudBlockListsView` draws the tab strip, the table head and the search box from this data,
 * and `BlockListAddForm` draws the add card from {@link blockListAddPart}, whose value caption is named for the list on show
 * (the skeleton shows the first list's). `UiSkeletonOf` draws the same blocks while the view chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { BlockListType } from '@/types/fraud'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable, TabsSkeletonPart, ToolbarSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The strip of the four lists, one tab per {@link BlockListType}, in the order the operator reads them. */
export const BLOCK_LIST_TABS_PART: TabsSkeletonPart = {
  kind: SkeletonPartKind.Tabs,
  tabs: [
    { key: BlockListType.Ip, label: 'constants.blockListTypeOptions.ip' },
    { key: BlockListType.Cidr, label: 'constants.blockListTypeOptions.cidr' },
    { key: BlockListType.EmailDomain, label: 'constants.blockListTypeOptions.emailDomain' },
    { key: BlockListType.Country, label: 'constants.blockListTypeOptions.country' },
  ],
}

/** Locale key of the caption of the value field of each list, named for what the list holds. */
export const BLOCK_LIST_VALUE_LABELS: Readonly<Record<BlockListType, string>> = {
  [BlockListType.Ip]: 'fraud.blockList.valueLabel.Ip',
  [BlockListType.Cidr]: 'fraud.blockList.valueLabel.Cidr',
  [BlockListType.EmailDomain]: 'fraud.blockList.valueLabel.EmailDomain',
  [BlockListType.Country]: 'fraud.blockList.valueLabel.Country',
}

/**
 * The card that adds an entry to a list: the value to block, named for what the list holds, and an optional note.
 *
 * @param type - The list the entry goes to.
 * @returns The form block, one card of two fields.
 */
export const blockListAddPart = (type: BlockListType): FormSkeletonPart => ({
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'add',
        fields: [
          { key: 'value', label: BLOCK_LIST_VALUE_LABELS[type], required: true },
          { key: 'note', label: 'fraud.blockList.note' },
        ],
      },
    ],
  ],
})

/** The search box over the table. */
export const BLOCK_LIST_TOOLBAR_PART: ToolbarSkeletonPart = {
  kind: SkeletonPartKind.Toolbar,
  toolbar: { search: true },
}

/** The table of the entries: the blocked value, its note, when it was added and the row action. */
export const BLOCK_LIST_TABLE: SkeletonTable = {
  pagination: true,
  columns: [
    { key: 'value', label: 'shared.value', size: UiColumnSize.Grow },
    { key: 'note', label: 'fraud.blockList.note' },
    { key: 'addedAt', label: 'fraud.blockList.addedAt', kind: UiColumnKind.Date },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** What the fraud block lists screen is made of. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.FraudBlockLists), subtitle: 'fraud.blockList.subtitle' },
  parts: [
    BLOCK_LIST_TABS_PART,
    blockListAddPart(BlockListType.Ip),
    BLOCK_LIST_TOOLBAR_PART,
    { kind: SkeletonPartKind.Table, table: BLOCK_LIST_TABLE },
  ],
}

export default schema
