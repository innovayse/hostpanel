/**
 * Guards the single source of truth for page width: the shared `UiContainer`.
 *
 * Page and section content takes its width from the container, so no template, page or component
 * may narrow it with a `max-w-*` wrapper. The only caps left are the documented exceptions: a
 * reading measure on prose (`max-w-[75ch]`), `max-w-full` / `max-w-none`, and intrinsically small UI
 * (dialogs, drawers, cards, form fields, logos), each listed below with the file that owns it.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const ROOT = resolve(__dirname, '../..')

/** Source folders (and root files) that render markup. */
const SCANNED = ['components', 'pages', 'templates', 'layouts', 'error.vue']

/** Caps that never narrow layout: a prose measure and the two "do not exceed the parent" values. */
const ALWAYS_ALLOWED = [/^max-w-\[75ch\]$/, /^max-w-full$/, /^max-w-none$/]

/**
 * Files that keep a cap, with the tokens they may carry and why: small UI, form fields and logo
 * images are intrinsically narrow and are not page layout.
 */
const ALLOWED_BY_FILE: Record<string, string[]> = {
  'components/cart/CartContent.vue': ['max-w-[60%]'],
  'components/client/AuthLogo.vue': ['max-w-[200px]'],
  'components/client/AuthShell.vue': ['max-w-md'],
  'components/client/layout/Sidebar.vue': ['max-w-[85vw]'],
  'components/layout/Footer.vue': ['max-w-[180px]'],
  'components/layout/Header.vue': ['max-w-[160px]'],
  // A preview's screen sizes, not page layout: the panel owns them and a frame sits in a pane.
  'components/plugin/PreviewBlock.vue': ['max-w-[375px]', 'max-w-[980px]'],
  'components/ui/UiBreadcrumbs.vue': ['max-w-[150px]'],
  // A chat bubble is a share of the conversation's width, never the page's: the conversation sits in the container.
  'components/ui/UiChatBubble.vue': ['max-w-[85%]'],
  'components/ui/UiContainer.vue': ['max-w-7xl'],
  'components/ui/UiDialog.vue': ['max-w-sm', 'max-w-md', 'max-w-lg', 'max-w-2xl', 'max-w-6xl'],
  'components/ui/UiDrawer.vue': ['max-w-xs', 'max-w-md', 'max-w-lg', 'max-w-2xl'],
  'components/ui/UiEmptyState.vue': ['max-w-sm'],
  'components/ui/UiLogoWallItem.vue': ['max-w-[13rem]'],
  'components/ui/UiTooltip.vue': ['max-w-[calc(100vw-2rem)]'],
  'pages/client/domains/transfer.vue': ['max-w-lg'],
  'pages/client/order-success.vue': ['max-w-md'],
  'pages/client/services/[id]/setup.vue': ['max-w-xl'],
  'pages/client/tickets/new.vue': ['max-w-2xl'],
  'pages/payment/result.vue': ['max-w-md'],
  'templates/aurora/layout/AuroraFooter.vue': ['max-w-[140px]'],
  'templates/aurora/layout/AuroraHeader.vue': ['max-w-[140px]'],
  'templates/nova/layout/NovaFooter.vue': ['max-w-[120px]'],
  'templates/nova/layout/NovaHeader.vue': ['max-w-[120px]'],
}

/**
 * Lists every `.vue` file under a path.
 *
 * @param path - A folder or a single file, relative to the client root.
 * @returns The files, relative to the client root with forward slashes.
 */
const listVue = (path: string): string[] => {
  const full = join(ROOT, path)
  if (statSync(full).isFile()) {
    return [path]
  }
  return readdirSync(full, { withFileTypes: true }).flatMap((entry) => {
    const child = join(path, entry.name)
    if (entry.isDirectory()) {
      return listVue(child)
    }
    return entry.name.endsWith('.vue') ? [child.replaceAll('\\', '/')] : []
  })
}

/**
 * Reads every `max-w-*` token of a file.
 *
 * @param file - File path relative to the client root.
 * @returns The distinct `max-w-*` tokens, without any variant prefix.
 */
const capsOf = (file: string): string[] => {
  const source = readFileSync(join(ROOT, file), 'utf8')
  return [...new Set([...source.matchAll(/max-w-[^\s"'`,;]+/g)].map(match => match[0]))]
}

describe('page width comes from the shared container', () => {
  const files = SCANNED.flatMap(listVue)

  it('scans the markup folders', () => {
    expect(files.length).toBeGreaterThan(100)
  })

  it.each(files)('%s carries no layout max-w cap', (file) => {
    const allowed = ALLOWED_BY_FILE[file] ?? []
    const stray = capsOf(file).filter((token) => {
      return !ALWAYS_ALLOWED.some(rule => rule.test(token)) && !allowed.includes(token)
    })

    expect(stray, `${relative(ROOT, join(ROOT, file))} narrows the container`).toEqual([])
  })

  it('has no stale entry in the allow-list', () => {
    for (const [file, tokens] of Object.entries(ALLOWED_BY_FILE)) {
      const source = readFileSync(join(ROOT, file), 'utf8')
      for (const token of tokens) {
        expect(source, `${file} no longer uses ${token}`).toContain(token)
      }
    }
  })
})
