/**
 * Helpers for the extra domain fields a registrar plugin asks for: the kind of registrant and which values are sent. Nothing
 * here names a zone or a country: the fields come from the plugin.
 */

import { type DomainField, DomainFieldHolder } from '@/types/domain'

/**
 * Tells the kind of registrant from the contact: one with an organisation is an organisation, any other a person. The
 * backend decides the same way from the registrant contact, so the form asks for the same fields the change is checked against.
 *
 * @param organization - The organisation on the contact; null, undefined or blank for none.
 * @returns The holder the fields of a zone are chosen for.
 */
export const holderOf = (organization: string | null | undefined): DomainFieldHolder =>
  organization && organization.trim() ? DomainFieldHolder.Organization : DomainFieldHolder.Individual

/**
 * Leaves out the blank values and the values of fields that do not apply, so an optional field left empty is neither sent
 * nor stored and a value typed for a field that no longer applies is never sent as an unknown key.
 *
 * @param fields - The fields that apply now.
 * @param values - The values by field key as typed.
 * @returns The values to send.
 */
export const attributesFor = (
  fields: readonly DomainField[],
  values: Readonly<Record<string, string>>,
): Record<string, string> => {
  const keys = new Set(fields.map(field => field.key))
  return Object.fromEntries(Object.entries(values).filter(([key, value]) => keys.has(key) && value.trim() !== ''))
}
