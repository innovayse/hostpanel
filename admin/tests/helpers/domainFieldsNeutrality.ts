/**
 * Scanner for the zone-neutral rule of the extra domain fields: which fields a registry asks for, for which zones, comes
 * from the registrar plugin's manifest, so the admin's code, tests and texts about them name no extension, no country code and
 * no national document. The scanner recognises the shape of a zone instead of listing zones, so it names none itself.
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

/** A source file as the scanner reads it. */
export interface DomainFieldSource {
  /** Path relative to the product root, forward slashes. */
  name: string
  /** The file text. */
  text: string
}

/** The marks in a file name that put the file in the feature. */
const FEATURE_NAME_MARKS = ['domainfield', 'domainfields'] as const

/** Folder names that hold dependencies or build output, never authored code. */
const SKIPPED_FOLDERS = new Set(['node_modules', '.nuxt', '.output', 'dist', 'locales'])

/** The extensions that carry authored code. */
const SCANNED_EXTENSIONS = ['.vue', '.ts']

/** Generic extensions that never appear as plain text in the feature. */
const GENERIC_ZONES = new Set(['com', 'net', 'org', 'info', 'biz'])

/** A quoted string literal without interpolation. */
const LITERAL = /(['"])((?:(?!\1)[^\\\r\n${}])*)\1/g

/** A literal that is a host name. */
const HOST_NAME = /^[a-z0-9-]+(\.[a-z0-9-]+)+$/

/** Words that name a national document or register, in code, comments and texts. */
const WORDS = /\b(passport|taxpayer|tax[ _-]?id|national[ _-]?id|identity[ _-]?card)\b/gi

/**
 * Tells whether a literal reads as a zone: a bare or dotted two letter label, a generic extension, a punycode label or a host
 * name that ends in one of them.
 *
 * @param value - The literal's text.
 * @returns True when the literal names a zone.
 */
const isZone = (value: string): boolean => {
  const bare = value.replace(/^\.+/, '')
  if (/^[a-z]{2}$/.test(bare) || GENERIC_ZONES.has(bare) || value.includes('xn--')) {
    return true
  }
  if (!HOST_NAME.test(value)) {
    return false
  }
  const last = value.slice(value.lastIndexOf('.') + 1)
  return last.length === 2 || GENERIC_ZONES.has(last)
}

/**
 * Finds what a source holds that is specific to a zone or a country.
 *
 * @param rawText - The source text.
 * @returns One short description per hit, in the order found.
 */
export const hitsIn = (rawText: string): string[] => {
  // The language of a script block (`lang="ts"`) names the language of the code, not a zone.
  const text = rawText.replace(/\blang="[a-z]+"/g, '')
  const hits = [...text.matchAll(LITERAL)].filter(match => isZone(match[2] ?? '')).map(match => `zone literal ${match[0]}`)
  return [...hits, ...[...text.matchAll(WORDS)].map(match => `word '${match[0]}'`)]
}

/**
 * Lists the sources that hold something specific to a zone or a country.
 *
 * @param sources - The sources to read.
 * @returns One `name -> hit` line per hit.
 */
export const offenders = (sources: readonly DomainFieldSource[]): string[] =>
  sources.flatMap(source => hitsIn(source.text).map(hit => `${source.name} -> ${hit}`))

/**
 * Lists the files under a path.
 *
 * @param path - Absolute file or folder path.
 * @returns Absolute file paths with a scanned extension.
 */
const filesUnder = (path: string): string[] => {
  if (!statSync(path).isDirectory()) {
    return SCANNED_EXTENSIONS.some(extension => path.endsWith(extension)) ? [path] : []
  }
  return readdirSync(path)
    .filter(child => !SKIPPED_FOLDERS.has(child))
    .flatMap(child => filesUnder(join(path, child)))
}

/**
 * Reads the authored files of the feature under the given folders: those whose name carries a feature mark, the neutrality
 * test files themselves excluded.
 *
 * @param root - The product root.
 * @param folders - Folders (or files) of the root to read.
 * @returns The sources.
 */
export const readFeatureSources = (root: string, folders: readonly string[]): DomainFieldSource[] =>
  folders
    .flatMap(folder => filesUnder(join(root, folder)))
    .filter(path => {
      const name = path.split(sep).pop()!.toLowerCase()
      return !name.includes('neutrality') && FEATURE_NAME_MARKS.some(mark => name.includes(mark))
    })
    .map(path => ({ name: relative(root, path).split(sep).join('/'), text: readFileSync(path, 'utf8') }))
