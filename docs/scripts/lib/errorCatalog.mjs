/**
 * Reads the error codes of the API from the backend sources: the codes and their groups, the
 * English sentence of each, and the HTTP status and title StatusMapping answers it with.
 *
 * Shared by the error code table and by the example error bodies on the endpoint pages, so both
 * say the same thing. It fails, instead of guessing, when a source no longer has the shape it
 * reads: a code with no sentence, a sentence with no code, or a StatusMapping arm it does not
 * understand.
 */

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const backend = resolve(here, '../../../backend/src')

/** The sentence key of each code the shared edge answers with on its own (ValidationMessages.resx). */
const EDGE_SENTENCE_KEYS = {
  VALIDATION_ERROR: 'ValidationErrorsOccurred',
  UNAUTHORIZED: 'UnauthorizedDetail',
  FORBIDDEN: 'ForbiddenDetail',
  CSRF_HEADER_MISSING: 'CsrfHeaderMissingDetail',
  NOT_FOUND: 'ResourceNotFoundDetail',
  BUSINESS_RULE_VIOLATION: 'BusinessRuleViolationDetail',
  CONFLICT: 'ConcurrencyConflictDetail',
  INVALID_REQUEST: 'InvalidRequestBodyDetail',
  INTERNAL_ERROR: 'UnexpectedErrorOccurred',
}

/** The title key of each code the shared edge answers with on its own (ValidationMessages.resx). */
const EDGE_TITLE_KEYS = {
  VALIDATION_ERROR: 'ValidationFailed',
  UNAUTHORIZED: 'Unauthorized',
  FORBIDDEN: 'Forbidden',
  CSRF_HEADER_MISSING: 'Forbidden',
  NOT_FOUND: 'ResourceNotFound',
  BUSINESS_RULE_VIOLATION: 'BusinessRuleViolation',
  CONFLICT: 'ConcurrencyConflict',
  INVALID_REQUEST: 'InvalidRequestBody',
  INTERNAL_ERROR: 'InternalServerError',
}

const fail = (message) => {
  console.error(`error catalog: ${message}`)
  process.exit(1)
}

const source = (relative) => readFileSync(resolve(backend, relative), 'utf8')

const decodeXml = (text) =>
  text
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(parseInt(dec, 10)))
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')

/** Reads every `<data name><value>` pair of a .resx into a Map. */
const readResx = (xml) => {
  const entries = new Map()
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g
  let match
  while ((match = re.exec(xml)) !== null) {
    entries.set(match[1], decodeXml(match[2]).trim())
  }
  return entries
}

/**
 * Loads the catalog.
 *
 * @returns {{
 *   edge: { code: string, status: number, title: string, sentence: string }[],
 *   groups: Map<string, { code: string, status: number, title: string, sentence: string }[]>,
 *   byCode: Map<string, { code: string, status: number, title: string, sentence: string }>,
 *   total: number,
 * }}
 */
