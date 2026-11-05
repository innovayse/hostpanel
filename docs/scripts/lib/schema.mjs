/**
 * Reads the schemas of an OpenAPI document into what an endpoint page shows: a readable type, the
 * rows of a fields table, and an example value generated from the schema.
 */

import { plain } from './markup.mjs'

/** Deepest level of nested objects listed in a fields table. */
const MAX_ROW_DEPTH = 3

/** Deepest level of nested objects in a generated example. */
const MAX_EXAMPLE_DEPTH = 5

/** The last dotted segment of a CLR name. */
const lastSegment = (name) => name.slice(name.lastIndexOf('.') + 1)

/**
 * Shortens a schema id (a full CLR name, with the assembly details of a generic argument) to the
 * name a reader knows: `PaginatedList<UserListItemDto>`.
 *
 * @param {string} full
 * @returns {string}
 */
export const shortName = (full) => {
  const cleaned = full.replace(/,\s*[^,[\]]+,\s*Version=[^\]]+/g, '')
  const generic = /^(.*?)`\d+\[\[(.+)\]\]$/.exec(cleaned)
  if (generic) { return `${lastSegment(generic[1])}<${generic[2].split('],[').map(lastSegment).join(', ')}>` }
  return lastSegment(cleaned)
}

/**
 * Binds the schema helpers to one OpenAPI document.
 *
 * @param {{ components?: { schemas?: Record<string, any> } }} spec
 */
export const createSchemaTools = (spec) => {
  const schemas = spec.components?.schemas ?? {}

  /** The name a `$ref` points at. */
  const refName = (ref) => decodeURIComponent(ref.slice(ref.lastIndexOf('/') + 1))

  /**
   * Follows `$ref` and single-member `allOf` wrappers to the schema that carries the shape.
   *
   * @returns {{ schema: any, name: string | null, nullable: boolean }}
   */
  const resolve = (input) => {
    let schema = input
    let name = null
    let nullable = Boolean(input?.nullable)
    for (let hops = 0; schema && hops < 8; hops += 1) {
      if (schema.$ref) {
        name = refName(schema.$ref)
        schema = schemas[name]
      } else if (Array.isArray(schema.allOf) && schema.allOf.length === 1) {
        schema = schema.allOf[0]
      } else {
        break
      }
      nullable = nullable || Boolean(schema?.nullable)
    }
    return { schema: schema ?? {}, name, nullable }
  }

  /** The alternatives of a `oneOf` schema, or an empty list when the schema is not one. */
  const variants = (input) => (Array.isArray(input?.oneOf) ? input.oneOf : [])

  /** A short, readable type for a table cell. */
  const typeLabel = (input) => {
    if (!input) { return 'object' }
    if (variants(input).length > 0) { return variants(input).map(typeLabel).join(' or ') }
    const { schema, name, nullable } = resolve(input)
    let base
    if (name && schema.enum) {
      base = `${shortName(name)} (enum)`
    } else if (name) {
      base = shortName(name)
    } else if (schema.type === 'array') {
      base = `${typeLabel(schema.items)}[]`
    } else {
      base = schema.type ?? (schema.properties ? 'object' : 'any')
      if (schema.format) { base += ` (${schema.format})` }
    }
    return nullable ? `${base} | null` : base
  }

  /** The description to show for a property: its own, else the one on the schema it refers to. */
  const describe = (property) => {
    const { schema } = resolve(property)
    return plain(property?.description ?? schema.description ?? '')
  }

  /** "One of: A, B, C." for an enum, or an empty string. */
  const enumNote = (property) => {
    const { schema } = resolve(property)
    const values = schema.enum
    if (!Array.isArray(values) || values.length === 0) { return '' }
    const shown = values.slice(0, 12).join(', ')
    return values.length > 12 ? `One of: ${shown} and ${values.length - 12} more.` : `One of: ${shown}.`
  }

  /**
   * The fields of an object schema as rows for a SettingTable, nested objects flattened with
   * dotted names (`address.city`, `items[].price`).
   *
   * @returns {{ name: string, type: string, default: string, description: string }[]}
   */
  const fieldRows = (root) => {
    const rows = []
    const walk = (input, prefix, depth, trail) => {
      const { schema, name } = resolve(input)
      if (name && trail.has(name)) { return }
      const nextTrail = name ? new Set([...trail, name]) : trail
      for (const [field, property] of Object.entries(schema.properties ?? {})) {
        const { schema: resolved } = resolve(property)
        const required = schema.required?.includes(field) ? 'Required.' : ''
        const description = [required, describe(property), enumNote(property)].filter(Boolean).join(' ')
        const fallback = resolved.default ?? property.default
        rows.push({
          name: `${prefix}${field}`,
          type: typeLabel(property),
          default: fallback === undefined ? 'None' : String(fallback),
          description,
        })
        if (depth >= MAX_ROW_DEPTH) { continue }
        if (resolved.properties) {
          walk(property, `${prefix}${field}.`, depth + 1, nextTrail)
        } else if (resolved.type === 'array' && resolve(resolved.items).schema.properties) {
          walk(resolved.items, `${prefix}${field}[].`, depth + 1, nextTrail)
        }
      }
    }
    walk(root, '', 1, new Set())
    return rows
  }

  /** An example value generated from a schema. */
  const example = (input, depth = 0, trail = new Set()) => {
    if (!input) { return null }
    const { schema, name } = resolve(input)
    if (name && trail.has(name)) { return {} }
    if (schema.example !== undefined) { return schema.example }
    if (input.example !== undefined) { return input.example }
    if (schema.default !== undefined) { return schema.default }
    if (Array.isArray(schema.enum) && schema.enum.length > 0) { return schema.enum[0] }

    const nextTrail = name ? new Set([...trail, name]) : trail
    if (schema.type === 'array') {
      return depth >= MAX_EXAMPLE_DEPTH ? [] : [example(schema.items, depth + 1, nextTrail)]
    }
    if (schema.properties) {
      if (depth >= MAX_EXAMPLE_DEPTH) { return {} }
      return Object.fromEntries(
        Object.entries(schema.properties).map(([field, property]) => [field, example(property, depth + 1, nextTrail)]),
      )
    }
    if (schema.additionalProperties && typeof schema.additionalProperties === 'object') {
      return depth >= MAX_EXAMPLE_DEPTH ? {} : { key: example(schema.additionalProperties, depth + 1, nextTrail) }
    }
    switch (schema.type) {
      case 'integer': return 1
      case 'number': return 1.5
      case 'boolean': return true
      case 'string':
        switch (schema.format) {
          case 'date-time': return '2026-01-15T12:00:00Z'
          case 'date': return '2026-01-15'
          case 'uuid': return '3fa85f64-5717-4562-b3fc-2c963f66afa6'
          case 'binary': return '<file>'
          default: return 'string'
        }
      default:
        return {}
    }
  }

  return { resolve, typeLabel, variants, fieldRows, example, describe, enumNote }
}
