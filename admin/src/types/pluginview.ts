/**
 * A plugin view document (schema 1) as the API sends it for an admin page: every label already in the
 * operator's language, every value bound from a route plain data that is drawn as text.
 *
 * The closed value sets of the plugin view area live here too, one file for the area: the block kinds,
 * tones, field types, value formats, table displays and the chat's roles, events and step states. The
 * members are the strings the view document and the event stream carry.
 *
 * A block names a route by its method and declared path template (`PluginViewRoute`, the SDK's `route`,
 * `send` and `history` members); the panel fills the route's `{param}` segments from the page's parameters.
 */

import type { HttpMethod } from '@/types/http'

/** Kind of one block of a view (`PluginViews.BlockKinds` in the SDK). */
export enum PluginBlockKind {
  /** A title with optional actions. */
  Header = 'header',
  /** Plain text, never HTML. */
  Text = 'text',
  /** A labelled value. */
  Stat = 'stat',
  /** Rows from a route, with columns and paging. */
  Table = 'table',
  /** Fields sent to a route. */
  Form = 'form',
  /** A button that calls a route. */
  Action = 'action',
  /** A link inside the panel or to one of the plugin's allowed origins. */
  Link = 'link',
  /** A short status word. */
  Badge = 'badge',
  /** An empty state. */
  Empty = 'empty',
  /** A notice. */
  Alert = 'alert',
  /** Tabs, each holding blocks. */
  Tabs = 'tabs',
  /** A chat streamed from a route. */
  Chat = 'chat',
  /** A progress bar. */
  Progress = 'progress',
  /** Two panes side by side, stacked on a phone. */
  Split = 'split',
  /** A framed site; allowed on a client page only, so the admin panel never draws it. */
  Preview = 'preview',
}

/** Tone of a block, a button or a chat card. */
export enum PluginBlockTone {
  /** Neutral. */
  Neutral = 'neutral',
  /** Information. */
  Info = 'info',
  /** Success. */
  Success = 'success',
  /** A warning. */
  Warning = 'warning',
  /** A failure or a destructive action. */
  Danger = 'danger',
  /** The primary action. */
  Primary = 'primary',
}

/** Type of one field of a form block. */
export enum PluginFieldType {
  /** One line of text. */
  Text = 'text',
  /** Several lines of text. */
  Textarea = 'textarea',
  /** A number. */
  Number = 'number',
  /** An http(s) address. */
  Url = 'url',
  /** A value that is not shown back. */
  Secret = 'secret',
  /** A choice from the field's options. */
  Select = 'select',
  /** On or off. */
  Boolean = 'boolean',
}

/** How a value bound from a route is formatted. */
export enum PluginValueFormat {
  /** The value as text. */
  Text = 'text',
  /** An ISO date and time. */
  DateTime = 'datetime',
  /** `{ amount, currency }`, in the operator's locale. */
  Money = 'money',
  /** A short status word drawn as a badge. */
  Badge = 'badge',
}

/** How a table block draws its rows. */
export enum PluginTableDisplay {
  /** A table with one row per entry. */
  Table = 'table',
  /** One card per entry, for a narrow pane. */
  Cards = 'cards',
}

/** Who wrote a chat message. */
export enum PluginChatRole {
  /** The operator. */
  User = 'user',
  /** The plugin. */
  Assistant = 'assistant',
}

/** Name of one event of a chat's stream. */
export enum PluginChatEventType {
  /** More text of the reply being written: `{ text }`. */
  Delta = 'delta',
  /** A whole reply: `{ text }`. */
  Message = 'message',
  /** What the plugin is doing now: `{ text }`. */
  Status = 'status',
  /** The plugin failed: `{ text }`, or the host's `{ message }`. */
  Error = 'error',
  /** A link offered with the reply: `{ text, url }`, shown only when the link rule admits `url`. */
  Link = 'link',
  /** The reply is complete. */
  Done = 'done',
  /** One row of the reply's step list: `{ id?, label, state }`. */
  Step = 'step',
  /** A card in the reply: `{ tone?, title?, text?, details?, actions? }`. */
  Result = 'result',
  /** The plugin's own failure, the stream going on: `{ text }`. */
  Issue = 'issue',
}

