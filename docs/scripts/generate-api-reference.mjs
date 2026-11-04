/**
 * Turns the OpenAPI document of the API into the endpoint pages of the API reference, so the
 * reference cannot drift from the code. CI exports the document (see the "openapi" job of
 * .github/workflows/docs.yml); nothing it produces is committed.
 *
 * It writes, under docs/api-reference/:
 *   - auth/, public/, client/, admin/   one page per operation (the audience groups)
 *   - generated-sidebar.json            the sidebar groups the VitePress config merges in
 *   - _partials/rate-limit-endpoints.md the list of endpoints per rate limit policy, which the
 *                                       Rate limits page includes
 *
 * The audience of an operation is the tag of the document (Public, Auth, Client, Admin). The
 * document tags every action that is not anonymous, not under api/auth and not a client action as
 * Admin, including the few that any signed-in user may call, so the access of each action is
 * also read from the controllers' attributes (see lib/controllers.mjs) and stated on its page.
 *
 * Usage (from the repository root):
 *   node docs/scripts/generate-api-reference.mjs [path/to/openapi.json]
 *
 * Re-running replaces everything it generated. It fails when the document has an operation it
 * cannot place, so a page can never be silently missing.
 */

import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { scanControllers, normalisePath } from './lib/controllers.mjs'
import { loadErrorCatalog } from './lib/errorCatalog.mjs'
import { attribute, cell, html, plain, prose } from './lib/markup.mjs'
import { createSchemaTools } from './lib/schema.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '../..')
const apiDir = resolve(here, '../api-reference')
const controllersDir = resolve(repoRoot, 'backend/src/Innovayse.Hostpanel.API/Controllers')
const openapiPath = process.argv[2] ? resolve(process.argv[2]) : resolve(repoRoot, 'openapi.json')

if (!existsSync(openapiPath)) {
  console.error(`openapi.json not found at ${openapiPath}`)
  console.error('CI exports it; to make one locally, run the "Export the OpenAPI document" step of .github/workflows/docs.yml.')
  process.exit(1)
}

const spec = JSON.parse(readFileSync(openapiPath, 'utf8'))
const { resolve: resolveSchema, typeLabel, fieldRows, example, describe, enumNote } = createSchemaTools(spec)
const access = scanControllers(controllersDir)
const errors = loadErrorCatalog()

/** The audiences, in the order the sidebar lists them, with the sidebar title of each. */
const AUDIENCES = [
  { key: 'Auth', dir: 'auth', title: 'Auth endpoints' },
  { key: 'Public', dir: 'public', title: 'Public endpoints' },
  { key: 'Client', dir: 'client', title: 'Client endpoints' },
  { key: 'Admin', dir: 'admin', title: 'Admin endpoints' },
]

const METHODS = ['get', 'post', 'put', 'patch', 'delete']

/** Hosts used in the examples: the admin panel's host for staff routes, the portal's otherwise. */
const ADMIN_HOST = 'https://admin.example.com'
const PORTAL_HOST = 'https://panel.example.com'

/** What each rate limit policy is for, in the words the Rate limits page uses. */
const RATE_LIMIT_POLICY_ORDER = ['auth', 'upstream', 'strict', 'ssh-keys', 'concurrent']
const RATE_LIMIT_NAMES = { Auth: 'auth', Upstream: 'upstream', Strict: 'strict', SshKeys: 'ssh-keys', Concurrent: 'concurrent' }

/** The error code shown as the example body of each failing status. */
const EXAMPLE_ERROR_CODE = {
  400: 'VALIDATION_ERROR',
  401: 'UNAUTHORIZED',
  402: 'PLUGIN_STORE_QUOTA_EXCEEDED',
  403: 'FORBIDDEN',
  404: 'NOT_FOUND',
  409: 'CONFLICT',
  413: 'PLUGIN_REQUEST_TOO_LARGE',
  422: 'TRANSACTION_AMOUNT_REQUIRED',
  423: 'SIGN_IN_ACCOUNT_LOCKED',
  429: 'HOSTPANEL_RATE_LIMITED',
  500: 'INTERNAL_ERROR',
  502: 'DOMAIN_REGISTRATION_UPSTREAM_FAILED',
  503: 'NO_ELIGIBLE_SERVER',
  504: 'PLUGIN_TIMEOUT',
}

