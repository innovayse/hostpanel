/**
 * The input kinds of an extra domain field, as the API writes them (by name).
 *
 * @module types/domainfieldtype
 */
export enum DomainFieldType {
  /** Single-line plain text. */
  Text = 'Text',
  /** Multi-line text. */
  Textarea = 'Textarea',
  /** One of a fixed list of options. */
  Select = 'Select',
  /** A calendar date, written `yyyy-MM-dd`. */
  Date = 'Date',
  /** A number with an optional sign and decimal point. */
  Number = 'Number',
  /** An absolute web address. */
  Url = 'Url'
}
