/**
 * The field types of a plugin form block: the same types as plugin settings.
 *
 * @module types/pluginfieldtype
 */

/** A field type of a plugin form. */
export enum PluginFieldType {
  /** One line of text. */
  Text = 'text',
  /** Several lines of text. */
  Textarea = 'textarea',
  /** A number. */
  Number = 'number',
  /** An http(s) address. */
  Url = 'url',
  /** A value that is not shown back. */
  Secret = 'secret',
  /** A choice from the field's options. */
  Select = 'select',
  /** On or off. */
  Boolean = 'boolean'
}