/** Builds the example body of a failing status from the error catalog. */
const errorExample = (status) => {
  const entry = errors.byCode.get(EXAMPLE_ERROR_CODE[status])
  if (!entry) { return null }
  const body = { type: entry.code, title: entry.title, status, detail: entry.sentence, errors: null }
  if (entry.code === 'VALIDATION_ERROR') { body.errors = { email: ['An email address is required.'] } }
  return body
}

// --- Operations ---------------------------------------------------------------------------------

const operations = []
for (const [path, item] of Object.entries(spec.paths ?? {})) {
  for (const method of METHODS) {
    const op = item[method]
    if (!op) { continue }
    const facts = access.get(`${method.toUpperCase()} ${normalisePath(path)}`)
    const tag = op.tags?.[0]
    if (!AUDIENCES.some((audience) => audience.key === tag)) {
      console.error(`${method.toUpperCase()} ${path} has no known tag (${op.tags?.join(', ') ?? 'none'})`)
      process.exit(1)
    }

    // A tag of Admin on an action any signed-in user may call is the document's fallback, not a
    // fact. Follow the audience the routes belong to: api/me/* is for clients, the rest is
    // shared reference data.
    let audience = tag
    if (tag === 'Admin' && facts?.policy === 'member') {
      audience = path.startsWith('/api/me/') ? 'Client' : 'Public'
    }

    operations.push({ path, method: method.toUpperCase(), op, facts, tag, audience })
  }
}

const slugs = new Set()
const slugFor = ({ path, method }) => {
  const base = `${method.toLowerCase()}-${path.replace(/[{}]/g, '').replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-|-$/g, '').toLowerCase()}`
  let slug = base
  for (let n = 2; slugs.has(slug); n += 1) { slug = `${base}-${n}` }
  slugs.add(slug)
  return slug
}

/**
 * Splits a summary into its first sentence, used as the title, and the rest. A full stop that
 * follows an abbreviation such as "e.g" does not end the sentence.
 *
 * @param {string | undefined} summary
 * @returns {{ title: string, rest: string }}
 */
const splitSummary = (summary) => {
  const text = (summary ?? '').replace(/\s+/g, ' ').trim()
  const pattern = /\.(?:\s+|$)/g
  let match
  while ((match = pattern.exec(text)) !== null) {
    if (!/(?:\be\.g|\bi\.e|\betc)$/i.test(text.slice(0, match.index))) {
      return { title: text.slice(0, match.index), rest: text.slice(match.index + match[0].length).trim() }
    }
  }
  return { title: text.replace(/\.$/, ''), rest: '' }
}

/** Shortens a title for the sidebar at a word boundary. */
const shorten = (text, max = 80) => {
  if (text.length <= max) { return text }
  const cut = text.slice(0, max).replace(/\s+\S*$/, '')
  return `${cut}...`
}

/** The area an operation is listed under in the sidebar: the controller route it belongs to. */
const areaOf = ({ path, audience }) => {
  if (audience === 'Auth') { return 'Sign-in and session' }
  const segments = path.split('/').filter(Boolean)
  if (segments[0] === 'api') { segments.shift() }
  if (segments.length === 0) { return 'health' }
  if (segments[0] === 'reports' || (segments[0] === 'admin' && segments[1] === 'reports')) { return 'reports' }
  if (['admin', 'me', 'public', 'client'].includes(segments[0]) && segments[1] && !segments[1].startsWith('{')) {
    return segments[1]
  }
  return segments[0]
}

const areaTitle = (area) => {
  const words = area.replace(/[-_]/g, ' ')
  return words.charAt(0).toUpperCase() + words.slice(1)
}

// --- Page pieces --------------------------------------------------------------------------------

/** A SettingTable for a list of rows, or null when there are none. */
const settingTable = (rows) =>
  rows.length === 0 ? null : `<SettingTable :settings="${attribute(rows)}" />`

/** Rows for the path or query parameters of an operation. */
const parameterRows = (parameters) =>
  parameters.map((parameter) => {
    const required = parameter.required ? 'Required.' : ''
    const description = [required, describe(parameter), enumNote(parameter.schema)].filter(Boolean).join(' ')
    const { schema } = resolveSchema(parameter.schema)
    return {
      name: parameter.name,
      type: typeLabel(parameter.schema),
      default: schema.default === undefined ? 'None' : String(schema.default),
      description,
    }
  })

