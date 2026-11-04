/**
 * Scanner for the plugin-neutral rule: the host interface names no plugin. A plugin id or a plugin entry
 * point type name inside a source file is a reference to one deployment's plugin. The names are those of
 * the in-tree plugin manifests (the `id` and the simple name of the `entryPoint` of each `plugin.json`
 * under `backend/src/Innovayse.Providers.*`); the backend architecture test `PluginNeutralSourceTests`
 * reads them from the manifests and holds the backend to the same rule.
 *
 * @module tests/helpers/pluginNeutrality
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

/** The plugin ids and entry point type names of the in-tree plugin manifests. */
export const PLUGIN_NAMES: readonly string[] = [
  'innovayse-cwp',
  'innovayse-cwp7',
  'inecobank',
  'CwpProvisioningProvider',
  'Cwp7ProvisioningProvider',
  'InecobankPaymentGateway',
]

/** A source file as the scanner reads it. */
export interface ScannedSource {
  /** Path relative to the product root, forward slashes. */
  name: string
  /** The file text. */
  text: string
}

/** The file extensions that carry authored interface code or text. */
const SCANNED_EXTENSIONS = ['.vue', '.ts', '.json']

/** Folder names that hold dependencies or build output, never authored code. */
const SKIPPED_FOLDERS = new Set(['node_modules', '.nuxt', '.output', 'dist'])

/**
 * Escapes a text for use inside a regular expression.
 *
 * @param value - Plain text.
 * @returns The text with every metacharacter escaped.
 */
const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/**
 * Lists the files under a path: the file itself, or every scanned file in the folder tree.
 *
 * @param path - Absolute file or folder path.
 * @returns Absolute file paths.
 */
const filesUnder = (path: string): string[] => {
  if (!statSync(path).isDirectory()) {
    return SCANNED_EXTENSIONS.some((extension) => path.endsWith(extension)) ? [path] : []
  }
  return readdirSync(path)
    .filter((child) => !SKIPPED_FOLDERS.has(child))
    .flatMap((child) => filesUnder(join(path, child)))
}

/**
 * Lists the plugin names a text contains as whole tokens. Matching is case sensitive and a name inside a
 * longer identifier or id (`inecobank-more`, `InecobankClient`) does not count.
 *
 * @param text - Source text.
 * @param names - The plugin ids and type names to look for.
 * @returns The names found.
 */
export const namesIn = (text: string, names: readonly string[] = PLUGIN_NAMES): string[] =>
  names.filter((name) => new RegExp(`(?<![A-Za-z0-9-])${escapeRegExp(name)}(?![A-Za-z0-9-])`).test(text))

/**
 * Finds the sources that name a plugin.
 *
 * @param sources - Named source texts.
 * @param names - The plugin ids and type names to look for.
 * @returns One `file -> name` line per hit.
 */
export const offenders = (sources: readonly ScannedSource[], names: readonly string[] = PLUGIN_NAMES): string[] =>
  sources.flatMap((source) => namesIn(source.text, names).map((hit) => `${source.name} -> ${hit}`))

/**
 * Reads the authored sources of a product: the given entries (folders walked recursively, or single files)
 * with a scanned extension, never dependencies or build output.
 *
 * @param root - The product root.
 * @param entries - Folders and files under the root.
 * @returns The sources, in directory order.
 */
export const readSources = (root: string, entries: readonly string[]): ScannedSource[] =>
  entries
    .flatMap((entry) => filesUnder(join(root, entry)))
    .map((file) => ({ name: relative(root, file).split(sep).join('/'), text: readFileSync(file, 'utf8') }))
