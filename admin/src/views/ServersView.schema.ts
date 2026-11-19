/**
 * Skeleton schema of the servers screen: a header with its two buttons over the search box, the grid of server cards and the
 * card of the server groups with their table.
 *
 * The screen is a composite. `ServersView` draws the placeholder cards of its grid while the list loads (as many as
 * {@link SERVER_CARDS_PART} counts) and the card of the groups with the head of their table from this data, and
 * `UiSkeletonOf` draws the same blocks while the view chunk is downloading. The number of real server cards is data.
 */
import { navTitleKey } from '@/lib/navigation'
import { SERVER_FACT_SLOTS } from '@/lib/servers'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, SkeletonTable, TilesSkeletonPart, ToolbarSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** Key of the card of the server groups, the suffix of its `card-<key>` slot. */
export const SERVER_GROUPS_CARD = 'groups'

/** The search box over the grid of servers. */
export const SERVERS_TOOLBAR_PART: ToolbarSkeletonPart = {
  kind: SkeletonPartKind.Toolbar,
  toolbar: { search: true },
}

/** The grid of server cards: three placeholders while the list loads, each with the facts grid of a real card. */
export const SERVER_CARDS_PART: TilesSkeletonPart = {
  kind: SkeletonPartKind.Tiles,
  count: 3,
  facts: SERVER_FACT_SLOTS,
}

/** The groups table: the name, how a group is filled, the servers in it and the row actions. */
export const SERVER_GROUPS_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'servers.servers.groupName', size: UiColumnSize.Grow },
    { key: 'fillType', label: 'servers.servers.fillType' },
    { key: 'servers', label: 'servers.servers.servers' },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The card of the server groups, headed "Groups". */
export const SERVER_GROUPS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: SERVER_GROUPS_CARD, title: 'servers.servers.groups', table: SERVER_GROUPS_TABLE }]],
}

/** What the servers screen is made of; the help link of the header shows only when a guide is configured and is not counted. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.Servers), subtitle: 'servers.servers.manageProvisioningServersAndLoad', actions: 2, primaryAction: 1 },
  parts: [SERVERS_TOOLBAR_PART, SERVER_CARDS_PART, SERVER_GROUPS_PART],
}

export default schema