/** The JSON request body schema of an operation, or null. */
const jsonBody = (op) => op.requestBody?.content?.['application/json']?.schema ?? null

/** The multipart request body schema of an operation, or null. */
const multipartBody = (op) => op.requestBody?.content?.['multipart/form-data']?.schema ?? null

/** One sentence about who may call an operation. */
const accessLine = ({ facts }) => {
  if (!facts?.policy) { return null }
  const permissions = facts.permissions?.map((name) => `\`${name}\``).join(', ')
  const lines = {
    admin: 'Requires the Admin role (staff).',
    client: `Requires a signed-in client user acting on their own account${permissions ? `, with the ${permissions} client permission` : ''}.`,
    member: 'Any signed-in user, whatever their role.',
    anonymous: 'No sign-in needed.',
    internal: 'Server to server. Requires the shared key in the `X-Logout-Key` header; it is not meant to be called from a browser or a script of your own.',
  }
  return lines[facts.policy] ?? null
}

/** The request path with example values in place of its parameters. */
const examplePath = (path, parameters) => {
  const values = Object.fromEntries(
    parameters.filter((parameter) => parameter.in === 'path').map((parameter) => [parameter.name, example(parameter.schema)]),
  )
  return path.replace(/\{(\w+)\}/g, (_, name) => encodeURIComponent(String(values[name] ?? 'value')))
}

/** The query string of the example: required parameters, plus the paging pair when it has one. */
const exampleQuery = (parameters) => {
  const chosen = parameters.filter((parameter) =>
    parameter.in === 'query' && (parameter.required || ['Page', 'PageSize'].includes(parameter.name)))
  if (chosen.length === 0) { return '' }
  return `?${chosen
    .map((parameter) => `${parameter.name}=${parameter.name === 'PageSize' ? 20 : encodeURIComponent(String(example(parameter.schema)))}`)
    .join('&')}`
}

/** The curl and fetch examples of an operation. */
const requestExamples = ({ path, method, op, facts, audience }) => {
  const host = audience === 'Admin' ? ADMIN_HOST : PORTAL_HOST
  const parameters = op.parameters ?? []
  const url = `${host}${examplePath(path, parameters)}${exampleQuery(parameters)}`
  const signedIn = facts?.policy ? facts.policy !== 'anonymous' && facts.policy !== 'internal' : audience !== 'Public'

  const headers = []
  if (signedIn) { headers.push(['Authorization', 'Bearer <token>']) }
  if (facts?.policy === 'internal') { headers.push(['X-Logout-Key', '<key>']) }

  const json = jsonBody(op)
  const multipart = multipartBody(op)
  const body = json ? JSON.stringify(example(json), null, 2) : null
  if (json) { headers.push(['Content-Type', 'application/json']) }

  const curl = [`curl -X ${method} '${url}'`]
  for (const [name, value] of headers) { curl.push(`  -H '${name}: ${value}'`) }
  if (body) { curl.push(`  -d '${body.replace(/'/g, "'\\''")}'`) }
  if (multipart) {
    for (const [field, property] of Object.entries(resolveSchema(multipart).schema.properties ?? {})) {
      const binary = resolveSchema(property).schema.format === 'binary'
      curl.push(`  -F '${field}=${binary ? '@./file.png' : example(property)}'`)
    }
  }

  const options = [`  method: '${method}'`]
  if (headers.length > 0) {
    options.push(`  headers: {\n${headers
      .map(([name, value]) => `    ${/^[A-Za-z]+$/.test(name) ? name : `'${name}'`}: ${name === 'Authorization' ? '`Bearer ${token}`' : `'${value}'`},`)
      .join('\n')}\n  }`)
  }
  if (body) { options.push(`  body: JSON.stringify(${body.replace(/\n/g, '\n  ')})`) }
  let fetchSource = `const response = await fetch('${url}', {\n${options.join(',\n')},\n})\n`
  if (multipart) {
    fetchSource = `const form = new FormData()\n${Object.keys(resolveSchema(multipart).schema.properties ?? {})
      .map((field) => `form.append('${field}', file)`)
      .join('\n')}\n\nconst response = await fetch('${url}', {\n${options.join(',\n')},\n  body: form,\n})\n`
  }
  fetchSource += 'const data = await response.json()'

  return { curl: curl.join(' \\\n'), fetch: fetchSource }
}

