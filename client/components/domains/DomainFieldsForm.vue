<template>
  <UiStack v-if="fields.length" :gap="UiGap.Sm" :data-testid="testid">
    <UiHeading :level="3" size="subsection">{{ $t('domains.fields.title') }}</UiHeading>
    <UiText :size="UiTextSize.Sm" tone="muted">{{ $t('domains.fields.note') }}</UiText>

    <template v-for="field in fields" :key="field.key">
      <UiSelect
        v-if="field.type === DomainFieldType.Select"
        :model-value="valueOf(field.key)"
        :label="field.label"
        :placeholder="field.placeholder ?? field.label"
        :options="field.options"
        :required="field.required"
        :disabled="disabled"
        :error="fieldErrorFor(errors, field.key)"
        :hint="field.description ?? undefined"
        :size="UiSize.Sm"
        :testid="`${testid}-${field.key}`"
        @update:model-value="edit(field.key, String($event ?? ''))"
      />
      <UiDatePicker
        v-else-if="field.type === DomainFieldType.Date"
        :model-value="valueOf(field.key)"
        :label="field.label"
        :required="field.required"
        :disabled="disabled"
        :error="fieldErrorFor(errors, field.key)"
        :hint="field.description ?? undefined"
        :testid="`${testid}-${field.key}`"
        @update:model-value="edit(field.key, $event)"
      />
      <UiTextarea
        v-else-if="field.type === DomainFieldType.Textarea"
        :model-value="valueOf(field.key)"
        :label="field.label"
        :placeholder="field.placeholder ?? field.label"
        :required="field.required"
        :disabled="disabled"
        :max-length="field.maxLength ?? undefined"
        :error="fieldErrorFor(errors, field.key)"
        :hint="field.description ?? undefined"
        :testid="`${testid}-${field.key}`"
        @update:model-value="edit(field.key, $event)"
      />
      <UiInput
        v-else
        :model-value="valueOf(field.key)"
        :label="field.label"
        :placeholder="field.placeholder ?? field.label"
        :type="inputTypeOf(field.type)"
        :required="field.required"
        :disabled="disabled"
        :error="fieldErrorFor(errors, field.key)"
        :hint="field.description ?? undefined"
        :size="UiSize.Sm"
        :testid="`${testid}-${field.key}`"
        @update:model-value="edit(field.key, String($event))"
      />
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * DomainFieldsForm: the extra fields a registrar plugin asks for a domain, drawn generically from what the plugin declares.
 * It knows no zone, country or registry: each field is an input, a text area, a kit list or the kit date picker, chosen by the
 * field's declared kind, with its label, hint and help text as the plugin worded them. It owns no state: it shows the values
 * it is given and reports every edit, and it shows the refusal the API gave for a field next to it.
 */
import { DomainFieldType } from '~/types/domainfieldtype'
import type { DomainField } from '~/types/domainfield'
import { fieldErrorFor } from '~/utils/domainFields'
import { UiGap, UiInputType, UiSize, UiTextSize } from '~/utils/ui/enums'

/** Props of DomainFieldsForm. */
interface Props {
  /** The fields to draw, in the plugin's order; nothing is drawn when there are none. */
  fields: DomainField[]
  /** The values by field key. */
  modelValue: Record<string, string>
  /** The API's refusal per field key, as the store keeps it; empty when nothing was refused. */
  errors?: Record<string, string>
  /** Disables every field. */
  disabled?: boolean
  /** Prefix of every `data-testid`; each field adds its key. */
  testid?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  errors: () => ({}),
  disabled: false,
  testid: 'domain-fields'
})

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader edited a field; carries the whole set of values. */
  'update:modelValue': [value: Record<string, string>]
}>()

/**
 * Reads one field's current value.
 *
 * @param key - The field key.
 * @returns The value, empty text when none was typed.
 */
const valueOf = (key: string): string => props.modelValue[key] ?? ''

/**
 * Maps a declared field kind to the kit input's type.
 *
 * @param type - The field's declared kind.
 * @returns The input type; a plain text input for every kind without a type of its own.
 */
const inputTypeOf = (type: DomainFieldType): UiInputType => {
  if (type === DomainFieldType.Number) {
    return UiInputType.Number
  }
  return type === DomainFieldType.Url ? UiInputType.Url : UiInputType.Text
}

/**
 * Reports one edit with the whole set of values.
 *
 * @param key - The field that changed.
 * @param value - Its new value.
 */
const edit = (key: string, value: string): void => {
  emit('update:modelValue', { ...props.modelValue, [key]: value })
}
</script>
