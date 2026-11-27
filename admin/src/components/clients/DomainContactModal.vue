<script setup lang="ts">
/**
 * Modal for modifying the WHOIS registrant contact details for a domain.
 * Displays a two-column form with standard contact/address fields.
 */
import { onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DomainFieldsForm from '@/components/clients/DomainFieldsForm.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { attributesFor, holderOf } from '@/lib/domainFields'
import { useDomainsStore } from '@/stores/domains'
import { type DomainField, DomainFieldOperation } from '@/types/domain'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import type { UiInputType } from '@/utils/ui/input'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Props for DomainContactModal. */
const props = withDefaults(defineProps<{
  /** True while the save operation is in flight. */
  saving: boolean
  /** Primary key of the domain, whose registrant contact and extra values prefill the form. */
  domainId: number | string
  /** Fully qualified name of the domain, which decides the extra fields the registrar plugin asks for. */
  domainName: string
  /** The API's refusal per field key from the last failed save; empty when nothing was refused. */
  fieldErrors?: Record<string, string>
}>(), {
  /**
   * No refusal until a save fails.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
})

/** The domains store, which reads the contact the registrar holds and the extra fields of the domain. */
const store = useDomainsStore()

const emit = defineEmits<{
  /** Emitted with the contact form payload and the registry's extra values (by field key) when saved. */
  save: [payload: Record<string, string>, extendedAttributes: Record<string, string>]
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

/** The registry's extra values as edited, by field key. */
const attributes = ref<Record<string, string>>({})

/** The extra fields the registrar plugin asks for a contact change of this domain; empty until read. */
const extraFields = ref<DomainField[]>([])

/**
 * Reads the extra fields for the domain and the kind of registrant the form describes, and keeps the values of the fields
 * that still apply.
 *
 * @returns Resolves when the fields are read.
 */
const loadFields = async (): Promise<void> => {
  extraFields.value = await store.fetchDomainFields(
    props.domainName,
    DomainFieldOperation.ContactChange,
    holderOf(form[ContactField.Organization]),
  )
  attributes.value = attributesFor(extraFields.value, attributes.value)
}

// The fields depend on whether the contact names an organisation, so they are read again when that changes.
watch(() => holderOf(form[ContactField.Organization]), loadFields)

// Prefill from what the registrar holds; a contact that cannot be read leaves the form empty, as it always was.
onMounted(async () => {
  const held = await store.fetchContact(props.domainId)
  if (held) {
    for (const field of fields) {
      form[field.key] = held[field.key] ?? ''
    }
    attributes.value = { ...held.extendedAttributes }
  }
  await loadFields()
})

/**
 * Emits the save event with all contact field values, trimmed, and the registry's extra values that apply.
 *
 * @returns Nothing.
 */
const handleSave = (): void => {
  emit(
    'save',
    Object.fromEntries(fields.map(f => [f.key, (form[f.key] ?? '').trim()])),
    attributesFor(extraFields.value, attributes.value),
  )
}
</script>

<template>
  <UiDialog :title="t('clients.domainContact.modifyContactDetails')" :size="UiDialogSize.Lg" @close="emit('close')">
    <UiForm stacked @submit="handleSave">
      <UiGrid :columns="2" :gap="UiGap.Sm" container>
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
      <DomainFieldsForm v-model="attributes" :fields="extraFields" :errors="fieldErrors" />
      <UiStack :direction="UiDirection.Row" :justify="UiJustify.End" :align="UiAlignItems.Center" :gap="UiGap.Sm">
        <UiButton :action="UiActionKind.Cancel"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Secondary"
          data-testid="clients-domain-contact-cancel"
          @click="emit('close')"
        >
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Save"
          :type="UiButtonType.Submit"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          data-testid="clients-domain-contact-save"
        >
          {{ t('shared.save') }}
        </UiButton>
      </UiStack>
    </UiForm>
  </UiDialog>
</template>