/** The response examples of an operation: a body per declared status. */
const responseExamples = (op) => {
  const out = []
  for (const [status, response] of Object.entries(op.responses ?? {})) {
    const code = Number(status)
    if (code >= 200 && code < 300) {
      const schema = response.content?.['application/json']?.schema
      out.push({ status, body: schema ? JSON.stringify(example(schema), null, 2) : null })
    } else {
      const sample = errorExample(code)
      out.push({ status, body: sample ? JSON.stringify(sample, null, 2) : null })
    }
  }
  return out
}

/** The label shown next to the title, from the access and mode facts. */
const labels = ({ facts }) => {
  const out = []
  if (facts?.policy === 'admin') { out.push('<Label type="admin" />') }
  if (facts?.localOnly) { out.push('<Label type="local" />') }
  return out
}

/** The whole Markdown page of an operation. */
const operationPage = (entry) => {
  const { path, method, op, facts } = entry
  const split = splitSummary(op.summary)
  const title = split.title || `${method} ${path}`
  const rest = split.rest
  const intro = [rest, op.description ?? ''].filter(Boolean).join('\n\n')

  const parameters = op.parameters ?? []
  const pathRows = parameterRows(parameters.filter((parameter) => parameter.in === 'path'))
  const queryRows = parameterRows(parameters.filter((parameter) => parameter.in === 'query'))
  const json = jsonBody(op)
  const multipart = multipartBody(op)
  const bodyRows = json ? fieldRows(json) : multipart ? fieldRows(multipart) : []
  const bodyType = json ? typeLabel(json) : null

  const accessSentence = accessLine(entry)
  const limit = facts?.rateLimit && facts.rateLimit !== 'none' ? RATE_LIMIT_NAMES[facts.rateLimit] : null
  const examples = requestExamples(entry)
  const responses = responseExamples(op)

  const main = [
    `<div class="api-signature"><span class="api-method api-method-${method.toLowerCase()}">${method}</span> <code>${html(path)}</code></div>`,
  ]
  if (intro) { main.push('', prose(intro)) }
  if (accessSentence) { main.push('', `**Access.** ${accessSentence}${facts?.localOnly ? ' Answers 404 when the deployment uses an external sign-on service.' : ''}`) }
  if (limit) { main.push('', `**Rate limit.** Draws from the \`${limit}\` policy as well as the global one. See [Rate limits](../rate-limits).`) }

  if (pathRows.length > 0) { main.push('', '## Path parameters', '', settingTable(pathRows)) }
  if (queryRows.length > 0) { main.push('', '## Query parameters', '', settingTable(queryRows)) }
  if (bodyRows.length > 0) {
    main.push('', json ? '## Request body' : '## Request body (multipart form)', '')
    if (bodyType && !bodyType.includes('object')) { main.push(`Schema: \`${html(bodyType)}\`.`, '') }
    main.push(settingTable(bodyRows))
  } else if (json) {
    main.push('', '## Request body', '', `A JSON body of type \`${html(bodyType)}\`.`)
  }

  main.push('', '## Responses', '', '| Status | Meaning | Body |', '|--------|---------|------|')
  for (const [status, response] of Object.entries(op.responses ?? {})) {
    const content = response.content?.['application/json']?.schema ?? response.content?.['application/problem+json']?.schema
    const body = content ? `\`${html(typeLabel(content))}\`` : 'Empty'
    main.push(`| ${status} | ${cell(response.description ?? '')} | ${body} |`)
  }
  main.push('', 'Failures answer with the `ErrorResponse` body; its `type` is the error code. See [Errors](../errors).')

  const aside = [
    '### Request', '',
    '::: code-group', '',
    '```bash [curl]', examples.curl, '```', '',
    '```js [JavaScript]', examples.fetch, '```', '',
    ':::', '',
    '### Responses', '',
  ]
  const withBody = responses.filter((response) => response.body)
  if (withBody.length > 0) {
    aside.push('::: code-group', '')
    for (const response of withBody) { aside.push(`\`\`\`json [${response.status}]`, response.body, '```', '') }
    aside.push(':::')
    if (withBody.some((response) => Number(response.status) >= 400)) {
      aside.push('', 'The failure bodies are examples: the `type` and `detail` carry the specific error of the call.')
    }
  }
  const empty = responses.filter((response) => !response.body).map((response) => response.status)
  if (empty.length > 0) { aside.push('', `Empty body: ${empty.join(', ')}.`) }

  const description = `${plain(title)}.`.slice(0, 200)
  return [
    '---',
    `title: ${JSON.stringify(title.replace(/`/g, ''))}`,
    `description: ${JSON.stringify(description)}`,
    'pageClass: api-endpoint-page',
    'aside: false',
    'outline: false',
    '---',
    '',
    `# ${cell(title)}${labels(entry).length ? ` ${labels(entry).join(' ')}` : ''}`,
    '',
    '<div class="api-endpoint">',
    '<div class="api-main">',
    '',
    ...main,
    '',
    '</div>',
    '<div class="api-aside">',
    '',
    ...aside,
    '',
    '</div>',
    '</div>',
    '',
  ].join('\n')
}

