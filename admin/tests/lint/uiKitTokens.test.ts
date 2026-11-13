/**
 * Guard: the component kit paints with theme tokens only. A hex colour or `text-white` in a file under
 * `src/components/ui` ties the component to one theme (white text on a surface that turns light, a fill
 * that ignores the palette), so this test reads every kit component and fails as soon as one comes back.
 * Comments are stripped first: a doc comment may quote an example colour.
 */
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The component kit folder. */
const UI_ROOT = join(__dirname, '..', '..', 'src', 'components', 'ui')

/** A hex colour literal (`#fff`, `#3B82F6`, `#3B82F680`); a slot shorthand such as `#default` has non-hex letters and never matches. */
const HEX_COLOUR = /#[0-9a-fA-F]{3,8}(?![\w-])/

/** The `text-white` utility, with or without a variant or an opacity (`hover:text-white`, `text-white/60`). */
const TEXT_WHITE = /(?<![\w-])text-white(?![\w-])/

/**
 * Removes block, HTML and whole-line or trailing `//` comments, so only code is scanned.
 *
 * @param source - File text.
 * @returns The text without comments.
 */
const stripComments = (source: string): string => source
  .replace(/\/\*[\s\S]*?\*\//g, '')
  .replace(/<!--[\s\S]*?-->/g, '')
  .replace(/(^|\s)\/\/.*$/gm, '$1')

/** Name and comment-free text of every kit component. */
const components: Array<[string, string]> = readdirSync(UI_ROOT)
  .filter((file) => file.endsWith('.vue'))
  .map((file): [string, string] => [file, stripComments(readFileSync(join(UI_ROOT, file), 'utf8'))])

describe('component kit tokens', () => {
  it('finds the kit components', () => {
    expect(components.length).toBeGreaterThan(20)
  })

  it.each(components)('%s has no hex colour', (_file, code) => {
    expect(code).not.toMatch(HEX_COLOUR)
  })

  it.each(components)('%s has no text-white', (_file, code) => {
    expect(code).not.toMatch(TEXT_WHITE)
  })
})
