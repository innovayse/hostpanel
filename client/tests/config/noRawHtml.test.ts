/**
 * Guard: no raw HTML tag in a template outside the kit. Pages, layouts, templates and domain components are composed
 * from `Ui*` components; a raw `div`, `span`, `button`, `input` and the like belongs only in `components/ui`, where the
 * kit draws them. A new visual need is a new `Ui*` component first.
 *
 * The test reads the `<template>` of every `.vue` file under `components`, `pages`, `layouts` and `templates`, plus
 * `app.vue` and `error.vue` (the kit left out), HTML comments removed, and fails with the file and the tag. Component
 * tags (`Ui*`, `NuxtLink`), `<template>`, `<slot>`, `<component>` and `<i18n-t>` are not in the forbidden list. The admin
 * has the same guard in `admin/tests/lint/noRawHtml.test.ts`: the two apps cannot read each other's files, so each
 * reads its own.
 *
 * @module tests/config/noRawHtml.test
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The folders and root files whose templates the guard reads. */
const SOURCES: readonly string[] = ['components', 'pages', 'layouts', 'templates', 'app.vue', 'error.vue']

/** The kit folder, the one place raw tags are drawn. */
const KIT = join(CLIENT_ROOT, 'components', 'ui')

/** The raw HTML tags a template outside the kit must not hold. */
const FORBIDDEN_TAGS: readonly string[] = [
  'div', 'span', 'p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'li', 'a', 'button', 'input', 'textarea',
  'select', 'label', 'img', 'svg', 'section', 'header', 'footer', 'nav', 'aside', 'main', 'form', 'table', 'thead',
  'tbody', 'tfoot', 'tr', 'td', 'th', 'mark', 'strong', 'em', 'small', 'hr', 'br', 'pre', 'code',
]

/** An opening tag of a forbidden element, the tag name in group 1. */
const RAW_TAG = new RegExp(`<(${FORBIDDEN_TAGS.join('|')})(?=[\\s>/])`, 'g')

/**
 * Lists every `.vue` file at a path, the kit left out.
 *
 * @param path Absolute file or folder to walk.
 * @returns Absolute paths of the components found; empty when the path does not exist.
 */
const vueFiles = (path: string): string[] => {
  if (!existsSync(path) || path === KIT) {
    return []
  }
  if (statSync(path).isDirectory()) {
    return readdirSync(path).flatMap((name) => vueFiles(join(path, name)))
  }
  return path.endsWith('.vue') ? [path] : []
}

/**
 * The root template of a single file component without its HTML comments; empty when there is none.
 *
 * @param source The `.vue` file contents.
 * @returns The template markup.
 */
const templateOf = (source: string): string => {
  const open = /^<template\b[^>]*>/m.exec(source)
  const end = source.lastIndexOf('</template>')
  if (!open || end < open.index) {
    return ''
  }
  return source.slice(open.index + open[0].length, end).replace(/<!--[\s\S]*?-->/g, '')
}

/**
 * The raw tags a component source holds in its template.
 *
 * @param source The `.vue` file contents.
 * @returns The forbidden tag names found, each once, in order of first use.
 */
const rawTagsOf = (source: string): string[] => [
  ...new Set([...templateOf(source).matchAll(RAW_TAG)].map((match) => match[1] ?? '')),
]

/** Every guarded file as its path under the client root and the raw tags its template holds. */
const FILES: ReadonlyArray<readonly [string, string[]]> = SOURCES
  .flatMap((source) => vueFiles(join(CLIENT_ROOT, source)))
  .map((path) => [relative(CLIENT_ROOT, path).split('\\').join('/'), rawTagsOf(readFileSync(path, 'utf8'))] as const)

describe('no raw HTML outside the kit', () => {
  it('reads pages, layouts, templates and domain components, and leaves the kit out', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('app.vue')
    expect(files).toContain('error.vue')
    expect(files.some((file) => file.startsWith('pages/'))).toBe(true)
    expect(files.some((file) => file.startsWith('templates/'))).toBe(true)
    expect(files.some((file) => file.startsWith('components/ui/'))).toBe(false)
  })

  it.each(FILES)('%s holds no raw tag in its template', (file, tags) => {
    expect({ file, tags }).toEqual({ file, tags: [] })
  })

  describe('self test', () => {
    it('catches a raw tag in a template', () => {
      expect(rawTagsOf('<template><div class="x"><span>a</span></div></template>')).toEqual(['div', 'span'])
      expect(rawTagsOf('<template>\n  <UiStack>\n    <input\n      v-model="a"\n    />\n  </UiStack>\n</template>')).toEqual(['input'])
      expect(rawTagsOf('<template><br/></template>')).toEqual(['br'])
    })

    it('passes the kit, template, slot, component and i18n-t tags', () => {
      const source = '<template><UiStack><template #x><slot /></template><component :is="c" /><i18n-t keypath="k" /><NuxtLink to="/" /></UiStack></template>'

      expect(rawTagsOf(source)).toEqual([])
    })

    it('does not mistake a longer tag name for a forbidden one', () => {
      expect(rawTagsOf('<template><path d="M0" /><article /><abbr /><address /><UiSpan /></template>')).toEqual([])
    })

    it('ignores comments and tag-looking text in the script', () => {
      const source = [
        '<script setup lang="ts">',
        'const html = \'<div>\'',
        '</script>',
        '<template><!-- <div> --><UiStack>{{ html }}</UiStack></template>',
      ].join('\n')

      expect(rawTagsOf(source)).toEqual([])
    })
  })
})
