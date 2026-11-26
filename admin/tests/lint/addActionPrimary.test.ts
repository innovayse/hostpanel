/**
 * Guard: a create action is always primary.
 *
 * `UiActionKind.Add` is the create action and the kit draws it in the primary look whatever a call site says
 * (`resolveActionVariant`). So that no call site even tries to override it, this guard reads every file under
 * `src` and fails when a `UiButton` or `UiLink` named `UiActionKind.Add`, or a header action built with
 * `actionIcons[UiActionKind.Add]`, is given a look other than primary. An add that is not a create (a row of a
 * form, a time entry, a group) names `UiActionKind.AddRow`, which keeps its own look and is not guarded.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** One opening tag of a kit button or link. */
const OPENING = /<(?:UiButton|UiLink)\b/g

/** The create action named on a tag. */
const ADD_ACTION = /(?:^|\s):action="UiActionKind\.Add"/

/** A variant attribute with its value, bound or literal. */
const VARIANT_ATTR = /(?:^|\s)(:?)variant=("[^"]*"|'[^']*')/

/** The create action drawn as the icon of a header action object. */
const ADD_ICON = /actionIcons\[UiActionKind\.Add\]/g

/** The primary look, written as the enum member or as the literal, without its attribute quotes. */
const PRIMARY_VALUE = /^(?:UiButtonVariant\.Primary|primary)$/

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
 * Reads one tag from its `<` to its closing `>`, honouring quoted attribute values that may hold a `>`.
 *
 * @param source - The file contents.
 * @param from - Index of the tag's `<`.
 * @returns Index of the closing `>`, or -1 when the tag never closes.
 */
const tagEnd = (source: string, from: number): number => {
  let quote: string | null = null
  for (let index = from; index < source.length; index++) {
    const char = source[index]
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
 * Whether a variant attribute is the primary look.
 *
 * @param attrs - The attributes of one tag.
 * @returns True when the tag has no variant (the kit then draws it primary) or has the primary one.
 */
const isPrimaryOrAbsent = (attrs: string): boolean => {
  const match = VARIANT_ATTR.exec(attrs)
  return match === null || PRIMARY_VALUE.test((match[2] ?? '').slice(1, -1))
}

/**
 * One-based line of an index in a source.
 *
 * @param source - The file contents.
 * @param index - Character index.
 * @returns The line number.
 */
const lineOf = (source: string, index: number): number => source.slice(0, index).split('\n').length

/**
 * Finds the create actions of a file that are not primary.
 *
 * A header action is an object literal naming its icon and look, so the object around `actionIcons[UiActionKind.Add]`
 * is read for its `variant:` property; the look is required there, so a missing one counts too.
 *
 * @param source - The `.vue` file contents.
 * @returns The one-based lines of the violations.
 */
const violations = (source: string): number[] => {
  const lines: number[] = []
  for (const opening of source.matchAll(OPENING)) {
    const start = opening.index ?? 0
    const attrs = source.slice(start, tagEnd(source, start) + 1)
    if (ADD_ACTION.test(attrs) && !isPrimaryOrAbsent(attrs)) {
      lines.push(lineOf(source, start))
    }
  }
  for (const icon of source.matchAll(ADD_ICON)) {
    const at = icon.index ?? 0
    const object = source.slice(source.lastIndexOf('{', at), source.indexOf('}', at) + 1)
    if (!/variant:\s*UiButtonVariant\.Primary\b/.test(object)) {
      lines.push(lineOf(source, at))
    }
  }
  return lines.sort((a, b) => a - b)
}

/** Every guarded file as its path under `src` and its contents. */
const FILES: ReadonlyArray<readonly [string, string]> = vueFiles(SRC).map(
  (path) => [relative(SRC, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

describe('create actions are primary', () => {
  it('reads the screens, the domain components and the kit', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('views/ServersView.vue')
    expect(files).toContain('components/ui/UiButton.vue')
  })

  it.each(FILES)('%s gives no create action a look other than primary', (file, source) => {
    expect(violations(source).map((line) => `${file}:${line}`)).toEqual([])
  })

  describe('self test', () => {
    it('catches a create button or link given another look', () => {
      expect(violations('<UiButton :action="UiActionKind.Add" :variant="UiButtonVariant.Secondary">Add</UiButton>')).toEqual([1])
      expect(violations('x\n<UiLink :action="UiActionKind.Add" variant="link" :to="p">Add</UiLink>')).toEqual([2])
      expect(violations('<UiButton :disabled="a > b" :action="UiActionKind.Add" variant="ghost">Add</UiButton>')).toEqual([1])
    })

    it('catches a header action that is a create in another look, or in none', () => {
      expect(violations("const a = [{ label: 'x', icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Secondary, testid: 't' }]")).toEqual([1])
      expect(violations("const a = [{ label: 'x', icon: actionIcons[UiActionKind.Add], testid: 't' }]")).toEqual([1])
    })

    it('passes a primary create, with the look given or left to the kit', () => {
      expect(violations('<UiButton :action="UiActionKind.Add" :variant="UiButtonVariant.Primary">Add</UiButton>')).toEqual([])
      expect(violations('<UiLink :action="UiActionKind.Add" variant="primary" :to="p">Add</UiLink>')).toEqual([])
      expect(violations('<UiButton :action="UiActionKind.Add">Add</UiButton>')).toEqual([])
      expect(violations("const a = [{ icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 't' }]")).toEqual([])
    })

    it('does not guard an add that is not a create', () => {
      expect(violations('<UiButton :action="UiActionKind.AddRow" :variant="UiButtonVariant.Secondary">Add line</UiButton>')).toEqual([])
      expect(violations("const a = [{ icon: actionIcons[UiActionKind.AddRow], variant: UiButtonVariant.Secondary, testid: 't' }]")).toEqual([])
      expect(violations('<UiButton :action="UiActionKind.Save" variant="secondary">Save</UiButton>')).toEqual([])
    })
  })
})
