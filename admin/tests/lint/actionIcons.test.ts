/**
 * Guard: every action button of a screen carries an icon, drawn from the one map of action kinds.
 *
 * Under `src/views` and `src/components` (the kit in `components/ui` included) a `UiButton` or a `UiLink` in a button
 * look must:
 * - name its icon: `action` (a `UiActionKind`, whose icon the kit owns in `utils/ui/actions`) or its own `icon` (an
 *   `Icon` entry), so no action reads as bare text;
 * - not draw the icon by hand as a `UiIcon` child, which would bypass the one map and the one placement;
 * - not sit in the `actions` slot of a `UiPageHeader`: header actions are data (`UiPageHeader` `actions` prop, `UiHeaderAction`,
 *   whose `icon` is required), so each one has its icon and folds into the "…" menu on a phone with the other actions.
 *
 * Text-only controls are the `link` look (a link inside prose), a plain `UiLink` (navigation text).
 * A few files hold choice rows that are labelled by data, not actions;
 * each is named below with its rule and the reason, and the list is checked too: an entry whose file is gone or no longer
 * breaks its rule fails, so the list cannot rot.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The folders the guard reads. */
const ROOTS: readonly string[] = [join(SRC, 'views'), join(SRC, 'components')]

/** The rules the guard enforces. */
enum ActionIconRule {
  /** A button or link in a button look with neither `action` nor `icon`. */
  MissingIcon = 'missing-icon',
  /** An action that draws its icon as a `UiIcon` child instead of the `icon` or `action` prop. */
  InlineIcon = 'inline-icon',
  /** A button or link in the `actions` slot of a page header instead of the `actions` prop. */
  HeaderSlot = 'header-slot',
}

/** One file allowed to break one rule, with the reason. */
interface AllowedException {
  /** Path under `src`, with forward slashes. */
  file: string
  /** The rule the file may break. */
  rule: ActionIconRule
  /** Why the file is not an action that needs it. */
  reason: string
}

/** The named exceptions: rows that pick a record, labelled by the record's own data. */
const ALLOWED: readonly AllowedException[] = [
  { file: 'components/clients/ClientUserPicker.vue', rule: ActionIconRule.MissingIcon, reason: 'search results: each button is a user picked by name' },
  { file: 'components/plugins/view/PluginChatEmpty.vue', rule: ActionIconRule.MissingIcon, reason: 'suggestions: each button sends the suggestion text it shows' },
  { file: 'components/servers/GroupFormModal.vue', rule: ActionIconRule.MissingIcon, reason: 'member lists: each button moves a server named by its own label' },
  { file: 'views/SupportOpenTicketView.vue', rule: ActionIconRule.MissingIcon, reason: 'search results: each button is a client picked by name' },
  { file: 'components/ui/UiPagination.vue', rule: ActionIconRule.MissingIcon, reason: 'page numbers: each button is the page it numbers' },
  { file: 'views/SupportTicketDetailView.vue', rule: ActionIconRule.HeaderSlot, reason: 'the Details button shows below lg only, a responsive choice the header action data does not carry' },
  { file: 'views/SlideFormView.vue', rule: ActionIconRule.MissingIcon, reason: 'icon presets: each button picks the icon it draws' },
]

/**
 * Lists every `.vue` file under a folder.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the components found.
 */
const vueFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return vueFiles(path)
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

/** One page header with everything inside it. */
const HEADER = /<UiPageHeader\b[\s\S]*?<\/UiPageHeader>/g

/** A kit button or link opened anywhere. */
const ANY_ACTION = /<(?:UiButton|UiLink)\b/

/** One opening tag of a kit button or link. */
const OPENING = /<(UiButton|UiLink)\b/g

/** The text-only look: the link look. */
const TEXT_ONLY_LOOK = /variant="link"|UiButtonVariant\.Link\b/

/** A variant attribute, bound or literal. */
const VARIANT_ATTR = /(?:^|\s):?variant=/

/** The `action` or `icon` attribute, bound or literal. */
const ICON_ATTR = /(?:^|\s):?(?:action|icon)=/

/**
 * Reads one tag from its `<` to its closing `>`, honouring quoted attribute values that may hold a `>`.
 *
 * @param template - The template markup.
 * @param from - Index of the tag's `<`.
 * @returns Index of the closing `>`, or -1 when the tag never closes.
 */
const tagEnd = (template: string, from: number): number => {
  let quote: string | null = null
  for (let index = from; index < template.length; index++) {
    const char = template[index]
    if (quote !== null) {
      quote = char === quote ? null : quote
    } else if (char === '"' || char === "'") {
      quote = char
    } else if (char === '>') {
      return index
    }
  }
  return -1
}

/**
 * Finds which rules a template breaks.
 *
 * @param template - The template markup.
 * @returns The broken rules, each once, in rule order.
 */
