<script setup lang="ts">
/**
 * Modal for modifying the WHOIS registrant contact details for a domain.
 * Displays a two-column form with standard contact/address fields.
 */
import { reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import type { UiInputType } from '@/utils/ui/input'

const { t } = useI18n()

/** Props for DomainContactModal. */
defineProps<{
  /** True while the save operation is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the contact form payload when saved. */
  save: [payload: Record<string, string>]
  /** Emitted when the modal is closed without saving. */
  close: []
}>()

/** Keys of the contact payload. */
const ContactField = {
  FirstName: 'firstName',
  LastName: 'lastName',
  Organization: 'organization',
  Email: 'email',
  Phone: 'phone',
  Address1: 'address1',
  Address2: 'address2',
  City: 'city',
  State: 'state',
  PostalCode: 'postalCode',
  Country: 'country',
} as const

/** One contact input of the form. */
interface ContactFieldDef {
  /** Payload key. */
  key: (typeof ContactField)[keyof typeof ContactField]
  /** Translated label. */
  label: string
  /** Translated or literal example shown as placeholder. */
  placeholder: string
  /** Native input type. */
  type: UiInputType
}

/** Fields rendered in order, two per row. */
const fields: ContactFieldDef[] = [
  { key: ContactField.FirstName, label: t('shared.firstName'), placeholder: t('shared.john'), type: 'text' },
  { key: ContactField.LastName, label: t('shared.lastName'), placeholder: t('shared.doe'), type: 'text' },
  { key: ContactField.Organization, label: t('clients.domainContact.organization'), placeholder: t('shared.acmeInc'), type: 'text' },
  { key: ContactField.Email, label: t('shared.email'), placeholder: t('shared.examples.email'), type: 'email' },
  { key: ContactField.Phone, label: t('shared.phone'), placeholder: t('shared.examples.phone'), type: 'text' },
  { key: ContactField.Address1, label: t('shared.address1'), placeholder: t('clients.domainContact.123MainSt'), type: 'text' },
  { key: ContactField.Address2, label: t('shared.address2'), placeholder: t('shared.suite100'), type: 'text' },
  { key: ContactField.City, label: t('shared.city'), placeholder: t('shared.newYork'), type: 'text' },
  { key: ContactField.State, label: t('shared.state'), placeholder: t('shared.examples.state'), type: 'text' },
  { key: ContactField.PostalCode, label: t('clients.domainContact.postalCode'), placeholder: t('shared.examples.postalCode'), type: 'text' },
  { key: ContactField.Country, label: t('shared.country'), placeholder: t('shared.examples.countryCode'), type: 'text' },
]

/** Contact values keyed by payload key. */
const form = reactive<Record<string, string>>(
  Object.fromEntries(fields.map(f => [f.key, ''])),
)

/**
 * Emits the save event with all contact field values, trimmed.
 *
 * @returns Nothing.
 */
const handleSave = (): void => {
  emit('save', Object.fromEntries(fields.map(f => [f.key, (form[f.key] ?? '').trim()])))
}
</script>

<template>
  <UiDialog :title="t('clients.domainContact.modifyContactDetails')" size="lg" @close="emit('close')">
    <UiForm stacked @submit="handleSave">
      <UiGrid :columns="2" gap="sm">
        <UiField v-for="field in fields" :key="field.key" :label="field.label">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="form[field.key]"
              :type="field.type"
              :placeholder="field.placeholder"
              :data-testid="`clients-domain-contact-${field.key}`"
            />
          </template>
        </UiField>
      </UiGrid>
      <UiStack direction="row" :justify="UiJustify.End" :align="UiAlignItems.Center" gap="sm">
        <UiButton
          type="button"
          :variant="UiButtonVariant.Secondary"
          data-testid="clients-domain-contact-cancel"
          @click="emit('close')"
        >
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton
          type="submit"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          data-testid="clients-domain-contact-save"
        >
          {{ t('shared.saveChanges') }}
        </UiButton>
      </UiStack>
    </UiForm>
  </UiDialog>
</template>