export const loadErrorCatalog = () => {
  const hostpanelCodesCs = source('Innovayse.Hostpanel.Application/Common/Errors/HostpanelErrorCodes.cs')
  const sharedCodesCs = source('Shared/Constants/SharedErrorCodes.cs')
  const statusMappingCs = source('Shared/Controllers/StatusMapping.cs')
  const sentences = readResx(source('Innovayse.Hostpanel.Application/Resources/ErrorMessages.resx'))
  const edgeSentences = readResx(source('Shared/Resources/ValidationMessages.resx'))

  // SharedErrorCodes.cs: member -> wire code, and the status each member's summary says it answers.
  const sharedByMember = new Map()
  const sharedStatedStatus = new Map()
  {
    const re = /\/\/\/ <summary>([\s\S]*?)<\/summary>\s*public const string (\w+) = "([A-Z0-9_]+)";/g
    let match
    while ((match = re.exec(sharedCodesCs)) !== null) {
      const [, summary, member, code] = match
      sharedByMember.set(member, code)
      const status = /answered\s+(\d{3})/.exec(summary)
      if (status) { sharedStatedStatus.set(code, Number(status[1])) }
    }
  }
  if (sharedByMember.size === 0) { fail('no codes found in SharedErrorCodes.cs') }

  // StatusMapping.cs: the switch, read arm by arm.
  const start = statusMappingCs.indexOf('errorCode switch')
  if (start < 0) { fail('StatusMapping.cs has no "errorCode switch"') }
  const body = statusMappingCs
    .slice(start)
    .split('\n')
    .filter((line) => !/^\s*\/\//.test(line))
    .join('\n')

  const rawArms = []
  // Arms are cut at their "=> new(status, title)," terminators instead of balancing the switch.
  const cut = /=>\s*new\(\s*(\d{3})\s*,\s*"([^"]*)"\s*\)\s*,/g
  let last = body.indexOf('{') + 1
  let cutMatch
  while ((cutMatch = cut.exec(body)) !== null) {
    rawArms.push({ pattern: body.slice(last, cutMatch.index).trim(), status: Number(cutMatch[1]), title: cutMatch[2] })
    last = cutMatch.index + cutMatch[0].length
  }

  const arms = rawArms.map(({ pattern, status, title }) => {
    if (pattern === '_') { return { exact: [], contains: [], endsWith: [], isDefault: true, status, title } }

    if (pattern.startsWith('var code when')) {
      const tests = [...pattern.matchAll(/code\.(Contains|EndsWith)\(\s*"([^"]+)"/g)]
      const residue = pattern
        .replace(/var code when|code\.(Contains|EndsWith)\(\s*"[^"]+"\s*,\s*StringComparison\.Ordinal\s*\)/g, '')
        .trim()
      if (tests.length === 0 || residue !== '') { fail(`unrecognised StatusMapping arm: ${pattern}`) }
      return {
        exact: [],
        contains: tests.filter((t) => t[1] === 'Contains').map((t) => t[2]),
        endsWith: tests.filter((t) => t[1] === 'EndsWith').map((t) => t[2]),
        isDefault: false,
        status,
        title,
      }
    }

    const exact = pattern.split(/\bor\b/).map((part) => {
      const piece = part.trim()
      const literal = /^"([A-Z0-9_]+)"$/.exec(piece)
      if (literal) { return literal[1] }
      const shared = /^SharedErrorCodes\.(\w+)$/.exec(piece)
      if (shared && sharedByMember.has(shared[1])) { return sharedByMember.get(shared[1]) }
      return fail(`unrecognised StatusMapping arm: ${pattern}`)
    })
    return { exact, contains: [], endsWith: [], isDefault: false, status, title }
  })
  if (!arms.some((arm) => arm.isDefault)) { fail('StatusMapping.cs has no default arm') }

  const mappingOf = (code) => {
    for (const arm of arms) {
      if (arm.isDefault || arm.exact.includes(code)
        || arm.contains.some((token) => code.includes(token))
        || arm.endsWith.some((token) => code.endsWith(token))) {
        return { status: arm.status, title: arm.title }
      }
    }
    return fail(`no StatusMapping arm matched ${code}`)
  }

  // HostpanelErrorCodes.cs: names in source order, with the heading comment above each.
  const hostpanelCodes = []
  {
    let group = 'General'
    let previousWasHeading = false
    for (const line of hostpanelCodesCs.split('\n')) {
      const heading = /^ {4}\/\/ (?!\/)(.+)$/.exec(line)
      if (heading) {
        // A heading may wrap over several comment lines; only its first line names the group.
        if (!previousWasHeading) { group = heading[1].replace(/\.\s.*$/, '').replace(/\.$/, '').trim() }
        previousWasHeading = true
        continue
      }
      previousWasHeading = false
      const constant = /^ {4}public const string ([A-Z][A-Z0-9_]*)\b/.exec(line)
      if (constant) { hostpanelCodes.push({ code: constant[1], group }) }
    }
  }
  if (hostpanelCodes.length === 0) { fail('no codes found in HostpanelErrorCodes.cs') }

  // The registry and the sentences must be the same set; the backend's ResourceCoverageTests
  // enforces it, and this fails the docs build the same way if it ever slips.
  const registered = new Set(hostpanelCodes.map((entry) => entry.code))
  const missingSentence = [...registered].filter((code) => !sentences.has(code))
  const missingCode = [...sentences.keys()].filter((key) => !registered.has(key))
  if (missingSentence.length || missingCode.length) {
    fail(
      `HostpanelErrorCodes.cs and ErrorMessages.resx disagree. No sentence: ${missingSentence.join(', ') || 'none'}. ` +
      `No code: ${missingCode.join(', ') || 'none'}.`,
    )
  }

  const byCode = new Map()

  // The codes the shared edge produces on its own, before or around any handler. The three that
  // are also in the registry (unexpected, rate limited, demo) are listed with the registry's groups.
  const edge = []
  for (const code of sharedByMember.values()) {
    if (registered.has(code)) { continue }
    const sentenceKey = EDGE_SENTENCE_KEYS[code]
    const titleKey = EDGE_TITLE_KEYS[code]
    if (!sentenceKey || !titleKey) { fail(`no sentence key known for the shared code ${code}`) }
    const sentence = edgeSentences.get(sentenceKey)
    const title = edgeSentences.get(titleKey)
    if (!sentence || !title) { fail(`ValidationMessages.resx has no "${sentenceKey}" or "${titleKey}" for ${code}`) }
    const entry = { code, status: sharedStatedStatus.get(code) ?? mappingOf(code).status, title, sentence }
    edge.push(entry)
    byCode.set(code, entry)
  }

  const groups = new Map()
  for (const { code, group } of hostpanelCodes) {
    const { status, title } = mappingOf(code)
    const entry = { code, status, title, sentence: sentences.get(code) }
    if (!groups.has(group)) { groups.set(group, []) }
    groups.get(group).push(entry)
    byCode.set(code, entry)
  }

  return { edge, groups, byCode, total: edge.length + hostpanelCodes.length }
}