const brokenRules = (template: string): ActionIconRule[] => {
  const broken = new Set<ActionIconRule>()
  for (const opening of template.matchAll(OPENING)) {
    const start = opening.index ?? 0
    const end = tagEnd(template, start)
    const tag = template.slice(start, end + 1)
    const attrs = tag.slice(opening[0].length, -1)
    const name = opening[1] ?? ''
    const isButtonLook = name === 'UiButton' ? !TEXT_ONLY_LOOK.test(attrs) : VARIANT_ATTR.test(attrs) && !TEXT_ONLY_LOOK.test(attrs)
    if (!isButtonLook || tag.endsWith('/>')) {
      continue
    }
    if (!ICON_ATTR.test(attrs)) {
      broken.add(ActionIconRule.MissingIcon)
    }
    const close = template.indexOf(`</${name}>`, end)
    if (/^\s*<UiIcon\b/.test(template.slice(end + 1, close))) {
      broken.add(ActionIconRule.InlineIcon)
    }
  }
  for (const header of template.matchAll(HEADER)) {
    if (ANY_ACTION.test(header[0])) {
      broken.add(ActionIconRule.HeaderSlot)
    }
  }
  return Object.values(ActionIconRule).filter((rule) => broken.has(rule))
}

/** Every guarded file as its path under `src` and its template. */
const FILES: ReadonlyArray<readonly [string, string]> = ROOTS.flatMap(vueFiles).map(
  (path) => [relative(SRC, path).split(sep).join('/'), templateOf(readFileSync(path, 'utf8'))] as const,
)

/**
 * Whether a file may break a rule.
 *
 * @param file - Path under `src`, with forward slashes.
 * @param rule - The broken rule.
 * @returns True when the allow list names the pair.
 */
const isAllowed = (file: string, rule: ActionIconRule): boolean => ALLOWED.some((entry) => entry.file === file && entry.rule === rule)

describe('action icons', () => {
  it('reads the screens, the domain components and the kit', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('views/ServersView.vue')
    expect(files).toContain('components/ui/UiDialog.vue')
  })

  it.each(FILES)('%s gives every action its icon through the kit', (file, template) => {
    expect(brokenRules(template).filter((rule) => !isAllowed(file, rule))).toEqual([])
  })

  it.each(ALLOWED.map((entry) => [entry.file, entry.rule, entry.reason] as const))('%s still needs its %s exception (%s)', (file, rule) => {
    const template = FILES.find(([path]) => path === file)?.[1]

    expect(template).toBeDefined()
    expect(brokenRules(template ?? '')).toContain(rule)
  })

  describe('self test', () => {
    it('catches a button or a button-look link with no icon', () => {
      expect(brokenRules('<UiButton variant="primary" @click="save">Save</UiButton>')).toEqual([ActionIconRule.MissingIcon])
      expect(brokenRules('<UiButton @click="save">Save</UiButton>')).toEqual([ActionIconRule.MissingIcon])
      expect(brokenRules('<UiButton :disabled="a > b" @click="save">Save</UiButton>')).toEqual([ActionIconRule.MissingIcon])
      expect(brokenRules('<UiLink :to="p" :variant="UiButtonVariant.Primary">Add</UiLink>')).toEqual([ActionIconRule.MissingIcon])
      expect(brokenRules('<UiLink :to="p" variant="secondary">Add</UiLink>')).toEqual([ActionIconRule.MissingIcon])
    })

    it('catches an icon drawn by hand inside the action', () => {
      const template = '<UiButton :action="UiActionKind.Save"><UiIcon :markup="Icon.Check" /> Save</UiButton>'

      expect(brokenRules(template)).toEqual([ActionIconRule.InlineIcon])
      expect(brokenRules('<UiButton variant="primary"><UiIcon :markup="Icon.Add" /> Add</UiButton>')).toEqual([
        ActionIconRule.MissingIcon,
        ActionIconRule.InlineIcon,
      ])
    })

    it('passes an action that names its kind or its icon', () => {
      expect(brokenRules('<UiButton :action="UiActionKind.Save" variant="primary">Save</UiButton>')).toEqual([])
      expect(brokenRules('<UiButton :icon="Icon.Mail" :disabled="a > b">Mail</UiButton>')).toEqual([])
      expect(brokenRules('<UiLink :to="p" variant="primary" :action="UiActionKind.Add">Add</UiLink>')).toEqual([])
    })

    it('passes the text-only look: a link in prose and a plain link', () => {
      expect(brokenRules('<UiButton variant="link" @click="x">terms</UiButton>')).toEqual([])
      expect(brokenRules('<UiButton :variant="UiButtonVariant.Link" @click="x">terms</UiButton>')).toEqual([])
      expect(brokenRules('<UiLink :to="p">Name</UiLink>')).toEqual([])
      expect(brokenRules('<UiLink :to="p" variant="link">Name</UiLink>')).toEqual([])
    })

    it('catches a button or link in the actions slot of a page header', () => {
      const button = '<UiPageHeader :title="t"><template #actions><UiButton :action="UiActionKind.Add">Add</UiButton></template></UiPageHeader>'
      const link = '<UiPageHeader :title="t"><template #actions><UiLink :to="p" :icon="Icon.Add" variant="primary">Add</UiLink></template></UiPageHeader>'

      expect(brokenRules(button)).toEqual([ActionIconRule.HeaderSlot])
      expect(brokenRules(link)).toEqual([ActionIconRule.HeaderSlot])
    })

    it('passes a header whose actions are data, with a help link or a filter in the slot', () => {
      expect(brokenRules('<UiPageHeader :actions="headerActions"><template #actions><PageHelpLink :url="u" /></template></UiPageHeader>')).toEqual([])
      expect(brokenRules('<UiPageHeader :title="t" /><UiButton :action="UiActionKind.Add">Add</UiButton>')).toEqual([])
    })

    it('ignores markup inside comments', () => {
      expect(templateOf('<template><!-- <UiButton>Save</UiButton> --><UiStack /></template>')).toBe('<UiStack />')
    })
  })
})
