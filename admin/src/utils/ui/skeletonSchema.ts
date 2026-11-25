/**
 * The skeleton schema: pure data that says what a screen is made of.
 *
 * One schema per screen drives two renders. The real screen draws its sections, fields, columns, tiles and tabs
 * from it, and `UiSkeletonOf` draws the same blocks with the loading flag on, so a skeleton can never differ from the
 * screen it stands in for. The file holds types, closed value sets and nothing else: no component, no store, no
 * translation call. Every text is a locale key, resolved by whoever draws it, so a schema module can be loaded
 * before the screen's own chunk has arrived (that is the whole point: the shell draws the skeleton of a route
 * whose view is still downloading).
 */
import type { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import type { UiGridColumns, UiSplitRatio } from '@/utils/ui/layout'
import type { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import type { UiCardRole, UiColumnKind, UiColumnSize, UiColumnWidth, UiHideBelow } from '@/utils/ui/table'

/** Closed set of the screen shapes a skeleton can have; the discriminant of {@link SkeletonSchema}. */
export const SkeletonKind = {
  /** A header and sections of fields. */
  Form: 'form',
  /** A header, a toolbar and a table with its pager. */
  List: 'list',
  /** A header and cards of label and value rows. */
  Detail: 'detail',
  /** A header card, a tab strip and the skeleton of the routed child below it (a layout). */
  Tabs: 'tabs',
  /** A report: header, filters, stat tiles, a chart and a table. */
  Report: 'report',
  /** A header and a list of parts in reading order (tiles, a form, cards, a toolbar, tables, a tab strip). */
  Composite: 'composite',
  /** The dashboard: stat tiles and panels. */
  Dashboard: 'dashboard',
} as const

/** One of the skeleton kinds. */
export type SkeletonKind = (typeof SkeletonKind)[keyof typeof SkeletonKind]

/** Closed set of the control a field holds; decides the height of its placeholder. */
export const SkeletonFieldKind = {
  /** A single line input (text, email, password, number, date, select, phone). */
  Input: 'input',
  /** A multi line text area. */
  Textarea: 'textarea',
  /** A switch with its caption on one line. */
  Toggle: 'toggle',
  /** A checkbox with its caption on one line. */
  Check: 'check',
  /** A group of radio options, one line each, under the field's caption. */
  Radio: 'radio',
} as const

/** One of the field kinds. */
export type SkeletonFieldKind = (typeof SkeletonFieldKind)[keyof typeof SkeletonFieldKind]

/** Closed set of how many columns of its section a field spans. */
export const SkeletonSpan = {
  /** One column of the section grid. */
  Half: 'half',
  /** The whole row of the section grid. */
  Full: 'full',
} as const

/** One of the spans. */
export type SkeletonSpan = (typeof SkeletonSpan)[keyof typeof SkeletonSpan]

/** One field of a form section. */
export interface SkeletonField {
  /** Identifies the field: the suffix of the `field-<key>` slot of `UiSchemaForm` that holds its real control. */
  key: string
  /** Locale key of the caption; absent for a field with no caption of its own. */
  label?: string
  /** The control it holds; an input when omitted. */
  kind?: SkeletonFieldKind
  /** How much of the section row it takes; half when omitted. */
  span?: SkeletonSpan
  /** Marks the caption with an asterisk. */
  required?: boolean
  /** Locale keys of the captions of the options of a radio group, in order; the real options are bound in the field's slot. */
  options?: readonly string[]
}

/** One card of fields in a form, with an optional title. */
export interface SkeletonSection {
  /** Identifies the section: the suffix of its `section-<key>` slot (extra content under its fields). */
  key: string
  /** Locale key of the card title. */
  title?: string
  /** Columns of the field grid; two when omitted. */
  columns?: 1 | 2
  /** Fields in display order. */
  fields: readonly SkeletonField[]
}

/** The header of a screen. */
export interface SkeletonHeader {
  /** Locale key of the title. Absent when the title is made of data (a client name, an invoice number): a bar stands in. */
  title?: string
  /** Locale key of the subtitle; absent when the screen has none. */
  subtitle?: string
  /** Whether the real screen draws a breadcrumb trail above its title, so a bar stands where the trail will be. */
  crumbs?: boolean
  /** Whether the subtitle is made of data, so a bar stands in for it while the title is drawn. */
  subtitleFromData?: boolean
  /** How many buttons sit in the actions area; each draws as a button shaped bar. A report counts only the controls of its own here: its standard buttons are the schema's `actions`. */
  actions?: number
  /** Index, among `actions`, of the button that stays on a phone: the one the real header keeps (`primaryIndex`, the first in the primary look); the first when omitted. */
  primaryAction?: number
}

/** What every column of a table has, whatever it holds. */
interface SkeletonColumnBase {
  /** Identifies the column. */
  key: string
  /** Horizontal alignment of the column's cells; left when omitted. */
  align?: 'left' | 'center' | 'right'
  /** Width of the column as its header sets it; the table default when omitted. */
  size?: UiColumnSize
  /** Kind of data the column holds, which sets its width when `size` is omitted; free text when omitted. */
  kind?: UiColumnKind
  /** Width of a `Fixed` column. */
  width?: UiColumnWidth
  /** Breakpoint below which the column is hidden, in the real header and in the skeleton alike. */
  hideBelow?: UiHideBelow
  /** What the column is on a card (below `sm`); the table default when omitted. */
  cardRole?: UiCardRole
  /** Heads a number or money column. */
  numeric?: boolean
}

/** A column of data: its header caption is the screen's own text. */
export interface SkeletonDataColumn extends SkeletonColumnBase {
  /** Locale key of the header caption. */
  label?: string
  /** Not the row actions column. */
  actions?: false
}

/** The row actions column. It names no caption: the header is always the shared "Actions", so a screen cannot leave it nameless or name it differently. */
export interface SkeletonActionsColumn extends SkeletonColumnBase {
  /** Marks the row actions column. */
  actions: true
  /** An actions column has no caption of its own. */
  label?: never
}

/** One column of a table: a data column or the row actions column. */
export type SkeletonColumn = SkeletonDataColumn | SkeletonActionsColumn

/** The toolbar over a list. */
export interface SkeletonToolbar {
  /** Whether a search box is drawn. */
  search?: boolean
  /** Locale keys of the filter selects, in order. */
  filters?: readonly string[]
  /** How many buttons sit at the right end. */
  actions?: number
}

/** One table of a screen. */
export interface SkeletonTable {
  /** Columns in display order. */
  columns: readonly SkeletonColumn[]
  /** Rows the skeleton draws; the kit default when omitted. */
  rows?: number
  /** Whether a pager sits under the table. */
  pagination?: boolean
  /** Whether the rows come in groups, each opened by a heading row (product groups, expiry groups), which the skeleton draws too. */
  grouped?: boolean
}

/** One stat tile. */
export interface SkeletonStat {
  /** Identifies the tile. */
  key: string
  /** Locale key of the caption, which is page text and stays while the number loads. */
  label: string
}

/** One card of a detail screen: a title and rows of caption over value, fields, lines, a table or a tab strip. */
export interface SkeletonDetailCard {
  /** Identifies the card: the suffix of its `card-<key>` slot of `UiDetailCards` that holds the real body. */
  key: string
  /** Locale key of the card title. */
  title?: string
  /** How much of the row of a one column screen it takes; the whole row when omitted. Only a card beside another one is half. */
  span?: SkeletonSpan
  /** Draws a head made of data (a logo and two lines of text, the name and its description) under the title. */
  identity?: boolean
  /** Locale keys of the captions of the rows, in order. */
  rows?: readonly string[]
  /** How many field placeholders (a caption and a box each, two across) follow the rows; for fields only the API knows. */
  fields?: number
  /** How many plain lines of text follow the fields; for text made of data. */
  lines?: number
  /** How many small boxes of text (a card each, three across) follow the lines; for a list of records. */
  tiles?: number
  /** A table the card holds instead of rows. */
  table?: SkeletonTable
  /** A chart the card holds; its skeleton is the placeholder of the chart's kind. */
  chart?: SkeletonChart
  /** How many buttons sit in the row at the foot of the card; each draws as a button shaped bar. */
  actions?: number
  /** Makes the part a bare tab strip with no card around it, its real tabs in the slot `card-<key>`. */
  tabs?: readonly SkeletonTab[]
}

/**
 * One tab of a tab strip.
 *
 * @typeParam Key - The closed set the tab keys come from (such as the integration categories); any string by default.
 */
export interface SkeletonTab<Key extends string = string> {
  /** Identifies the tab. */
  key: Key
  /** Locale key of the caption. */
  label: string
}

/** Closed set of how a tab strip is laid out. */
export const SkeletonTabsOrientation = {
  /** A strip of tabs under the header, the routed child below it. */
  Horizontal: 'horizontal',
  /** A rail of entries beside the routed child (an inner sidebar), one entry per tab. */
  Vertical: 'vertical',
} as const

/** One of the tab orientations. */
export type SkeletonTabsOrientation = (typeof SkeletonTabsOrientation)[keyof typeof SkeletonTabsOrientation]

/** The chart of a report or a dashboard panel: the one definition both the screen and its skeleton are drawn from. */
export interface SkeletonChart {
  /** Shape of the chart; the skeleton draws the placeholder of the same shape. */
  kind: ChartKind
  /** Height step of the chart box, as `UiChart` names them; the medium one when omitted. */
  height?: UiChartHeight
}

/** Fields every screen schema has. */
interface SkeletonSchemaBase {
  /** The header; absent for a layout whose header is its own card. */
  header?: SkeletonHeader
  /** Columns of the stat tiles, when the screen has any; four when omitted. */
  statColumns?: UiGridColumns
}

/** A form screen. */
export interface FormSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Form
  /** Columns of sections side by side; each inner list is one column's stack of cards. */
  columns: readonly (readonly SkeletonSection[])[]
}

/** A list screen. */
export interface ListSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.List
  /** The toolbar over the table; absent when there is none. */
  toolbar?: SkeletonToolbar
  /** Stat tiles over the list; absent when there are none. */
  stats?: readonly SkeletonStat[]
  /** The table. */
  table: SkeletonTable
}

