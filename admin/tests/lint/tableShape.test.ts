/**
 * Guard: every table in a screen or domain component is drawn by the kit and has the one shape of #1036.
 *
 * Under `src/views` and `src/components` (the kit in `components/ui` excepted) a template must not:
 * - render raw `table`, `thead`, `tbody`, `tfoot`, `tr`, `td` or `th` markup (UiTable and its parts draw those);
 * - put two or more UiIconButton or UiButton controls in one UiTableCell (row actions go through UiRowActions);
 * - caption a column "Actions" without the `actions` prop on its UiTableHeader (the shared compact actions column);
 * - put a text action in any cell, not only an `actions` cell: a button or link (UiButton, UiLink, RouterLink, a) with
 *   a button look (primary, secondary, danger, ghost, field), or whose label is a bare sentence ("Edit", "View",
 *   `t('shared.view')`) instead of a record's own data. One action is an icon button and several are one "…" menu,
 *   both drawn by UiRowActions, so a column never widens with a label. A link whose label is the record's data (a
 *   client name, `#{{ id }}`, a `t` call with parameters) is navigation, not an action, and passes. A control in an
 *   `actions` cell is always an action.
 *
 * A few files are allowed to break one rule, each named below with the reason. The list is checked too: an entry
 * whose file is gone or no longer breaks its rule fails, so the list cannot rot.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The folders the guard reads. */
const ROOTS: readonly string[] = [join(SRC, 'views'), join(SRC, 'components')]

/** The kit folder, which draws the raw table markup on purpose. */
const KIT = join(SRC, 'components', 'ui')

/** The rules the guard enforces. */
enum TableRule {
  /** Raw table markup instead of UiTable and its parts. */
  RawMarkup = 'raw-markup',
  /** Two or more buttons in one cell instead of UiRowActions. */
  CellButtons = 'cell-buttons',
  /** An "Actions" column header without the `actions` prop. */
  ActionsHeader = 'actions-header',
  /** A text button or link in an actions cell instead of UiRowActions. */
  TextAction = 'text-action',
}

/** One file allowed to break one rule, with the reason it may. */
interface AllowedException {
  /** Path under `src`, with forward slashes. */
  file: string
  /** The rule the file may break. */
  rule: TableRule
  /** Why the file is not tabular row actions. */
  reason: string
}

/** The named exceptions: inline form rows and a status feed, none of them row actions. */
const ALLOWED: readonly AllowedException[] = [
  {
    file: 'views/KnowledgebaseView.vue',
    rule: TableRule.CellButtons,
    reason: 'form row: a category renamed in place shows its Save and Cancel buttons in the edited row',
  },
  {
    file: 'views/KnowledgebaseView.vue',
    rule: TableRule.TextAction,
    reason: 'form row: the Save and Cancel buttons of a category renamed in place are the form of the edited row, not row actions',
  },
  {
    file: 'views/PredefinedRepliesView.vue',
    rule: TableRule.TextAction,
    reason: 'form row: the Save and Cancel buttons of a category or reply edited in place are the form of the edited row, not row actions',
  },
  {
    file: 'views/PredefinedRepliesView.vue',
    rule: TableRule.CellButtons,
    reason: 'form row: a category or reply edited in place shows its Save and Cancel buttons in the edited row',
  },
  {
    file: 'components/clients/DnsRecordsTable.vue',
    rule: TableRule.CellButtons,
    reason: 'form row: the draft record row being written carries its Save and Cancel buttons',
  },
  {
    file: 'components/clients/EmailForwardingTable.vue',
    rule: TableRule.CellButtons,
    reason: 'form row: the draft forwarding rule row being written carries its Save and Cancel buttons',
  },
  {
    file: 'views/OrderDetailView.vue',
    rule: TableRule.CellButtons,
    reason: 'feed: the fulfilment cell of an order line shows the fix for its state (retry, close refund) beside the badge',
  },
  {
    file: 'views/OrderDetailView.vue',
    rule: TableRule.TextAction,
    reason: 'feed: the retry and close refund buttons carry the busy state of their line and sit in the fulfilment status cell, not in an actions column',
  },
]

/**
 * Lists every `.vue` file under a folder, the kit left out.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the components found.
 */
const vueFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return path === KIT ? [] : vueFiles(path)
    }
    return name.endsWith('.vue') ? [path] : []
  })

/**
 * The template of a single file component without its HTML comments; empty when there is none.
 *
 * @param source - The `.vue` file contents.
 * @returns The template markup.
 */
const templateOf = (source: string): string => {
  const start = source.indexOf('<template>')
  const end = source.lastIndexOf('</template>')
  if (start < 0 || end < start) {
    return ''
  }
  return source.slice(start + '<template>'.length, end).replace(/<!--[\s\S]*?-->/g, '')
}

