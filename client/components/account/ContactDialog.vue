<template>
  <UiDialog
    :model-value="modelValue"
    :title="isAdd ? $t('client.contacts.addTitle') : $t('client.contacts.editTitle')"
    size="lg"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <UiForm id="account-contact-form" :error="errorText" spacing="md" data-testid="account-contact-form" @submit="onSubmit">
      <UiGrid cols="2" gap="md">
        <UiInput
          v-model="form.firstName"
          :label="$t('client.profile.firstName')"
          :placeholder="$t('client.account.placeholders.firstName')"
          :error="fieldErrors.firstName"
          size="sm"
          required
          testid="account-contact-firstname"
        />
        <UiInput
          v-model="form.lastName"
          :label="$t('client.profile.lastName')"
          :placeholder="$t('client.account.placeholders.lastName')"
          :error="fieldErrors.lastName"
          size="sm"
          required
          testid="account-contact-lastname"
        />
        <UiInput
          v-model="form.companyName"
          data-testid="contact-company"
          :label="$t('client.profile.companyName')"
          :placeholder="$t('client.account.placeholders.companyName')"
          :error="fieldErrors.companyName"
          size="sm"
          class="sm:col-span-2"
        />
        <UiInput
          v-model="form.email"
          type="email"
          :label="$t('client.profile.emailAddress')"
          :placeholder="$t('client.account.placeholders.email')"
          :error="fieldErrors.email"
          size="sm"
          class="sm:col-span-2"
          testid="account-contact-email"
        />
        <UiInput
          v-model="form.phone"
          data-testid="contact-phone"
          type="tel"
          :label="$t('client.profile.phone')"
          :placeholder="$t('client.account.placeholders.phone')"
          :error="fieldErrors.phone"
          size="sm"
          class="sm:col-span-2"
        />
      </UiGrid>

      <UiStack gap="sm">
        <UiText size="sm" weight="medium">{{ $t('client.contacts.emailNotifications') }}</UiText>
        <UiGrid cols="2" gap="sm">
          <UiCheckbox id="cm-general" v-model="form.notifications.general" :label="$t('client.contacts.generalEmails')" testid="account-contact-general" />
          <UiCheckbox id="cm-invoice" v-model="form.notifications.invoice" :label="$t('client.contacts.invoiceEmails')" testid="account-contact-invoice" />
          <UiCheckbox id="cm-domain" v-model="form.notifications.domain" :label="$t('client.contacts.domainEmails')" testid="account-contact-domain" />
          <UiCheckbox id="cm-product" v-model="form.notifications.product" :label="$t('client.contacts.productEmails')" testid="account-contact-product" />
          <UiCheckbox id="cm-support" v-model="form.notifications.support" :label="$t('client.contacts.supportEmails')" testid="account-contact-support" />
        </UiGrid>
      </UiStack>
    </UiForm>

    <!-- Pinned footer; the save button submits the body's form through its `form` attribute. -->
    <template #footer>
      <UiButton type="button" variant="outline" data-testid="account-contact-cancel" @click="emit('update:modelValue', false)">
        {{ $t('common.cancel') }}
      </UiButton>
      <UiButton type="submit" form="account-contact-form" :loading="contactSaving" data-testid="account-contact-save">
        <Check v-if="!contactSaving" :size="14" :stroke-width="2.5" class="mr-1.5" />
        {{ $t('client.contacts.save') }}
      </UiButton>
    </template>
  </UiDialog>
</template>

<script setup lang="ts">
/**
 * ContactDialog: adds a sub-contact or edits an existing one.
 *
 * Every opening reseeds the draft from `contact` (null means "new"). Saving goes through the
 * account store; on success the dialog closes and the tab is told to read the list again.
 *
 * @module components/account/ContactDialog
 */

import { Check } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import type { AccountContact } from '~/types/accountcontact'
import type { AccountContactInput } from '~/types/accountcontactinput'
import { ContactType } from '~/types/contacttype'
import { ContactDialogMode } from '~/types/contactdialogmode'
import { ContactSaveOutcome } from '~/types/contactsaveoutcome'

/** Props of {@link ContactDialog}. */
interface Props {
  /** Whether the dialog is open. */
  modelValue: boolean
  /** The contact to edit, or null to add a new one. */
  contact: AccountContact | null
}

