/**
 * The blocks of a plugin view document (schema 1), as the API sends them: every label already in the reader's
 * language. Values bound from routes are data and are always rendered as text.
 *
 * A block names a route by its method and declared path template (`PluginViewRoute`, the SDK's `route`, `send` and
 * `history` members); the host fills the route's `{param}` segments from the page's parameters.
 *
 * @module types/pluginblock
 */

import type { PluginBlockKind } from '~/types/pluginblockkind'
import type { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginFieldType } from '~/types/pluginfieldtype'
import type { PluginValueFormat } from '~/types/pluginvalueformat'
import type { PluginViewRoute } from '~/types/pluginviewroute'

/** A value read from a route: the route's reply, or one field of it. */
export interface PluginSource {
  /** The GET route read. */
  route: PluginViewRoute
  /** A dotted path into the reply, e.g. `site.previewUrl`; the whole reply when absent. */
  field?: string
}

/** A plugin page to open, its parameters taken from fields of the row or reply at hand. */
export interface PluginPageLink {
  /** The page id. */
  page: string
  /** Page parameter name to the field whose value fills it. */
  params?: Record<string, string>
}

/** What every block may carry. */
interface PluginBlockBase {
  /** The block id, which an action's `refresh` names; optional. */
  id?: string
}

/** A title with optional actions beside it. */
export interface PluginHeaderBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Header
  /** The title. */
  title: string
  /** Buttons shown beside the title. */
  actions?: PluginActionBlock[]
}

/** Plain text. */
export interface PluginTextBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Text
  /** The text, shown as written: never HTML. */
  text: string
}

/** A labelled value, written in the view or read from a route. */
export interface PluginStatBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Stat
  /** The label. */
  label: string
  /** The value written in the view; ignored when `source` is given. */
  value?: string | number
  /** Where the value is read from. */
  source?: PluginSource
  /** How the value is formatted. */
  format?: PluginValueFormat
}

/** One column of a table block. */
export interface PluginTableColumn {
  /** The dotted path of the row field shown. */
  field: string
  /** The column heading. */
  label: string
  /** How the field is formatted. */
  format?: PluginValueFormat
}

/** Rows read from a route. */
export interface PluginTableBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Table
  /** The heading above the table. */
  title?: string
  /** Where the rows are read from: an array, the reply itself or one field of it. */
  source: PluginSource
  /** The columns, in order. */
  columns: readonly PluginTableColumn[]
  /** The page a row opens. */
  rowLink?: PluginPageLink
  /** Rows per page; every row on one page when absent. */
  pageSize?: number
}

/** One field of a form block. */
export interface PluginFormField {
  /** The key the value is sent under. */
  key: string
  /** The label. */
  label: string
  /** The field type. */
  type: PluginFieldType
  /** Whether a value is needed. */
  required?: boolean
  /** The values of a select field, sent as written. */
  options?: readonly string[]
  /** The placeholder. */
  placeholder?: string
}

/** Fields sent as one JSON object to a route. */
export interface PluginFormBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Form
  /** The heading above the form. */
  title?: string
  /** The fields, in order. */
  fields: readonly PluginFormField[]
  /** What submitting does. */
  submit: {
    /** The route the values are sent to. */
    route: PluginViewRoute
    /** The button label. */
    label: string
    /** The ids of the blocks read again after a successful submit. */
    refresh?: string[]
  }
}

/** A button that calls a route. */
export interface PluginActionBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Action
  /** The button label. */
  label: string
  /** The route called. */
  route: PluginViewRoute
  /** The question asked before the call; no question when absent. */
  confirm?: string
  /** The button's tone. */
  style?: PluginBlockTone
  /** The ids of the blocks read again after a successful call. */
  refresh?: string[]
  /** The page opened after a successful call, its parameters taken from the reply. */
  navigate?: PluginPageLink
}

/** A link to one of the plugin's allowed origins. */
export interface PluginLinkBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Link
  /** The link text. */
  label: string
  /** The address written in the view; ignored when `source` is given. */
  href?: string
  /** Where the address is read from. */
  source?: PluginSource
}

/** A short status word. */
export interface PluginBadgeBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Badge
  /** The word written in the view; ignored when `source` is given. */
  text?: string
  /** Where the word is read from. */
  source?: PluginSource
  /** The tone. */
  tone?: PluginBlockTone
}

/** An empty state. */
export interface PluginEmptyBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Empty
  /** The title. */
  title: string
  /** The sentence below it. */
  text?: string
}

/** A notice. */
export interface PluginAlertBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Alert
  /** The title. */
  title?: string
  /** The sentence. */
  text: string
  /** The tone. */
  tone?: PluginBlockTone
}

/** One tab of a tabs block. */
export interface PluginTab {
  /** The tab id, unique within the block. */
  id: string
  /** The tab title. */
  title: string
  /** The blocks the tab shows. */
  blocks: PluginBlock[]
}

/** Tabs, each holding blocks. */
export interface PluginTabsBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Tabs
  /** The tabs, in order. */
  tabs: PluginTab[]
}

/** A ready-made message a chat offers before the first one is written. */
export interface PluginChatSuggestion {
  /** The button text. */
  label: string
  /** The message it puts in the message field. */
  text: string
}

/** A chat streamed from a route. */
export interface PluginChatBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Chat
  /** The streaming POST route a message is sent to. */
  send: PluginViewRoute
  /** The GET route answering the earlier messages. */
  history?: PluginViewRoute
  /** The placeholder of the message field. */
  placeholder?: string
  /** The most characters a message may have; the field stops there. */
  maxLength?: number
  /** How the plugin's replies are signed. */
  assistant?: {
    /** The name shown above each reply. */
    label: string
  }
  /** What an empty chat shows before the first message. */
  intro?: {
    /** The heading. */
    title?: string
    /** The sentence below it. */
    text?: string
  }
  /** Ready-made messages offered while the chat is empty. */
  suggestions?: PluginChatSuggestion[]
  /** The ids of the blocks read again once a reply has ended. */
  refresh?: string[]
}

/** A progress bar. */
export interface PluginProgressBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Progress
  /** The label. */
  label: string
  /** The value written in the view, 0 to `max`; ignored when `source` is given. */
  value?: number
  /** The largest value; 100 when absent. */
  max?: number
  /** Where the value is read from. */
  source?: PluginSource
}

/** One block of a plugin view. */
export type PluginBlock =
  | PluginHeaderBlock
  | PluginTextBlock
  | PluginStatBlock
  | PluginTableBlock
  | PluginFormBlock
  | PluginActionBlock
  | PluginLinkBlock
  | PluginBadgeBlock
  | PluginEmptyBlock
  | PluginAlertBlock
  | PluginTabsBlock
  | PluginChatBlock
  | PluginProgressBlock