/** State of one step of a chat reply. */
export enum PluginChatStepState {
  /** The plugin is doing it now. */
  Running = 'running',
  /** It finished. */
  Done = 'done',
  /** It failed. */
  Failed = 'failed',
  /** The operator stopped the reply while it ran; set by the panel, never read from a stream. */
  Stopped = 'stopped',
}

/** A view's reference to one of the plugin's declared routes. */
export interface PluginViewRoute {
  /** The method as the manifest declares it, any case. */
  method: string
  /** The path template, e.g. `/sites/{siteId}`. */
  path: string
}

/** One scalar a view condition compares against. */
export type PluginConditionValue = string | number | boolean | null

/** A condition on a GET route's reply: `field` strictly equals `equals`, or one of its values. */
export interface PluginViewCondition {
  /** The GET route read. */
  route: PluginViewRoute
  /** The dotted path of the field compared. */
  field: string
  /** The value, or the list of values, the field must equal. */
  equals: PluginConditionValue | readonly PluginConditionValue[]
}

/** The blocks read again after a successful call until a route read reaches a final value. */
export interface PluginFollowUp {
  /** The ids of the blocks read again on every attempt. */
  refresh: readonly string[]
  /** The condition that ends the follow-up once it holds. */
  until: PluginViewCondition
}

/** A value read from a route: the reply, or one field of it. */
export interface PluginSource {
  /** The GET route read. */
  route: PluginViewRoute
  /** A dotted path into the reply; the whole reply when absent. */
  field?: string
}

/** One member value of an action's body. */
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
  /** The title. */
  title?: string
  /** Where the title is read from; replaces `title` once read. */
  titleSource?: PluginSource
  /** Where the smaller line under the title is read from. */
  subtitleSource?: PluginSource
  /** One slim line holding the title and the actions. */
  compact?: boolean
  /** What is shown beside the title: buttons, or any other block. */
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
  /** Where the rows are read from. */
  source: PluginSource
  /** The columns, in order. */
  columns: readonly PluginTableColumn[]
  /** The page a row opens. */
  rowLink?: PluginPageLink
  /** Rows per page; every row on one page when absent. */
  pageSize?: number
  /** `cards` draws each row as a card. */
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

/** What submitting a form does. */
export interface PluginFormSubmit {
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

/** Fields sent as one JSON object to a route. */
export interface PluginFormBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Form
  /** The heading above the form. */
  title?: string
  /** The fields, in order. */
  fields: readonly PluginFormField[]
  /** What submitting does. */
  submit: PluginFormSubmit
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
  /** The call's body as a chat result card writes it. */
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

/** A link inside the panel or to one of the plugin's allowed origins. */
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
  /** Small actions on the tab row, shown whichever tab is open. */
  actions?: PluginActionBlock[]
}

/** A ready-made message a chat offers before the first one is written; pressing it sends the message. */
export interface PluginChatSuggestion {
  /** The button text. */
  label: string
  /** The message a press sends; the label when absent. */
  text?: string
}

/** What an empty chat shows before the first message. */
export interface PluginChatIntro {
  /** The heading. */
  title?: string
  /** The sentence below it. */
  text?: string
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
  /** The most characters a message may have. */
  maxLength?: number
  /** How the plugin's replies are signed. */
  assistant?: {
    /** The name shown above each reply. */
    label: string
  }
  /** What an empty chat shows before the first message. */
  intro?: PluginChatIntro
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
  /** The two panes. */
  panes: PluginSplitPane[]
}

/** A framed site; refused on an admin page at install, so it is never drawn here. */
export interface PluginPreviewBlock extends PluginBlockBase {
  /** The kind. */
  kind: PluginBlockKind.Preview
}

