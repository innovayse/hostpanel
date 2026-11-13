<script setup lang="ts">
/**
 * Modal for adding or editing a client contact.
 * Shows a two-column form with profile, address, and notification fields.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Contact } from '@/types/contact'
import type { ContactNotifications } from '@/types/contactnotifications'
import { useGeoOptions } from '@/composables/useGeoOptions'
import { ContactType } from '@/types/contact'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiPhoneInput from '@/components/ui/UiPhoneInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiLabelVariant } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for ContactFormModal. */
const props = defineProps<{
  /** Contact to edit, or null for create mode. */
  contact: Contact | null
  /** True while save is in flight. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Emitted with the form payload when saved. */
  save: [payload: Record<string, unknown>]
  /** Emitted when delete is clicked in edit mode. */
  delete: []
  /** Emitted when the modal is closed without saving. */
  close: []
}>()

/** Whether we are editing an existing contact. */
const isEditing = computed(() => props.contact !== null)

/** Contact first name. */
const firstName = ref('')

/** Contact last name. */
const lastName = ref('')

/** Optional company name. */
const companyName = ref('')

/** Contact email address. */
const email = ref('')

/** Phone number without country code. */
const phone = ref('')

/** Selected phone country ISO2 code. */
const phoneCountry = ref('US')

/** Contact type (Billing, Technical, General). */
const contactType = ref<string>(ContactType.General)

/** Street address line 1. */
const street = ref('')

/** Street address line 2. */
const address2 = ref('')

/** City. */
const city = ref('')

/** State or region. */
const state = ref('')

/** Postal code. */
const postCode = ref('')

/** Country ISO2 code. */
const country = ref('')

/** Email notification switches, named as the payload fields. */
const notify = reactive<ContactNotifications>({
  general: true,
  invoice: true,
  support: true,
  product: true,
  domain: true,
  affiliate: true,
})

/** Email notification switches in display order. */
const notifyToggles = computed<{ key: keyof typeof notify; label: string }[]>(() => [
  { key: 'general', label: t('shared.generalEmails') },
  { key: 'invoice', label: t('shared.invoiceEmails') },
  { key: 'support', label: t('shared.supportEmails') },
  { key: 'product', label: t('shared.productEmails') },
  { key: 'domain', label: t('shared.domainEmails') },
  { key: 'affiliate', label: t('shared.affiliateEmails') },
])

/** Field-level validation errors. */
const errors = ref({ firstName: '', lastName: '', email: '' })

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

const { countryOptions, resolvePhoneCode, parsePhone } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Contact type options. */
const contactTypeOptions = [
  { value: ContactType.Billing, label: t('nav.billing') },
  { value: ContactType.Technical, label: t('clients.contactForm.technical') },
  { value: ContactType.General, label: t('nav.settingsChildren.general') },
]

/**
 * Validates required fields.
 *
 * @returns True when all required fields are filled.
 */
const validate = (): boolean => {
  errors.value = { firstName: '', lastName: '', email: '' }
  if (!firstName.value.trim()) {
    errors.value.firstName = t('clients.clientForm.firstNameIsRequired')
  }
  if (!lastName.value.trim()) {
    errors.value.lastName = t('clients.clientForm.lastNameIsRequired')
  }
  if (!email.value.trim()) {
    errors.value.email = t('clients.clientForm.emailIsRequired')
  }
  return !hasErrors.value
}

/**
 * Validates and emits the save event with form payload.
 *
 * @returns Nothing.
 */
const handleSave = (): void => {
  if (!validate()) {
    return
  }

  emit('save', {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    companyName: companyName.value.trim() || null,
    email: email.value.trim(),
    phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : null,
    type: contactType.value,
    address: {
      street: street.value.trim() || null,
      address2: address2.value.trim() || null,
      city: city.value.trim() || null,
      state: state.value.trim() || null,
      postCode: postCode.value.trim() || null,
      country: country.value || null,
    },
    notifications: { ...notify },
  })
}

/** True while the delete confirmation prompt is showing; nothing is staged when false. */
const showDeleteConfirm = ref(false)

/**
 * Stages the delete confirmation. Only opens the prompt: {@link confirmDelete} does the work,
 * so the question is asked by the app's own modal instead of the browser's blocking dialog.
 *
 * @returns Nothing.
 */
const handleDelete = (): void => {
  showDeleteConfirm.value = true
}

/**
 * Emits the delete event and dismisses the prompt.
 *
 * There is no in-flight flag: the parent owns the request and unmounts this modal as soon as it
 * accepts the event, so this component never observes the outcome.
 *
 * @returns Nothing.
 */
const confirmDelete = (): void => {
  showDeleteConfirm.value = false
  emit('delete')
}

/**
 * Populates form fields from the contact prop on mount.
 *
 * @returns Nothing.
 */
