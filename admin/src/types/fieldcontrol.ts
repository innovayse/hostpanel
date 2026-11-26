/**
 * Vocabulary of the one mapping from a form field's type to the global kit control that draws it, shared by the
 * plugin forms and the integration config form (see `lib/fieldControls.ts`).
 */
import type { UiInputType } from '@/utils/ui/input'

/** The one global kit control that draws a form field, so no field type brings markup of its own. */
export enum FieldControl {
  /** `UiInput`: one line of text, drawn with the rendering's `inputType`. */
  Input = 'input',
  /** `UiNumberInput`: a bounded number with steppers. */
  Number = 'number',
  /** `UiTextarea`: several lines of text. */
  Textarea = 'textarea',
  /** `UiSelect`: a choice from the field's options. */
  Select = 'select',
  /** `UiToggle`: on or off. */
  Toggle = 'toggle',
  /** `UiSecretField`: a write-only value. */
  Secret = 'secret',
}

/** How one field type is drawn. */
export interface FieldRendering {
  /** The kit control that draws the field. */
  control: FieldControl
  /** The native input type, for the `Input` control; plain text when absent. */
  inputType?: UiInputType
}