/** How a run of blocks is laid out (`groupPluginBlocks`). */
export enum PluginBlockGroupKind {
  /** One block, drawn on its own line. */
  Single = 'single',
  /** A run of consecutive `stat` blocks, drawn as a grid of tiles. */
  Stats = 'stats',
  /** A run of consecutive `link` blocks, drawn as a wrapping row of buttons. */
  Links = 'links',
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

/** A plugin view document. */
export interface PluginView {
  /** The schema version, 1. */
  schema: number
  /** The blocks, in order. */
  blocks: PluginBlock[]
}

/** The outcome of one call to a plugin route, handed back to the block that made it. */
export interface PluginCallResult {
  /** Whether the call succeeded (for a stream: ended or was stopped). */
  ok: boolean
  /** The reply; null on a failure, for a stream and for an empty reply. */
  data: unknown
  /** The refusal's sentence, the API's own; empty when there is none. */
  error: string
  /** The refusal's sentence per field. */
  fieldErrors: Record<string, string>
  /** The call could not be made on this page (a method the panel does not call, an unfilled parameter); nothing was sent. */
  unavailable: boolean
}

/** A route reference turned into a request. */
export interface PluginRouteRequest {
  /** The method. */
  method: HttpMethod
  /** The filled path below the plugin's prefix, with its query, without a leading `/`. */
  url: string
}

/** A link a plugin may render: inside the panel, or out to one of the plugin's allowed origins. */
export interface PluginLinkTarget {
  /** The address, exactly as checked. */
  href: string
  /** Whether it leaves the panel: opened in a new tab with `noopener noreferrer`. */
  external: boolean
}

/** One step of a chat reply. */
export interface PluginChatStep {
  /** The event's `id`, or else its label; a repeat updates the row. */
  key: string
  /** The text. */
  label: string
  /** The state. */
  state: PluginChatStepState
}

/** One action of a chat result card. */
export interface PluginChatResultAction {
  /** Its place in the card. */
  key: number
  /** The label. */
  label: string
  /** The checked link; null for a call. */
  link: PluginLinkTarget | null
  /** The call; null for a link. */
  block: PluginActionBlock | null
  /** Sends the conversation's last message again. */
  resend?: boolean
  /** The action shows only while this condition holds; always shown when absent. */
  visibleWhen?: PluginViewCondition
}

/** A result card of a chat reply. */
export interface PluginChatResult {
  /** A key unique within the chat. */
  key: number
  /** The tone of the card. */
  tone: PluginBlockTone
  /** The title; empty for none. */
  title: string
  /** The sentence; empty for none. */
  text: string
  /** The lines behind the card's details switch; empty for no switch. */
  details: string[]
  /** The actions, in order. */
  actions: PluginChatResultAction[]
}

/** A link a chat reply carries. */
export interface PluginChatLink extends PluginLinkTarget {
  /** A key unique within the chat. */
  key: number
  /** The link text. */
  text: string
}

/** The plugin's own failure during a reply. */
export interface PluginChatIssue {
  /** A key unique within the chat. */
  key: number
  /** The sentence. */
  text: string
}

/** One message of a chat. */
export interface PluginChatMessage {
  /** A key unique within the chat. */
  key: number
  /** Who wrote it. */
  role: PluginChatRole
  /** The text. */
  text: string
  /** The links the reply carries. */
  links: PluginChatLink[]
  /** The reply's steps, in the order they first arrived. */
  steps: PluginChatStep[]
  /** The reply's result cards. */
  results: PluginChatResult[]
  /** The plugin's own failures during the reply. */
  issues: PluginChatIssue[]
  /** What the plugin says it is doing now while the reply is written; empty when nothing. */
  status: string
  /** When this session wrote the message, in milliseconds since the epoch; absent for a message read from the history. */
  sentAt?: number
}

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
