/**
 * Guard: every table in a screen or domain component is drawn by the kit and has the one shape of #1036.
 *
 * Under `src/views` and `src/components` (the kit in `components/ui` excepted) a template must not:
 * - render raw `table`, `thead`, `tbody`, `tfoot`, `tr`, `td` or `th` markup (UiTable and its parts draw those);
 * - put two or more UiIconButton or UiButton controls in one UiTableCell (row actions go through UiRowActions);
 * - caption a column "Actions" without the `actions` prop on its UiTableHeader (the shared actions column width).
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

/** A button of the kit opened inside a cell. */
const BUTTON = /<(UiIconButton|UiButton)\b/g

/** A column header whose caption is the shared "Actions" word; group 1 holds its attributes. */
const ACTIONS_HEADER = /<UiTableHeader\b([^>]*)>\s*\{\{\s*t\('shared\.actions'\)\s*\}\}/g

/** The bare `actions` prop among a tag's attributes. */
const ACTIONS_PROP = /(?:^|\s)actions(?=\s|$)/

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
      const template = '<UiTableCell><UiIconButton label="Edit" /><UiButton>Delete</UiButton></UiTableCell>'

      expect(brokenRules(template)).toEqual([TableRule.CellButtons])
    })

    it('catches an actions header without the actions prop', () => {
      expect(brokenRules(`<UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>`)).toEqual([TableRule.ActionsHeader])
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

    it('passes one button in a cell, a header named like the prop, and the draggable body tag', () => {
      expect(brokenRules('<UiTableCell><UiButton>Retry</UiButton></UiTableCell>')).toEqual([])
      expect(brokenRules('<UiTableHeader>{{ t(\'shared.actionsTaken\') }}</UiTableHeader>')).toEqual([])
      expect(brokenRules('<draggable tag="tbody" item-key="id" />')).toEqual([])
    })

    it('ignores markup inside comments', () => {
      expect(templateOf('<template><!-- <table> --><UiStack /></template>')).toBe('<UiStack />')
    })
  })
})
