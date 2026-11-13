<template>
  <UiGrid cols="2" gap="sm" data-testid="domain-contact-form">
    <UiInput
      v-for="field in FIELDS"
      :key="field.key"
      :model-value="values[field.key]"
      :label="$t(`client.domains.${field.label}`)"
      :placeholder="$t(`client.domains.contactPlaceholder.${field.key}`)"
      :type="field.type"
      :disabled="disabled"
      :class="field.wide ? 'sm:col-span-2' : ''"
      :testid="`domain-contact-${field.key}`"
      size="sm"
      @update:model-value="emit('change', field.key, String($event))"
    />
  </UiGrid>
</template>

<script setup lang="ts">
/**
 * DomainContactForm: the eleven WHOIS fields of one contact role, in the order they are
 * normally written. It owns no state: it shows the values it is given and reports each edit.
 */
import type { DomainContactFields } from '~/types/domaincontactfields'
import { UiInputType } from '~/utils/ui/enums'

/** One field of the form. */
interface FieldDefinition {
  /** The key of the contact object the field edits. */
  key: keyof DomainContactFields
  /** The i18n key of the label, under `client.domains`. */
  label: string
  /** The kind of input. */
  type: UiInputType
  /** Whether the field spans both columns. */
  wide: boolean
}

/** The fields, in display order; half-width ones pair up in a row. */
const FIELDS: FieldDefinition[] = [
  { key: 'firstName', label: 'contactFirstName', type: UiInputType.Text, wide: false },
  { key: 'lastName', label: 'contactLastName', type: UiInputType.Text, wide: false },
  { key: 'organization', label: 'contactCompany', type: UiInputType.Text, wide: true },
  { key: 'email', label: 'contactEmail', type: UiInputType.Email, wide: true },
  { key: 'address1', label: 'contactAddress1', type: UiInputType.Text, wide: true },
  { key: 'address2', label: 'contactAddress2', type: UiInputType.Text, wide: true },
  { key: 'city', label: 'contactCity', type: UiInputType.Text, wide: false },
  { key: 'state', label: 'contactState', type: UiInputType.Text, wide: false },
  { key: 'postalCode', label: 'contactPostcode', type: UiInputType.Text, wide: false },
  { key: 'country', label: 'contactCountry', type: UiInputType.Text, wide: false },
  { key: 'phone', label: 'contactPhone', type: UiInputType.Tel, wide: true },
]

/** Props of DomainContactForm. */
interface Props {
  /** The contact being edited. */
  values: DomainContactFields
  /** Disables every field (the contact comes from an existing one). */
  disabled: boolean
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader edited a field. */
  change: [key: keyof DomainContactFields, value: string]
}>()
</script>
