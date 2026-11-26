/**
 * The one place a form field type is mapped to the global kit control that draws it. The plugin forms and the
 * integration config form both read their field's rendering here, so a field type never brings markup of its own
 * and the two forms cannot drift apart. Both maps draw a number with `UiNumberInput`. A field the API marks secret
 * is always the secret field, whatever its type says, so a value is never shown back in a plain input.
 */
import { FieldControl } from '@/types/fieldcontrol'
import type { FieldRendering } from '@/types/fieldcontrol'
import { IntegrationFieldType } from '@/types/integration'
import { PluginFieldType } from '@/types/pluginview'
import { UiInputType } from '@/utils/ui/input'

/** The rendering of each plugin form field type. */
export const PLUGIN_FIELD_RENDERINGS: Readonly<Record<PluginFieldType, FieldRendering>> = {
  [PluginFieldType.Text]: { control: FieldControl.Input },
  [PluginFieldType.Number]: { control: FieldControl.Number },
  [PluginFieldType.Url]: { control: FieldControl.Input, inputType: UiInputType.Url },
  [PluginFieldType.Textarea]: { control: FieldControl.Textarea },
  [PluginFieldType.Select]: { control: FieldControl.Select },
  [PluginFieldType.Boolean]: { control: FieldControl.Toggle },
  [PluginFieldType.Secret]: { control: FieldControl.Secret },
}

/** The rendering of each integration config field type. */
export const INTEGRATION_FIELD_RENDERINGS: Readonly<Record<IntegrationFieldType, FieldRendering>> = {
  [IntegrationFieldType.Text]: { control: FieldControl.Input },
  [IntegrationFieldType.Url]: { control: FieldControl.Input, inputType: UiInputType.Url },
  [IntegrationFieldType.Number]: { control: FieldControl.Number },
  [IntegrationFieldType.Textarea]: { control: FieldControl.Textarea },
  [IntegrationFieldType.Select]: { control: FieldControl.Select },
  [IntegrationFieldType.Toggle]: { control: FieldControl.Toggle },
  [IntegrationFieldType.Password]: { control: FieldControl.Secret },
  [IntegrationFieldType.Secret]: { control: FieldControl.Secret },
}

/**
 * The native input type of a field drawn by `UiInput`.
 *
 * @param rendering - The field's rendering.
 * @returns The rendering's input type; plain text when it names none.
 */
export const fieldInputType = (rendering: FieldRendering): UiInputType => rendering.inputType ?? UiInputType.Text

/**
 * The control that draws a field. A field the API marks secret is always the write-only secret field, whatever its
 * type says; every other field takes the control of its type.
 *
 * @param rendering - The rendering of the field's type.
 * @param isSecret - Whether the API marks the field secret.
 * @returns The control to draw the field with.
 */
export const fieldControlOf = (rendering: FieldRendering, isSecret: boolean): FieldControl =>
  isSecret ? FieldControl.Secret : rendering.control
