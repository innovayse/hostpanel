/**
 * Row identities for the report tables whose rows carry no id of their own: the key is built from
 * the fields that together tell two rows apart, so a table never keys a row by its position.
 */
import type { ReportStatementLine } from '@/types/reportclientstatement'
import type { ReportDomainRenewalRow } from '@/types/reportdomainrenewal'
import type { ReportVatRow } from '@/types/reportvat'

/** Separator between the parts of a composite key; no part contains it. */
const KEY_SEPARATOR = '|'

/**
 * Key of a client statement entry: its kind, day, invoice, text and the running balance after it.
 *
 * @param line - The statement entry.
 * @returns The key.
 */
export const statementLineKey = (line: ReportStatementLine): string =>
  [line.type, line.date, line.invoiceId ?? '', line.description ?? '', line.currency, line.balance].join(KEY_SEPARATOR)

/**
 * Key of a sent renewal reminder: the domain, the reminder kind and when it was sent.
 *
 * @param row - The reminder row.
 * @returns The key.
 */
export const renewalEmailKey = (row: ReportDomainRenewalRow): string =>
  [row.domain, row.reminder, row.sentAt].join(KEY_SEPARATOR)

/**
 * Key of a VAT row: the member state, the rate and the currency, which is how the report groups rows.
 *
 * @param row - The VAT row.
 * @returns The key.
 */
export const vatRowKey = (row: ReportVatRow): string => [row.countryCode, row.vatRate, row.currency].join(KEY_SEPARATOR)