const populateFromContact = (): void => {
  const c = props.contact
  if (!c) {
    return
  }

  firstName.value = c.firstName
  lastName.value = c.lastName
  companyName.value = c.companyName ?? ''
  email.value = c.email
  contactType.value = c.type

  // Phone is stored as one string; split it back into country code and national number.
  if (c.phone) {
    const parsed = parsePhone(c.phone)
    phoneCountry.value = parsed.countryIso2
    phone.value = parsed.number
  }

  street.value = c.address.street ?? ''
  address2.value = c.address.address2 ?? ''
  city.value = c.address.city ?? ''
  state.value = c.address.state ?? ''
  postCode.value = c.address.postCode ?? ''
  country.value = c.address.country ?? ''

  Object.assign(notify, c.notifications)
}

onMounted(() => {
  if (props.contact) {
    populateFromContact()
  }
})
</script>

<template>
  <UiDialog
    :title="isEditing ? t('clients.contactForm.editContact') : t('clients.contactForm.addContact')"
    size="lg"
    @close="emit('close')"
  >
    <UiForm stacked @submit="handleSave">
      <UiGrid :columns="2">
        <UiStack>
          <UiHeading :level="3" variant="card">{{ t('clients.contactForm.profile') }}</UiHeading>
          <UiField :label="t('shared.firstName')" :error="errors.firstName || undefined" required>
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="firstName"
                type="text"
                :invalid="invalid"
                :placeholder="t('shared.firstName')"
                data-testid="clients-contact-first-name"
                @input="errors.firstName = ''"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.lastName')" :error="errors.lastName || undefined" required>
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="lastName"
                type="text"
                :invalid="invalid"
                :placeholder="t('shared.lastName')"
                data-testid="clients-contact-last-name"
                @input="errors.lastName = ''"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.companyName')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="companyName"
                type="text"
                :placeholder="t('shared.company')"
                data-testid="clients-contact-company"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.email')" :error="errors.email || undefined" required>
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="email"
                type="email"
                :invalid="invalid"
                :placeholder="t('shared.examples.email')"
                data-testid="clients-contact-email"
                @input="errors.email = ''"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.phone')">
            <UiPhoneInput
              v-model:phone-number="phone"
              v-model:country-code="phoneCountry"
              data-testid="clients-contact-phone"
            />
          </UiField>
          <UiField :label="t('clients.contactForm.contactType')">
            <UiSelect v-model="contactType" :options="contactTypeOptions" :placeholder="t('clients.contactForm.contactType')" data-testid="clients-contact-type" />
          </UiField>
        </UiStack>

        <UiStack>
          <UiHeading :level="3" variant="card">{{ t('clients.contactForm.address') }}</UiHeading>
          <UiField :label="t('shared.address1')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="street"
                type="text"
                :placeholder="t('clients.contactForm.streetAddress')"
                data-testid="clients-contact-street"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.address2')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="address2"
                type="text"
                :placeholder="t('clients.contactForm.suiteAptEtc')"
                data-testid="clients-contact-address2"
              />
            </template>
          </UiField>
          <UiGrid :columns="2" gap="sm">
            <UiField :label="t('shared.city')">
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="city"
                  type="text"
                  :placeholder="t('shared.city')"
                  data-testid="clients-contact-city"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.stateRegion')">
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="state"
                  type="text"
                  :placeholder="t('shared.state')"
                  data-testid="clients-contact-state"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.postcode')">
              <template #default="{ id }">
                <UiInput
                  :id="id"
                  v-model="postCode"
                  type="text"
                  :placeholder="t('shared.postcode')"
                  data-testid="clients-contact-postcode"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.country')">
              <UiSelect v-model="country" :options="countryOptions" searchable wide :placeholder="t('shared.selectCountry')" data-testid="clients-contact-country" />
            </UiField>
          </UiGrid>

          <UiHeading :level="3" variant="card">{{ t('shared.emailNotifications') }}</UiHeading>
          <UiStack gap="sm">
            <UiLabel v-for="toggle in notifyToggles" :key="toggle.key" :variant="UiLabelVariant.Inline">
              <UiToggle v-model="notify[toggle.key]" :data-testid="`clients-contact-notify-${toggle.key}`" />
              {{ toggle.label }}
            </UiLabel>
          </UiStack>
        </UiStack>
      </UiGrid>

      <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
        <UiButton
          v-if="isEditing"
          type="button"
          :variant="UiButtonVariant.Danger"
          data-testid="clients-contact-delete"
          @click="handleDelete"
        >
          {{ t('clients.contactForm.deleteContact') }}
        </UiButton>
        <UiStack direction="row" gap="sm" :align="UiAlignItems.Center">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="clients-contact-cancel"
            @click="emit('close')"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            type="submit"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            data-testid="clients-contact-save"
          >
            {{ isEditing ? t('shared.saveChanges') : t('clients.contactForm.addContact') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiForm>
  </UiDialog>

  <UiDialog
    v-if="showDeleteConfirm"
    variant="confirm"
    :title="t('clients.contactForm.deleteContact')"
    :message="t('clients.contactForm.permanentlyDeleteThisContactThis')"
    :confirm-label="t('shared.delete')"
    tone="danger"
    @confirm="confirmDelete"
    @close="showDeleteConfirm = false"
  />
</template>
