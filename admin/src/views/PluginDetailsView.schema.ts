/**
 * Skeleton schema of the plugin details screen: the plugin head, the tab strip, the overview and the links, one under the other.
 *
 * The head (picture, name, author, state, description, buttons) is made of the plugin's own data, so it has no caption. The
 * tab strip varies with the plugin (Settings, Versions, Licence and Changes appear only when they apply), so only the
 * overview tab that is always there is drawn. The links card is also the placeholder of the links while the registry's
 * details load, which is why it is exported on its own.
 */
import { SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The links card: its heading and a row of links, which come from the registry. */
export const PLUGIN_LINKS_CARD = { key: 'links', title: 'pluginDetails.links.heading', lines: 1 } as const

/** The details card of the installed overview: who made the plugin and what it is; its rows are the captions of its facts. */
export const PLUGIN_OVERVIEW_DETAILS_CARD = {
  key: 'details',
  title: 'pluginDetails.overview.details',
  span: SkeletonSpan.Half,
  rows: ['pluginDetails.overview.author', 'pluginDetails.overview.type', 'pluginDetails.overview.category', 'pluginDetails.overview.id'],
} as const

/** The installation card of the installed overview: where the package came from, who signed it and what it is. */
export const PLUGIN_OVERVIEW_INSTALLATION_CARD = {
  key: 'installation',
  title: 'pluginDetails.overview.installation',
  span: SkeletonSpan.Half,
  rows: [
    'pluginDetails.overview.state.label',
    'pluginDetails.overview.sourceLabel',
    'pluginDetails.overview.installedAt',
    'pluginDetails.overview.updatedAt',
    'pluginDetails.overview.installedBy',
    'pluginDetails.overview.signerLabel',
    'pluginDetails.overview.signerKey',
    'pluginDetails.overview.size',
    'pluginDetails.overview.sha256',
  ],
} as const

/** The compatibility card of the installed overview: the installed and staged version and the server versions it runs on. */
export const PLUGIN_OVERVIEW_COMPATIBILITY_CARD = {
  key: 'compatibility',
  title: 'pluginDetails.overview.compatibility',
  span: SkeletonSpan.Half,
  rows: ['pluginDetails.overview.installedVersion', 'pluginDetails.overview.stagedVersion', 'pluginDetails.overview.hostVersions', 'pluginDetails.overview.sdkVersion'],
} as const

/** The cards of the installed overview: the first four share two columns and the release notes take the whole row under them. */
export const PLUGIN_OVERVIEW_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [
      PLUGIN_OVERVIEW_DETAILS_CARD,
      PLUGIN_OVERVIEW_INSTALLATION_CARD,
      PLUGIN_OVERVIEW_COMPATIBILITY_CARD,
      { key: 'permissions', title: 'pluginDetails.overview.permissions', span: SkeletonSpan.Half, lines: 2 },
      { key: 'notes', title: 'pluginDetails.versions.changelog', lines: 3 },
    ],
  ],
}

/** What the plugin details screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { title: 'pluginDetails.title', backLabel: 'shared.plugins', backMobileOnly: true },
  columns: [
    [
      { key: 'head', identity: true, lines: 2, actions: 2 },
      { key: 'tabs', tabs: [{ key: 'overview', label: 'pluginDetails.tabs.overview' }] },
      { key: 'overview', title: 'pluginDetails.tabs.overview' },
      PLUGIN_LINKS_CARD,
    ],
  ],
}

export default schema