/** A detail screen. */
export interface DetailSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Detail
  /**
   * Columns of cards side by side; each inner list is one column's stack. A single column whose cards are half wide
   * lays them out two across, the whole row ones spanning both.
   */
  columns: readonly (readonly SkeletonDetailCard[])[]
  /** How the width is shared between two columns, or between the two tracks of a single column of half cards; half and half when omitted. */
  ratio?: UiSplitRatio
}

/**
 * A layout with a tab strip and a routed child.
 *
 * @typeParam Key - The closed set the tab keys come from; any string by default.
 */
export interface TabsSkeletonSchema<Key extends string = string> extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Tabs
  /** Stat tiles of the header card; absent when there are none. */
  stats?: readonly SkeletonStat[]
  /** How many buttons sit in the header card beside the identity; each draws as a button shaped bar. */
  actions?: number
  /** The tab strip, or the entries of the rail when the orientation is vertical. */
  tabs: readonly SkeletonTab<Key>[]
  /** How the tabs are laid out; horizontal when omitted. */
  orientation?: SkeletonTabsOrientation
  /** Whether a vertical rail gives way to a tab strip over the routed child below `md`, as the reports layout does on a phone. */
  stripBelowMd?: boolean
}

/**
 * A report screen.
 *
 * The parts are listed in the order they are drawn: the header buttons, the filters, the tiles, then the parts whose
 * content only the data decides but whose place is fixed (the map, a notice, the summary lines), the chart, the table,
 * the closing lines and the note. `ReportPage` draws the real thing from this schema and `UiSkeletonOf` draws the
 * placeholder of the same blocks, so the two can never differ.
 */