// --- Write --------------------------------------------------------------------------------------

for (const audience of AUDIENCES) { rmSync(resolve(apiDir, audience.dir), { recursive: true, force: true }) }

const sidebar = []
let generated = 0

for (const audience of AUDIENCES) {
  const entries = operations
    .filter((entry) => entry.audience === audience.key)
    .sort((a, b) => a.path.localeCompare(b.path) || METHODS.indexOf(a.method.toLowerCase()) - METHODS.indexOf(b.method.toLowerCase()))
  if (entries.length === 0) { continue }

  const dir = resolve(apiDir, audience.dir)
  mkdirSync(dir, { recursive: true })

  const areas = new Map()
  for (const entry of entries) {
    const slug = slugFor(entry)
    writeFileSync(resolve(dir, `${slug}.md`), operationPage(entry))
    generated += 1

    const area = areaOf(entry)
    if (!areas.has(area)) { areas.set(area, []) }
    const title = shorten(plain(splitSummary(entry.op.summary).title || `${entry.method} ${entry.path}`))
    areas.get(area).push({
      text: `<span class="api-badge api-badge-${entry.method.toLowerCase()}">${entry.method}</span> ${html(title)}`,
      link: `/api-reference/${audience.dir}/${slug}`,
    })
  }

  sidebar.push({
    text: audience.title,
    collapsed: true,
    items: [...areas.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([area, items]) => ({ text: areaTitle(area), collapsed: true, items })),
  })
  console.log(`${audience.key}: ${entries.length} operations in ${areas.size} areas`)
}

// Count what is on disk, not what the loop wrote: every operation of the document must have a page.
const onDisk = AUDIENCES.reduce(
  (total, audience) => total + (existsSync(resolve(apiDir, audience.dir)) ? readdirSync(resolve(apiDir, audience.dir)).filter((name) => name.endsWith('.md')).length : 0),
  0,
)
if (generated !== operations.length || onDisk !== operations.length) {
  console.error(`The document has ${operations.length} operations, ${generated} pages were written and ${onDisk} are on disk`)
  process.exit(1)
}

writeFileSync(resolve(apiDir, 'generated-sidebar.json'), JSON.stringify(sidebar, null, 2))

// The endpoints of each rate limit policy, from the controllers' attributes.
const byPolicy = new Map()
for (const { path, method, facts } of operations) {
  const name = facts?.rateLimit && facts.rateLimit !== 'none' ? RATE_LIMIT_NAMES[facts.rateLimit] : null
  if (!name) { continue }
  if (!byPolicy.has(name)) { byPolicy.set(name, []) }
  byPolicy.get(name).push(`${method} ${path}`)
}
const limitLines = []
for (const name of RATE_LIMIT_POLICY_ORDER) {
  const list = byPolicy.get(name)
  if (!list) { continue }
  limitLines.push(`### \`${name}\``, '', ...list.sort().map((entry) => `- \`${entry}\``), '')
}
mkdirSync(resolve(apiDir, '_partials'), { recursive: true })
writeFileSync(resolve(apiDir, '_partials/rate-limit-endpoints.md'), limitLines.join('\n'))

console.log(`Generated ${generated} endpoint pages from ${openapiPath}`)
