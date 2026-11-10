/**
 * Fixtures for the plugin pages flow: a sample app plugin that declares two admin pages (an overview
 * at its root and one page per site) and one menu item, the view documents of those pages as the API
 * answers them, and the data its admin routes return.
 */
import type { PluginPageViewDto, PluginUiDto } from '../../src/types/pluginui'
import { PluginBlockKind, PluginBlockTone, PluginFieldType, PluginValueFormat } from '../../src/types/pluginview'

/** Id of the sample plugin. */
export const SAMPLE_PLUGIN = 'sample-app'

/** The admin index the sample plugin adds: one menu item to its root page, and a page per site. */
export const sampleIndex: PluginUiDto = {
  menus: [{ pluginId: SAMPLE_PLUGIN, id: 'main', label: 'Sample sites', page: 'overview', icon: 'globe', order: 1 }],
  pages: [
    { pluginId: SAMPLE_PLUGIN, id: 'overview', path: '/', title: 'Sample sites' },
    { pluginId: SAMPLE_PLUGIN, id: 'site', path: '/sites/{siteId}', title: 'Site' },
  ],
}

/** The overview page: a header with an action, a stat, a table whose rows open a site, and a form. */
export const overviewPage: PluginPageViewDto = {
  pluginId: SAMPLE_PLUGIN,
  id: 'overview',
  path: '/',
  title: 'Sample sites',
  externalOrigins: ['https://docs.example.com'],
  view: {
    schema: 1,
    blocks: [
      {
        kind: PluginBlockKind.Header,
        title: 'Sample sites',
        actions: [{ kind: PluginBlockKind.Action, label: 'Sync now', route: { method: 'post', path: '/sync' }, refresh: ['sites'], style: PluginBlockTone.Primary }],
      },
      { kind: PluginBlockKind.Stat, id: 'count', label: 'Sites', source: { route: { method: 'get', path: '/sites' }, field: 'total' } },
      {
        kind: PluginBlockKind.Table,
        id: 'sites',
        title: 'All sites',
        source: { route: { method: 'get', path: '/sites' }, field: 'items' },
        columns: [{ field: 'name', label: 'Name' }, { field: 'state', label: 'State', format: PluginValueFormat.Badge }],
        rowLink: { page: 'site', params: { siteId: 'id' } },
      },
      {
        kind: PluginBlockKind.Form,
        title: 'New site',
        fields: [{ key: 'name', label: 'Name', type: PluginFieldType.Text, required: true, placeholder: 'Site name' }],
        submit: { route: { method: 'post', path: '/sites' }, label: 'Create', refresh: ['sites', 'count'], resetOnSuccess: true },
      },
      { kind: PluginBlockKind.Link, label: 'Read the guide', href: 'https://docs.example.com/guide' },
    ],
  },
}

/** The page of one site: its name and a chat with the plugin. */
export const sitePage: PluginPageViewDto = {
  pluginId: SAMPLE_PLUGIN,
  id: 'site',
  path: '/sites/{siteId}',
  title: 'Site',
  externalOrigins: [],
  view: {
    schema: 1,
    blocks: [
      { kind: PluginBlockKind.Header, title: 'Site', subtitleSource: { route: { method: 'get', path: '/sites/{siteId}' }, field: 'name' } },
      { kind: PluginBlockKind.Chat, send: { method: 'post', path: '/sites/{siteId}/chat' }, placeholder: 'Ask about this site', assistant: { label: 'Helper' } },
    ],
  },
}

/** One site row as the plugin's route answers it. */
export interface SampleSite {
  /** Its id. */
  id: number
  /** Its name. */
  name: string
  /** Its state word. */
  state: string
}

/**
 * The sites the plugin starts with.
 *
 * @returns A fresh list the spec may grow to play the plugin.
 */
export const initialSites = (): SampleSite[] => [
  { id: 1, name: 'Shop', state: 'live' },
  { id: 2, name: 'Blog', state: 'draft' },
]