export interface ReportSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Report
  /** The buttons of the header, in order; absent when the report has none. */
  actions?: readonly ReportAction[]
  /** Filter fields over the result; absent when the report has none. */
  filters?: readonly SkeletonField[]
  /** Stat tiles; absent when the report has none. */
  stats?: readonly SkeletonStat[]
  /** Whether a world map of the result stands above the chart. */
  map?: boolean
  /** Whether a notice about the result (a server that could not be read) stands above it. */
  alert?: boolean
  /** Whether lines of text over the result (totals per currency, an opening balance) stand above the chart and the table. */
  summary?: boolean
  /** The chart; absent when the report has none. */
  chart?: SkeletonChart
  /** The table; absent when the report has none. */
  table?: SkeletonTable
  /** Whether lines of text under the table (a closing balance) follow it. */
  closing?: boolean
  /** The note that ends the report and names its currency; absent when it has none. */
  note?: ReportNote
  /** Locale key of the sentence of the empty state shown when the whole report has no data; a generic sentence when absent. */
  emptyText?: string
}

/** A report screen that has a chart: the schema the report view reads its chart kind and height from. */
export type ChartReportSkeletonSchema = ReportSkeletonSchema & { chart: SkeletonChart }

/** The dashboard. */
export interface DashboardSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Dashboard
  /** The headline tiles. */
  stats: readonly SkeletonStat[]
  /** The rows of panels under the tiles, top to bottom; each is a block of detail cards with its own columns and width ratio. */
  panels: readonly CardsSkeletonPart[]
}

