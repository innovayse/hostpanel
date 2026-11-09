/**
 * `node scripts/generate-locale-fallbacks.mjs [--clean]`: writes a placeholder page, in the reader's
 * language, for every English page that a language does not have yet, into `docs/_fallback/`. The
 * folder is ignored by git; the site config maps it back to `/ru/...` and `/hy/...`. Run by the `prebuild`
 * and `predev` scripts of `package.json`, after the API reference pages have been generated and before
 * VitePress reads the sources, and removed again by `postbuild`. `--clean` only removes the folder.
 * See `scripts/lib/localeFallbacks.mjs` for the rules.
 */
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { cleanFallbacks, generateFallbacks } from './lib/localeFallbacks.mjs'

/** The docs folder. */
const docsRoot = join(dirname(fileURLToPath(import.meta.url)), '..')

if (process.argv.includes('--clean')) {
  cleanFallbacks(docsRoot)
  console.log('locale fallbacks: removed')
} else {
  const { created, orphans } = generateFallbacks(docsRoot)
  const summary = Object.entries(created)
    .map(([locale, count]) => `${locale} ${count}`)
    .join(', ')
  console.log(`locale fallbacks: generated ${summary}`)
  for (const orphan of orphans) {
    console.warn(`locale fallbacks: ${orphan} has no English page`)
  }
}