/** A raw table element opening tag. */
const RAW_TAG = /<(table|thead|tbody|tfoot|tr|td|th)[\s>/]/g

/** One UiTableCell with its content (cells do not nest). */
const CELL = /<UiTableCell\b[^>]*>([\s\S]*?)<\/UiTableCell>/g

/** One UiTableCell with its attributes (group 1) and its content (group 2). */
const CELL_WITH_ATTRS = /<UiTableCell\b([^>]*)>([\s\S]*?)<\/UiTableCell>/g

/** One control inside a cell: a kit button or link, a router link or an anchor, with its attributes (group 2) and label markup (group 3). */
const CONTROL = /<(UiButton|UiLink|RouterLink|a)\b((?:[^>"']|"[^"]*"|'[^']*')*)>([\s\S]*?)<\/\1>/g

/** The button looks, which make a control an action whatever its label says. */
const BUTTON_LOOK = /variant="(?:primary|secondary|danger|ghost|field)"|UiButtonVariant\.(?:Primary|Secondary|Danger|Ghost|Field)\b/

/** A label that is only a translation key without parameters, such as `{{ t('shared.view') }}`. */
const BARE_TRANSLATION = /^\{\{\s*t\(\s*(?:'[^']*'|"[^"]*"|`[^`]*`)\s*\)\s*\}\}$/

/** A button of the kit opened inside a cell. */
const BUTTON = /<(UiIconButton|UiButton)\b/g

/** A column header whose caption is the shared "Actions" word; group 1 holds its attributes. */
const ACTIONS_HEADER = /<UiTableHeader\b([^>]*)>\s*\{\{\s*t\('shared\.actions'\)\s*\}\}/g

/** The bare `actions` prop among a tag's attributes. */
const ACTIONS_PROP = /(?:^|\s)actions(?=\s|$)/

/**
 * Whether a control in a cell is an action rather than a data link.
 *
 * @param attrs - The control's attributes.
 * @param content - The control's label markup.
 * @param inActionsCell - Whether the cell carries the `actions` prop, where every control is an action.
 * @returns True when the control is a text action.
 */
const isTextAction = (attrs: string, content: string, inActionsCell: boolean): boolean => {
  if (inActionsCell || BUTTON_LOOK.test(attrs)) {
    return true
  }
  const label = content.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()
  return label !== '' && (!label.includes('{{') || BARE_TRANSLATION.test(label))
}

/**
 * Finds which rules a template breaks.
 *
 * @param template - The template markup.
 * @returns The broken rules, each once, in rule order.
 */
const brokenRules = (template: string): TableRule[] => {
  const broken = new Set<TableRule>()
  if (template.match(RAW_TAG)) {
    broken.add(TableRule.RawMarkup)
  }
  for (const cell of template.matchAll(CELL)) {
    if ((cell[1]?.match(BUTTON)?.length ?? 0) >= 2) {
      broken.add(TableRule.CellButtons)
    }
  }
  for (const cell of template.matchAll(CELL_WITH_ATTRS)) {
    const inActionsCell = ACTIONS_PROP.test(cell[1] ?? '')
    for (const control of (cell[2] ?? '').matchAll(CONTROL)) {
      if (isTextAction(control[2] ?? '', control[3] ?? '', inActionsCell)) {
        broken.add(TableRule.TextAction)
      }
    }
  }
  for (const header of template.matchAll(ACTIONS_HEADER)) {
    if (!ACTIONS_PROP.test(header[1] ?? '')) {
      broken.add(TableRule.ActionsHeader)
    }
  }
  return Object.values(TableRule).filter((rule) => broken.has(rule))
}

/**
 * Whether a file may break a rule.
 *
 * @param file - Path under `src`, with forward slashes.
 * @param rule - The broken rule.
 * @returns True when the allow list names the pair.
 */
const isAllowed = (file: string, rule: TableRule): boolean =>
  ALLOWED.some((entry) => entry.file === file && entry.rule === rule)

/** Every guarded file as its path under `src` and its template. */
const FILES: ReadonlyArray<readonly [string, string]> = ROOTS.flatMap(vueFiles).map(
  (path) => [relative(SRC, path).split(sep).join('/'), templateOf(readFileSync(path, 'utf8'))] as const,
)

describe('table shape', () => {
  it('reads the screens and the domain components, and leaves the kit out', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('views/ServersView.vue')
    expect(files).toContain('components/storefront/StorefrontListTable.vue')
    expect(files.some((file) => file.startsWith('components/ui/'))).toBe(false)
  })

  it.each(FILES)('%s draws its tables through the kit with the shared columns', (file, template) => {
    expect(brokenRules(template).filter((rule) => !isAllowed(file, rule))).toEqual([])
  })

  it.each(ALLOWED.map((entry) => [entry.file, entry.rule, entry.reason] as const))(
    '%s still needs its %s exception (%s)',
    (file, rule) => {
      const template = FILES.find(([path]) => path === file)?.[1]

      expect(template).toBeDefined()
      expect(brokenRules(template ?? '')).toContain(rule)
    },
  )

  describe('self test', () => {
    it('catches raw table markup', () => {
      expect(brokenRules('<table><tr><td>1</td></tr></table>')).toEqual([TableRule.RawMarkup])
      expect(brokenRules('<UiStack><th class="x">A</th></UiStack>')).toEqual([TableRule.RawMarkup])
    })

    it('catches two buttons in one cell', () => {
      const template = '<UiTableCell><UiIconButton label="Edit" /><UiButton @click="go">{{ row.name }}</UiButton></UiTableCell>'

      expect(brokenRules(template)).toEqual([TableRule.CellButtons])
    })

    it('catches an actions header without the actions prop', () => {
      expect(brokenRules(`<UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>`)).toEqual([TableRule.ActionsHeader])
    })

    it('catches a text button or link in an actions cell', () => {
      expect(brokenRules('<UiTableCell actions><UiButton variant="link">View</UiButton></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell actions><UiLink :to="p">View</UiLink></UiTableCell>')).toEqual([TableRule.TextAction])
    })

    it('catches a text action in a cell that is not an actions cell', () => {
      expect(brokenRules('<UiTableCell><UiLink :to="p" variant="primary">{{ item.name }}</UiLink></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell><UiButton :variant="UiButtonVariant.Secondary" @click="go">Open</UiButton></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell><UiLink :to="p" variant="link">View</UiLink></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules(`<UiTableCell><UiLink :to="p">{{ t('shared.edit') }}</UiLink></UiTableCell>`)).toEqual([TableRule.TextAction])
      expect(brokenRules(`<UiTableCell><UiButton variant="link" @click="x">{{ t('shared.view') }}</UiButton></UiTableCell>`)).toEqual([TableRule.TextAction])
      expect(brokenRules(`<UiTableCell><RouterLink :to="p">{{ t('shared.manage') }}</RouterLink></UiTableCell>`)).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell><a href="/x" class="y">Edit</a></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell>{{ x }}<UiLink :to="p" variant="link">{{ item.name }}</UiLink> <UiButton variant="ghost">Edit</UiButton></UiTableCell>')).toEqual([TableRule.TextAction])
    })

    it('reads a control whose attribute holds a greater-than sign', () => {
      expect(brokenRules('<UiTableCell><UiButton :disabled="a > b" variant="ghost" @click="() => go()">{{ row.name }}</UiButton></UiTableCell>')).toEqual([TableRule.TextAction])
      expect(brokenRules('<UiTableCell><UiButton variant="link" @click="() => go()">{{ row.name }}</UiButton></UiTableCell>')).toEqual([])
    })

    it('passes a data link in a data cell and an icon button in an actions cell', () => {
      expect(brokenRules('<UiTableCell><UiLink :to="p" variant="link">{{ client.name }}</UiLink></UiTableCell>')).toEqual([])
      expect(brokenRules('<UiTableCell><UiLink :to="p" variant="link">#{{ invoice.id }}</UiLink></UiTableCell>')).toEqual([])
      expect(brokenRules(`<UiTableCell><UiLink :to="p" variant="link">{{ t('x.invoice', { id: item.invoiceId }) }}</UiLink></UiTableCell>`)).toEqual([])
      expect(brokenRules('<UiTableCell><UiButton variant="link" @click="open">{{ cell.text }}</UiButton></UiTableCell>')).toEqual([])
      expect(brokenRules('<UiTableCell actions><UiIconButton label="Save" /></UiTableCell>')).toEqual([])
    })

    it('passes the kit shape', () => {
      const template = [
        '<UiTable><UiTableHead><UiTableRow>',
        `<UiTableHeader numeric>Total</UiTableHeader><UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>`,
        '</UiTableRow></UiTableHead><UiTableBody><UiTableRow>',
        '<UiTableCell numeric>1</UiTableCell><UiTableCell actions><UiRowActions :actions="a" /></UiTableCell>',
        '</UiTableRow></UiTableBody></UiTable>',
      ].join('')

      expect(brokenRules(template)).toEqual([])
    })

    it('passes one data button in a cell, a header named like the prop, and the draggable body tag', () => {
      expect(brokenRules('<UiTableCell><UiButton>{{ row.label }}</UiButton></UiTableCell>')).toEqual([])
      expect(brokenRules('<UiTableHeader>{{ t(\'shared.actionsTaken\') }}</UiTableHeader>')).toEqual([])
      expect(brokenRules('<draggable tag="tbody" item-key="id" />')).toEqual([])
    })

    it('ignores markup inside comments', () => {
      expect(templateOf('<template><!-- <table> --><UiStack /></template>')).toBe('<UiStack />')
    })
  })
})