/** Closed set of the blocks a composite screen is made of. */
export const SkeletonPartKind = {
  /** A row of stat tiles. */
  Stats: 'stats',
  /** Sections of fields. */
  Form: 'form',
  /** Columns of detail cards. */
  Cards: 'cards',
  /** A toolbar of search, filters and buttons (a bulk action bar is one). */
  Toolbar: 'toolbar',
  /** A table with its pager. */
  Table: 'table',
  /** A bare tab strip. */
  Tabs: 'tabs',
  /** A chart box. */
  Chart: 'chart',
  /** A grid of cards, each holding a few boxes of text (the server cards). */
  Tiles: 'tiles',
  /** A world map. */
  Map: 'map',
  /** Lines of text whose number follows the data. */
  Lines: 'lines',
  /** A notice bar. */
  Alert: 'alert',
  /** A line of small print. */
  Note: 'note',
} as const

/** One of the part kinds. */
export type SkeletonPartKind = (typeof SkeletonPartKind)[keyof typeof SkeletonPartKind]

/** A row of stat tiles in a composite screen. */
export interface StatsSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Stats
  /** The tiles. */
  stats: readonly SkeletonStat[]
  /** Columns of the row; four when omitted. */
  columns?: UiGridColumns
}

/** Sections of fields in a composite screen. */
export interface FormSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Form
  /** Columns of sections side by side; each inner list is one column's stack of cards. */
  columns: readonly (readonly SkeletonSection[])[]
}

/** Detail cards in a composite screen. */
export interface CardsSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Cards
  /** Columns of cards, as a detail screen lists them. */
  columns: readonly (readonly SkeletonDetailCard[])[]
  /** How the width is shared, as a detail screen says it. */
  ratio?: UiSplitRatio
}

/** A toolbar in a composite screen. */
export interface ToolbarSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Toolbar
  /** The toolbar. */
  toolbar: SkeletonToolbar
}

/** A table in a composite screen. */
export interface TableSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Table
  /** The table. */
  table: SkeletonTable
}

/** A bare tab strip in a composite screen. */
export interface TabsSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Tabs
  /** The tabs. */
  tabs: readonly SkeletonTab[]
}

/** A chart in a composite screen. */
export interface ChartSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Chart
  /** The chart. */
  chart: SkeletonChart
}

/** A grid of cards in a composite screen: as many as fit the row, each holding a few boxes of text. */
export interface TilesSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Tiles
  /** How many cards the skeleton draws; the real screen's count is data. */
  count: number
  /** How many icon, caption and value facts each card draws under its load bar; the real card's count follows its data. */
  facts: number
}

/** A world map in a report. */
export interface MapSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Map
}

/** Lines of text in a report, over or under its table. */
export interface LinesSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Lines
}

/** A notice bar in a report. */
export interface AlertSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Alert
}

/** The line of small print that ends a report. */
export interface NoteSkeletonPart {
  /** Discriminant. */
  kind: typeof SkeletonPartKind.Note
}

/** The parts that stand for a picture or text the data fills in, each drawn as one placeholder shape. */
export type ShapeSkeletonPart = MapSkeletonPart | LinesSkeletonPart | AlertSkeletonPart | NoteSkeletonPart

/** One block of a composite screen, one shape per {@link SkeletonPartKind}. */
export type SkeletonPart =
  | StatsSkeletonPart
  | FormSkeletonPart
  | CardsSkeletonPart
  | ToolbarSkeletonPart
  | TableSkeletonPart
  | TabsSkeletonPart
  | ChartSkeletonPart
  | TilesSkeletonPart
  | ShapeSkeletonPart

/** A screen that composes blocks in reading order: tiles over a form over a table, two tables and a bulk bar, and the like. */
export interface CompositeSkeletonSchema extends SkeletonSchemaBase {
  /** Discriminant. */
  kind: typeof SkeletonKind.Composite
  /** The blocks, top to bottom. */
  parts: readonly SkeletonPart[]
}

/** What a screen is made of, one shape per {@link SkeletonKind}. */
export type SkeletonSchema =
  | FormSkeletonSchema
  | ListSkeletonSchema
  | DetailSkeletonSchema
  | TabsSkeletonSchema
  | ReportSkeletonSchema
  | DashboardSkeletonSchema
  | CompositeSkeletonSchema
