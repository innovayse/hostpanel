<script setup lang="ts">
/**
 * DomainFieldsForm: the extra fields a registrar plugin asks for a domain, drawn generically from what the plugin declares.
 * It knows no zone, country or registry: each field is a kit input, text area, list or date picker chosen by the field's
 * declared kind, with its label, hint and help text as the plugin worded them. It owns no state: it shows the values it is
 * given and reports every edit, and it shows the refusal the API gave for a field next to it.
 */
import { useI18n } from 'vue-i18n'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { type DomainField, DomainFieldType } from '@/types/domain'
import { UiHeadingVariant, UiTextSize } from '@/utils/ui/text'
import { UiGap } from '@/utils/ui/layout'
import type { UiInputType } from '@/utils/ui/input'

const { t } = useI18n()

/** Props for DomainFieldsForm. */
const props = withDefaults(defineProps<{
  /** The fields to draw, in the plugin's order; nothing is drawn when there are none. */
  fields: DomainField[]
  /** The values by field key (two-way bound with `v-model`). */
  modelValue: Record<string, string>
  /** The API's refusal per field key; empty when nothing was refused. */
  errors?: Record<string, string>
}>(), {
  errors: () => ({}),
})

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
    return 'number'
  }
  return type === DomainFieldType.Url ? 'url' : 'text'
}

/**
 * Finds the refusal that belongs to one field. The error reader writes each segment of a dotted key with a lower case first
 * letter, so a key is looked up as declared and then in that form.
 *
 * @param key - The field key as the plugin declares it.
 * @returns The sentence, or undefined when the field was not refused.
 */
const errorOf = (key: string): string | undefined =>
  props.errors[key] ?? props.errors[key.split('.').map(segment => segment.charAt(0).toLowerCase() + segment.slice(1)).join('.')]

/**
 * Reports one edit with the whole set of values.
 *
 * @param key - The field that changed.
 * @param value - Its new value.
 */
const edit = (key: string, value: string | null): void => {
  emit('update:modelValue', { ...props.modelValue, [key]: value ?? '' })
}
</script>

<template>
  <UiStack v-if="fields.length" :gap="UiGap.Md" data-testid="clients-domain-fields">
    <UiHeading :level="3" :variant="UiHeadingVariant.Card">{{ t('domainFields.title') }}</UiHeading>
    <UiText tone="secondary" :size="UiTextSize.Sm">{{ t('domainFields.note') }}</UiText>

    <UiField
      v-for="field in fields"
      :key="field.key"
      :label="field.label"
      :hint="field.description ?? undefined"
      :error="errorOf(field.key)"
      :required="field.required"
    >
      <template #default="{ id: controlId, invalid }">
        <UiSelect
          v-if="field.type === DomainFieldType.Select"
          :id="controlId"
          :model-value="valueOf(field.key)"
          :options="field.options"
          :placeholder="field.placeholder ?? field.label"
          :data-testid="`clients-domain-fields-${field.key}`"
          @update:model-value="edit(field.key, $event)"
        />
        <UiDatePicker
          v-else-if="field.type === DomainFieldType.Date"
          :model-value="valueOf(field.key) || null"
          :placeholder="field.placeholder ?? field.label"
          :data-testid="`clients-domain-fields-${field.key}`"
          @update:model-value="edit(field.key, $event)"
        />
        <UiTextarea
          v-else-if="field.type === DomainFieldType.Textarea"
          :id="controlId"
          :model-value="valueOf(field.key)"
          :placeholder="field.placeholder ?? field.label"
          :invalid="invalid"
          :data-testid="`clients-domain-fields-${field.key}`"
          @update:model-value="edit(field.key, $event)"
        />
        <UiInput
          v-else
          :id="controlId"
          :model-value="valueOf(field.key)"
          :type="inputTypeOf(field.type)"
          :placeholder="field.placeholder ?? field.label"
          :invalid="invalid"
          :data-testid="`clients-domain-fields-${field.key}`"
          @update:model-value="edit(field.key, String($event))"
        />
      </template>
    </UiField>
  </UiStack>
</template>