/** The props. */
const props = defineProps<Props>()

/** Events of {@link ContactDialog}. */
const emit = defineEmits<{
  /** The dialog asked to open or close. */
  'update:modelValue': [value: boolean]
  /** A contact was saved, so the list should be read again. */
  saved: []
}>()

/** Translator for the local validation sentence. */
const { t } = useI18n()

/** The account store that saves the contact. */
const account = useAccountStore()

/** Save progress, the API's sentence and the per-field messages. */
const { contactSaving, contactError, fieldErrors } = storeToRefs(account)

/** Whether the dialog adds (true) or edits (false). */
const mode = computed<ContactDialogMode>(() => props.contact === null ? ContactDialogMode.Add : ContactDialogMode.Edit)

/** Convenience flag for the template's title. */
const isAdd = computed<boolean>(() => mode.value === ContactDialogMode.Add)

/**
 * The draft of the contact's fields. Text fields hold empty strings rather than null so the
 * inputs bind cleanly; {@link toNullable} turns a blank back into null on the way out. The
 * address, type and affiliate flag have no inputs here but are carried, so an edit sends back
 * what the contact already had instead of blanking it.
 */
const form = reactive({
  firstName: '',
  lastName: '',
  companyName: '',
  email: '',
  phone: '',
  type: ContactType.General,
  address: { street: '', address2: '', city: '', state: '', postCode: '', country: '' },
  notifications: {
    general: true,
    invoice: true,
    support: true,
    product: true,
    domain: false,
    affiliate: false
  }
})

/** Set when a contact is submitted without a name, so no request was made. */
const invalid = ref(false)

/** The error to show: the local validation sentence or the API's. */
const errorText = computed<string>(() => invalid.value ? t('client.contacts.saveError') : contactError.value)

// Every opening starts from the contact being edited (or the defaults) with no stale refusal.
watch(() => props.modelValue, (open) => {
  if (!open) {
    return
  }
  const contact = props.contact
  Object.assign(form, {
    firstName: contact?.firstName ?? '',
    lastName: contact?.lastName ?? '',
    companyName: contact?.companyName ?? '',
    email: contact?.email ?? '',
    phone: contact?.phone ?? '',
    type: contact?.type ?? ContactType.General,
    address: {
      street: contact?.address.street ?? '',
      address2: contact?.address.address2 ?? '',
      city: contact?.address.city ?? '',
      state: contact?.address.state ?? '',
      postCode: contact?.address.postCode ?? '',
      country: contact?.address.country ?? ''
    },
    notifications: {
      general: contact?.notifications.general ?? true,
      invoice: contact?.notifications.invoice ?? true,
      support: contact?.notifications.support ?? true,
      product: contact?.notifications.product ?? true,
      domain: contact?.notifications.domain ?? false,
      affiliate: contact?.notifications.affiliate ?? false
    }
  })
  invalid.value = false
  account.clearModalErrors()
}, { immediate: true })

/**
 * Turns a blank text field into null, the backend's spelling of "not set".
 *
 * @param value - The field as typed.
 * @returns The trimmed text, or null when it is blank.
 */
const toNullable = (value: string): string | null => value.trim() === '' ? null : value.trim()

/**
 * Builds the request body from the draft.
 *
 * @returns The contact in the backend's `AddContactCommand` / `UpdateContactCommand` shape.
 */
const toInput = (): AccountContactInput => ({
  ...form,
  companyName: toNullable(form.companyName),
  phone: toNullable(form.phone),
  address: {
    street: toNullable(form.address.street),
    address2: toNullable(form.address.address2),
    city: toNullable(form.address.city),
    state: toNullable(form.address.state),
    postCode: toNullable(form.address.postCode),
    country: toNullable(form.address.country)
  }
})

/** Saves the draft; on success closes the dialog and asks for a fresh list. */
const onSubmit = async (): Promise<void> => {
  // An edit without a contact is unreachable, and creating is the safe reading of it; the
  // alternative would be a PUT to `/contacts/null`.
  const outcome = await account.saveContact(props.contact?.id ?? null, toInput())
  invalid.value = outcome === ContactSaveOutcome.Invalid
  if (outcome === ContactSaveOutcome.Saved) {
    emit('update:modelValue', false)
    emit('saved')
  }
}
</script>
