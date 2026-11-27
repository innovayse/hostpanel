/**
 * Helpers for the extra domain fields a registrar plugin asks for: the kind of registrant, what is sent, what is missing
 * and which refusal belongs to which field. Nothing here names a zone or a country: the fields come from the plugin.
 *
 * @module utils/domainFields
 */

import type { DomainField } from '~/types/domainfield'
import { DomainFieldHolder } from '~/types/domainfieldholder'

/**
 * Tells the kind of registrant from the contact: one with an organisation is an organisation, any other a person. The
 * backend decides the same way from the registrant contact, so the form asks for the same fields the order will be checked
 * against.
 *
 * @param organization - The organisation on the contact; null, undefined or blank for none.
 * @returns The holder the fields of a zone are chosen for.
 */
export const holderOf = (organization: string | null | undefined): DomainFieldHolder =>
  organization && organization.trim() ? DomainFieldHolder.Organization : DomainFieldHolder.Individual

/**
 * Leaves out the blank values, so an optional field the visitor did not fill in is neither sent nor stored.
 *
 * @param values - The values by field key as typed.
 * @returns The values that hold something.
 */
export const compactAttributes = (values: Readonly<Record<string, string>>): Record<string, string> =>
  Object.fromEntries(Object.entries(values).filter(([, value]) => value.trim() !== ''))

/**
 * Lists the required fields that have no value yet.
 *
 * @param fields - The fields the registrar asks for.
 * @param values - The values by field key as typed.
 * @returns The keys of the required fields left empty, in the fields' order.
 */
export const missingRequiredKeys = (fields: readonly DomainField[], values: Readonly<Record<string, string>>): string[] =>
  fields.filter(field => field.required && !(values[field.key] ?? '').trim()).map(field => field.key)

/**
 * Keeps only the values of the fields that apply, so a value typed for a field that no longer applies (the visitor changed the
 * domain, or the kind of registrant) is never sent as an unknown key.
 *
 * @param fields - The fields that apply now.
 * @param values - The values by field key as typed.
 * @returns The compacted values of those fields.
 */
export const attributesFor = (fields: readonly DomainField[], values: Readonly<Record<string, string>>): Record<string, string> => {
  const keys = new Set(fields.map(field => field.key))
  return compactAttributes(Object.fromEntries(Object.entries(values).filter(([key]) => keys.has(key))))
}

/**
 * Finds the refusal that belongs to one field. The error reader writes each segment of a dotted key with a lower case first
 * letter, so a key is looked up as declared and then in that form.
 *
 * @param errors - Field key to message, as `apiFieldErrors` reads them.
 * @param key - The field key as the plugin declares it.
 * @returns The sentence, or undefined when the field was not refused.
 */
export const fieldErrorFor = (errors: Readonly<Record<string, string>>, key: string): string | undefined =>
  errors[key] ?? errors[key.split('.').map(segment => segment.charAt(0).toLowerCase() + segment.slice(1)).join('.')]
