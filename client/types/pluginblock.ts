/**
 * The blocks of a plugin view document (schema 1), as the API sends them: every label already in the reader's
 * language. Values bound from routes are data and are always rendered as text.
 *
 * A block names a route by its method and declared path template (`PluginViewRoute`, the SDK's `route`, `send` and
 * `history` members); the host fills the route's `{param}` segments from the page's parameters.
 *
 * @module types/pluginblock
 */

import type { PluginBlockGroupKind, PluginBlockKind } from '~/types/pluginblockkind'
import type { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginFieldType } from '~/types/pluginfieldtype'
import type { PluginFollowUp } from '~/types/pluginfollowup'
import type { PluginPreviewSize } from '~/types/pluginpreviewsize'
import type { PluginTableDisplay } from '~/types/plugintabledisplay'
import type { PluginValueFormat } from '~/types/pluginvalueformat'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
import type { PluginPreviewSelection } from '~/types/pluginpreviewselection'
import type { PluginViewRoute } from '~/types/pluginviewroute'

/** A value read from a route: the route's reply, or one field of it. */
export interface PluginSource {
  /** The GET route read. */
  route: PluginViewRoute
  /** A dotted path into the reply, e.g. `site.previewUrl`; the whole reply when absent. */
  field?: string
}

/** The body of an action's call, built from a route's reply; `fields` maps each body member to a dotted path into it. */
export interface PluginActionBodySource {
  /** The GET route read. */
  route: PluginViewRoute
  /** The body members and where each is read from. */
  fields: Record<string, string>
}

/** One value of a body built from page data. */
export type PluginActionBodyValue = string | number | boolean

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
  /** The block shows only while this condition holds; always shown when absent. */
  visibleWhen?: PluginViewCondition
}

/** A title with optional actions beside it. */
export interface PluginHeaderBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Header
  /** The title; a compact header may read it from `titleSource` or go without. */
  title?: string
  /** Where the title is read from; replaces `title` once read. */
  titleSource?: PluginSource
  /** Where the smaller line under the title is read from. */
  subtitleSource?: PluginSource
  /** One slim line holding the back link, the title and the actions, for a workspace page's top. */
  compact?: boolean
  /** What is shown beside the title: buttons, and any other block a compact header lines up. */
  actions?: PluginBlock[]
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
  /** `cards` draws each row as a card instead of a table row. */
  display?: PluginTableDisplay
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
    /** Whether every field returns to empty after a successful submit. */
    resetOnSuccess?: boolean
    /** The blocks read again after a successful submit until a route read reaches a final value. */
    followUp?: PluginFollowUp
  }
}

/** A button that calls a route. */
export interface PluginActionBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Action
  /** The button label. */
  label: string
  /** Where a label that depends on data is read from; `label` stands until it is read or when it is empty. */
  labelSource?: PluginSource
  /** The route called. */
  route: PluginViewRoute
  /** Where the call's body is read from, in one read with `labelSource`; the button is disabled until every member is read. */
  bodySource?: PluginActionBodySource
  /** The call's body as written by a result card, which shows one version for as long as it stands. */
  body?: Record<string, PluginActionBodyValue>
  /** The question asked before the call; no question when absent. */
  confirm?: string
  /** The button's tone. */
  style?: PluginBlockTone
  /** The ids of the blocks read again after a successful call. */
  refresh?: string[]
  /** The page opened after a successful call, its parameters taken from the reply. */
  navigate?: PluginPageLink
  /** The blocks read again after a successful call until a route read reaches a final value. */
  followUp?: PluginFollowUp
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
  /** Small actions on the tab row's right, shown whichever tab is open. */
  actions?: PluginActionBlock[]
}

/** A ready-made message a chat offers before the first one is written; pressing it sends the message. */
export interface PluginChatSuggestion {
  /** The button text. */
  label: string
  /** The message a press sends; the label when absent. */
  text?: string
}

/** A chat streamed from a route. */
export interface PluginChatBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Chat
  /** The streaming POST route a message is sent to. */
  send: PluginViewRoute
  /** Whether a message carries the elements selected on the document's preview; off when absent. */
  selection?: boolean
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

/** One pane of a split block. */
export interface PluginSplitPane {
  /** The blocks the pane shows, in order. */
  blocks: PluginBlock[]
}

/** Two panes side by side, the first on top on a phone. */
export interface PluginSplitBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Split
  /** The two panes; the install check refuses any other number. */
  panes: PluginSplitPane[]
}

/** One named address a preview block can show. */
export interface PluginPreviewSource extends PluginSource {
  /** The source id, unique within the block. */
  id: string
  /** The switch's label and the frame's title. */
  label: string
}

/** One address in a sandboxed frame, with a source and a size switch. */
export interface PluginPreviewBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Preview
  /** Lets the reader select elements in the frame; off when absent. */
  selection?: PluginPreviewSelection
  /** The heading above the frame. */
  title?: string
  /** The size shown first; desktop when absent. */
  size?: PluginPreviewSize
  /** The addresses, 1 to 5; the first is shown first. */
  sources: PluginPreviewSource[]
  /** Where a note over the frame is read from: an object of sentences by source id. */
  note?: PluginSource
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
  | PluginSplitBlock
  | PluginPreviewBlock

/** A block of a group with the key it is rendered under. */
export interface PluginKeyedBlock {
  /** The block's id, else its kind, and always its place, which a view document never reorders. */
  key: string
  /** The block. */
  block: PluginBlock
}

/** A run of blocks laid out together. */
export interface PluginBlockGroup {
  /** The key of the group: the key of its first block. */
  key: string
  /** How the group is laid out. */
  kind: PluginBlockGroupKind
  /** The blocks of the group, in document order. */
  blocks: PluginKeyedBlock[]
}
