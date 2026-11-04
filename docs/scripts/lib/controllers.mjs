/**
 * Reads who may call each action, and which rate limit applies to it, from the attributes on the
 * API's controllers. The OpenAPI document does not carry either: it states one security
 * requirement for the whole API and tags every action that is not anonymous, not under api/auth
 * and not a client action as "Admin", including the few that any signed-in user may call.
 *
 * The result is keyed by `METHOD /normalised/path`, the same way operations of the document are
 * keyed, so the generator can look an operation up. An action this reader cannot place is simply
 * absent: the page then says nothing about access rather than guessing.
 */

import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const METHOD_ATTRIBUTE = /\[Http(Get|Post|Put|Patch|Delete)(?:\(\s*"([^"]*)"[^)]*\))?\]/g
const CLASS_DECLARATION = /^\s*public\s+(?:(?:sealed|abstract|partial|static)\s+)*class\s+(\w+)/
const METHOD_DECLARATION = /^ {4}(?:public|internal)\s+(?:async\s+)?[\w<>[\],.?\s]+?\s+\w+\s*\(/

/** Removes string literals so brackets inside them are not counted. */
const withoutStrings = (text) => text.replace(/"(?:[^"\\]|\\.)*"/g, '""')

/**
 * Normalises a route template and an OpenAPI path to the same form: lower case, a leading slash,
 * no trailing slash, and route constraints (`{id:int}`), optional markers (`{id?}`) and catch-all
 * stars (`{**path}`) removed.
 *
 * @param {string} path
 * @returns {string}
 */
export const normalisePath = (path) => {
  const cleaned = path
    .replace(/\{\*{0,2}([\w-]+)(?::[^}]*)?\??\}/g, '{$1}')
    .replace(/\/+/g, '/')
    .toLowerCase()
    .replace(/\/$/, '')
  return cleaned.startsWith('/') ? cleaned : `/${cleaned}`
}

/** Reads the authorization facts from a block of attribute text. */
const readAttributes = (text) => {
  const facts = {}
  if (/\[AllowAnonymous\]/.test(text)) { facts.policy = 'anonymous' }
  const policy = /\[Authorize\(\s*Policy\s*=\s*Policies\.(\w+)\s*\)\]/.exec(text)
  if (policy) { facts.policy = policy[1].toLowerCase() }
  else if (/\[Authorize\]/.test(text)) { facts.policy = 'member' }
  if (/\[BackchannelKey\]/.test(text)) { facts.policy = 'internal' }

  const permissions = [...text.matchAll(/RequiresClientPermission\(\s*ClientPermission\.(\w+)/g)].map((m) => m[1])
  if (permissions.length > 0) { facts.permissions = permissions }

  const limit = /\[EnableRateLimiting\(\s*RateLimitPolicies\.(\w+)\s*\)\]/.exec(text)
  if (limit) { facts.rateLimit = limit[1] }
  if (/\[DisableRateLimiting\]/.test(text)) { facts.rateLimit = 'none' }

  if (/LocalModeOnlyFilter/.test(text)) { facts.localOnly = true }
  return facts
}

/**
 * Scans a controllers directory.
 *
 * @param {string} dir Absolute path of `API/Controllers`.
 * @returns {Map<string, { policy?: string, permissions?: string[], rateLimit?: string, localOnly?: boolean }>}
 */
export const scanControllers = (dir) => {
  const operations = new Map()

  for (const file of readdirSync(dir).filter((name) => name.endsWith('.cs'))) {
    const lines = readFileSync(join(dir, file), 'utf8').split('\n')
    const classIndex = lines.findIndex((line) => CLASS_DECLARATION.test(line))
    if (classIndex < 0) { continue }
    const controller = CLASS_DECLARATION.exec(lines[classIndex])[1].replace(/Controller$/, '').toLowerCase()

    let classAttributes = ''
    for (let i = classIndex - 1; i >= 0; i -= 1) {
      const trimmed = lines[i].trim()
      if (trimmed.startsWith('[')) { classAttributes = `${trimmed} ${classAttributes}` }
      else if (trimmed !== '' && !trimmed.startsWith('//')) { break }
    }
    const classFacts = readAttributes(classAttributes)
    const classRoute = /\[Route\("([^"]*)"\)\]/.exec(classAttributes)?.[1]?.replace(/\[controller\]/gi, controller) ?? ''

    let pending = ''
    let depth = 0
    for (let i = classIndex + 1; i < lines.length; i += 1) {
      const trimmed = lines[i].trim()
      if (depth === 0 && (trimmed === '' || trimmed.startsWith('//'))) { continue }

      if (depth > 0 || trimmed.startsWith('[')) {
        pending += ` ${trimmed}`
        const stripped = withoutStrings(trimmed)
        depth += (stripped.match(/\[/g) ?? []).length - (stripped.match(/\]/g) ?? []).length
        continue
      }

      if (METHOD_DECLARATION.test(lines[i])) {
        // A catch-all action stacks one attribute per verb on the same method.
        for (const http of pending.matchAll(METHOD_ATTRIBUTE)) {
          const template = http[2] ?? ''
          const route = template.startsWith('/') || template.startsWith('~/')
            ? template.replace(/^~?\//, '')
            : [classRoute, template].filter(Boolean).join('/')
          operations.set(`${http[1].toUpperCase()} ${normalisePath(route)}`, { ...classFacts, ...readAttributes(pending) })
        }
      }
      pending = ''
    }
  }

  return operations
}
