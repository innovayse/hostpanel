<script setup lang="ts">
/**
 * Client profile editing page: displays and allows editing of all client fields.
 *
 * Rendered inside {@link ClientLayout} which handles data fetching and the header.
 * All sections are on a single scrollable page in a two-column layout.
 */
import { ref, reactive, computed, watch, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import { CLIENT_STATUS_OPTIONS, CONTACT_TYPE_OPTIONS } from '@/lib/constants'
import { contactTypeTone, statusLabel } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { useGeoOptions } from '@/composables/useGeoOptions'
import { restrictsAccount, type RestrictiveClientStatus } from '@/lib/clientStatus'
import { ClientStatus, type ClientSettings } from '@/types/client'
import type { ContactNotifications } from '@/types/contactnotifications'
import ClientGroupSelect from '@/components/clients/ClientGroupSelect.vue'
import ClientStatusConfirmDialog from '@/components/clients/ClientStatusConfirmDialog.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPhoneInput from '@/components/ui/UiPhoneInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiButtonVariant, UiButtonType } from '@/utils/ui/button'
import { UiAlignItems, UiJustify, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone, UiHeadingVariant } from '@/utils/ui/text'
import { CLIENT_TAX_EXEMPTION_REASON_MAX_LENGTH, CLIENT_TAX_ID_MAX_LENGTH, NOTICE_VISIBLE_MS } from '@/lib/limits'
import { UiTableDensity } from '@/utils/ui/injection'
import { PROFILE_CONTACTS_TABLE, PROFILE_FORM_PART } from '@/views/ClientProfileView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { UiInputType } from '@/utils/ui/input'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Clients store, owner of the loaded client and its update. */
const store = useClientsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** True while save is in flight (owned by the store). */
const saving = computed(() => store.saving)

/** Success message after save. */
const saveSuccess = ref(false)

/** Timer that hides the success message. */
let savedTimer: ReturnType<typeof setTimeout> | undefined

/** Error message after save (owned by the store). */
const saveError = computed(() => store.saveError)

/** Client first name. */
const firstName = ref('')

/** Client last name. */
const lastName = ref('')

/** Optional company name. */
const companyName = ref('')

/** Optional phone number (without country code). */
const phone = ref('')

/** Selected phone country ISO2 code. */
const phoneCountry = ref('US')

/** Client status. */
const status = ref<ClientStatus>(ClientStatus.Active)

/** Client email (read-only, from Identity). */
const email = ref('')

/** Client ID (read-only). */
const id = ref(0)

/** Joined date (read-only). */
const createdAt = ref('')

/**
 * The client group the client belongs to, chosen in the form and sent on save: the update replaces
 * the whole record, so a null takes the client out of its group.
 */
const clientGroupId = ref<number | null>(null)

/** Street address line 1. */
const street = ref('')

/** Street address line 2. */
const address2 = ref('')

/** City. */
const city = ref('')

/** State or province. */
const state = ref('')

/** Postal code. */
const postCode = ref('')

/** Selected country code. */
const country = ref('')

/** Selected currency code. */
const currency = ref('')

/**
 * Explains the currency lock under the field while the client holds credit; empty otherwise.
 * Only the hint is written here: the refusal sentence itself comes from the API and shows in the
 * form level message, like the invoice lock.
 */
const currencyHint = computed(() => ((store.current?.creditBalance ?? 0) !== 0 ? t('clients.clientProfile.currencyLockedByCreditHint') : undefined))

/** Help lines of the form by field key: the currency lock note while the client holds credit. */
const hints = computed((): Record<string, string> => (currencyHint.value === undefined ? {} : { currency: currencyHint.value }))

/** Payment method. */
const paymentMethod = ref('')

/** Billing contact. */
const billingContact = ref('')

/** Email notification switches, named as the payload fields. */
const notify = reactive<ContactNotifications>({
  general: true,
  invoice: true,
  support: true,
  product: true,
  domain: true,
  affiliate: true,
})

/** Account setting switches, named as the payload fields. */
const settings = reactive<ClientSettings>({
  overdueNotices: true,
  taxExempt: false,
  taxId: null,
  taxExemptionReason: null,
  separateInvoices: false,
  disableCcProcessing: false,
  marketingOptIn: false,
  statusUpdate: true,
  allowSso: true,
})

/** Internal admin notes. */
const adminNotes = ref('')

const { countryOptions, resolvePhoneCode, parsePhone } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Currency options for the UiSelect dropdown. */
const currencyOptions = computed(() =>
  [{ value: '', label: t('shared.selectCurrency') }, ...store.currencies.map(c => ({ value: c.code, label: `${c.name} (${c.symbol})` }))],
)

/** Status options for the dropdown. */
const statusOptions = CLIENT_STATUS_OPTIONS

/** The status chosen in the form when it restricts the account, whose effects are shown under the select. */
const chosenRestrictiveStatus = computed(() => (restrictsAccount(status.value) ? status.value : null))

/** The restrictive status awaiting the operator's confirmation, null when no prompt is open. */
const pendingStatus = ref<RestrictiveClientStatus | null>(null)

/** Field-level validation errors. */
const errors = ref({ firstName: '', lastName: '' })

/** True when any validation error is present. */
const hasErrors = computed(() => Object.values(errors.value).some(Boolean))

/**
 * Validates required fields.
 *
 * @returns True when all required fields are filled.
 */
const validate = (): boolean => {
  errors.value = { firstName: '', lastName: '' }
  if (!firstName.value.trim()) {
    errors.value.firstName = t('clients.clientForm.firstNameIsRequired')
  }
  if (!lastName.value.trim()) {
    errors.value.lastName = t('clients.clientForm.lastNameIsRequired')
  }
  return !hasErrors.value
}

/**
 * Populates all form refs from the store's current client.
 *
 * @returns Nothing.
 */
const populateForm = (): void => {
  const c = store.current
  if (!c) {
    return
  }

  id.value = c.id
  email.value = c.email ?? ''
  firstName.value = c.firstName
  lastName.value = c.lastName
  companyName.value = c.companyName ?? ''
  status.value = c.status
  createdAt.value = c.createdAt
  clientGroupId.value = c.clientGroupId ?? null

  // Phone is stored as one string; split it back into country code and national number.
  if (c.phone) {
    const parsed = parsePhone(c.phone)
    phoneCountry.value = parsed.countryIso2
    phone.value = parsed.number
  } else {
    phone.value = ''
  }

  street.value = c.address.street ?? ''
  address2.value = c.address.address2 ?? ''
  city.value = c.address.city ?? ''
  state.value = c.address.state ?? ''
  postCode.value = c.address.postCode ?? ''
  country.value = c.address.country ?? ''
  currency.value = c.preferences.currency ?? ''
  paymentMethod.value = c.preferences.paymentMethod ?? ''
  billingContact.value = c.preferences.billingContact ?? ''
  adminNotes.value = c.preferences.adminNotes ?? ''

  Object.assign(notify, c.notifications)
  Object.assign(settings, c.settings)
}

/**
 * Resets form to the last saved state.
 *
 * @returns Nothing.
 */
const handleCancel = (): void => {
  populateForm()
  store.saveError = null
  saveSuccess.value = false
}

/**
 * Validates the form and saves it. Moving the account to Suspended or Closed is first put to the
 * operator, with the effects spelled out, because both change what the client and their services can do.
 *
 * @returns Promise that resolves when the save attempt has finished or the prompt is open.
 */
const handleSave = async (): Promise<void> => {
  if (!validate()) {
    return
  }

  if (restrictsAccount(status.value) && status.value !== store.current?.status) {
    pendingStatus.value = status.value
    return
  }

  await saveClient()
}

/**
 * Confirms the staged status change and saves.
 *
 * @returns Promise that resolves when the save attempt has finished.
 */
const confirmStatusChange = async (): Promise<void> => {
  pendingStatus.value = null
  await saveClient()
}

/**
 * Submits all changes to the API.
 *
 * @returns Promise that resolves when the save attempt has finished.
 */
const saveClient = async (): Promise<void> => {
  saveSuccess.value = false

  const saved = await store.updateClient(clientId.value, {
    email: email.value.trim() || null,
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    companyName: companyName.value.trim() || null,
    phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : null,
    address: {
      street: street.value.trim() || null,
      address2: address2.value.trim() || null,
      city: city.value.trim() || null,
      state: state.value.trim() || null,
      postCode: postCode.value.trim() || null,
      country: country.value || null,
    },
    preferences: {
      // The account language is not edited on this form; null leaves it as it is.
      language: null,
      currency: currency.value || null,
      paymentMethod: paymentMethod.value.trim() || null,
      billingContact: billingContact.value.trim() || null,
      adminNotes: adminNotes.value.trim() || null,
    },
    status: status.value,
    clientGroupId: clientGroupId.value,
    notifications: { ...notify },
    settings: {
      ...settings,
      taxId: settings.taxId?.trim() || null,
      taxExemptionReason: settings.taxExemptionReason?.trim() || null,
    },
  })
  if (saved) {
    saveSuccess.value = true
    clearTimeout(savedTimer)
    savedTimer = setTimeout(() => {
      saveSuccess.value = false
    }, NOTICE_VISIBLE_MS)
  }
}

// Populate form when store data loads
watch(() => store.current, () => populateForm(), { immediate: true })

onBeforeUnmount(() => {
  clearTimeout(savedTimer)
})
</script>

<template>
  <UiStack v-if="store.current">
    <UiSchemaForm :columns="PROFILE_FORM_PART.columns" :errors="errors" :hints="hints" @submit="handleSave">
      <template #field-identity>
        <UiStack :direction="UiDirection.Row" :gap="UiGap.Lg">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientProfile.clientId') }} {{ id }}
          </UiText>
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('clients.clientProfile.joined') }} {{ formatDate(createdAt) }}
          </UiText>
        </UiStack>
      </template>
      <template #field-email="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="email"
          :type="UiInputType.Email"
          :placeholder="t('shared.examples.email')"
          data-testid="client-profile-email"
        />
      </template>
      <template #field-firstName="{ id: fieldId, invalid }">
        <UiInput
          :id="fieldId"
          v-model="firstName"
          :type="UiInputType.Text"
          :invalid="invalid"
          :placeholder="t('shared.firstName')"
          data-testid="client-profile-first-name"
          @input="errors.firstName = ''"
        />
      </template>
      <template #field-lastName="{ id: fieldId, invalid }">
        <UiInput
          :id="fieldId"
          v-model="lastName"
          :type="UiInputType.Text"
          :invalid="invalid"
          :placeholder="t('shared.lastName')"
          data-testid="client-profile-last-name"
          @input="errors.lastName = ''"
        />
      </template>
      <template #field-companyName="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="companyName"
          :type="UiInputType.Text"
          :placeholder="t('shared.company')"
          data-testid="client-profile-company"
        />
      </template>
      <template #field-phone>
        <UiPhoneInput
          v-model:phone-number="phone"
          v-model:country-code="phoneCountry"
          data-testid="client-profile-phone"
        />
      </template>
      <template #field-status>
        <UiSelect data-testid="client-profile-status" v-model="status" :options="statusOptions" :placeholder="t('shared.status')" />
        <UiText v-if="chosenRestrictiveStatus" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-profile-status-effects">
          {{ t(`clients.clientProfile.statusEffects.${chosenRestrictiveStatus}`) }}
        </UiText>
      </template>
      <template #field-clientGroupId="{ id: fieldId }">
        <ClientGroupSelect
          :id="fieldId"
          v-model="clientGroupId"
          :empty-label="t('clients.clientGroups.noGroup')"
          data-testid="client-profile-group"
        />
      </template>

      <template #field-street="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="street"
          :type="UiInputType.Text"
          :placeholder="t('clients.clientProfile.streetAddress')"
          data-testid="client-profile-street"
        />
      </template>
      <template #field-address2="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="address2"
          :type="UiInputType.Text"
          :placeholder="t('clients.clientProfile.suiteAptEtc')"
          data-testid="client-profile-address2"
        />
      </template>
      <template #field-city="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="city"
          :type="UiInputType.Text"
          :placeholder="t('shared.city')"
          data-testid="client-profile-city"
        />
      </template>
      <template #field-state="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="state"
          :type="UiInputType.Text"
          :placeholder="t('shared.state')"
          data-testid="client-profile-state"
        />
      </template>
      <template #field-postCode="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="postCode"
          :type="UiInputType.Text"
          :placeholder="t('shared.postcode')"
          data-testid="client-profile-postcode"
        />
      </template>
      <template #field-country>
        <UiSelect data-testid="client-profile-country" v-model="country" :options="countryOptions" searchable wide :placeholder="t('shared.selectCountry')" />
      </template>

      <template #field-currency>
        <UiSelect data-testid="client-profile-currency" v-model="currency" :options="currencyOptions" :placeholder="t('shared.selectCurrency')" />
      </template>
      <template #field-paymentMethod="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="paymentMethod"
          :type="UiInputType.Text"
          :placeholder="t('shared.creditCardBankTransfer')"
          data-testid="client-profile-payment-method"
        />
      </template>
      <template #field-billingContact="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="billingContact"
          :type="UiInputType.Text"
          :placeholder="t('clients.clientProfile.billingContactName')"
          data-testid="client-profile-billing-contact"
        />
      </template>

      <template #field-notifyGeneral>
        <UiToggle v-model="notify.general" data-testid="client-profile-notify-general" />
      </template>
      <template #field-notifyInvoice>
        <UiToggle v-model="notify.invoice" data-testid="client-profile-notify-invoice" />
      </template>
      <template #field-notifySupport>
        <UiToggle v-model="notify.support" data-testid="client-profile-notify-support" />
      </template>
      <template #field-notifyProduct>
        <UiToggle v-model="notify.product" data-testid="client-profile-notify-product" />
      </template>
      <template #field-notifyDomain>
        <UiToggle v-model="notify.domain" data-testid="client-profile-notify-domain" />
      </template>
      <template #field-notifyAffiliate>
        <UiToggle v-model="notify.affiliate" data-testid="client-profile-notify-affiliate" />
      </template>

      <template #field-overdueNotices>
        <UiToggle v-model="settings.overdueNotices" data-testid="client-profile-overdueNotices" />
      </template>
      <template #field-taxExempt>
        <UiToggle v-model="settings.taxExempt" data-testid="client-profile-taxExempt" />
      </template>
      <template #field-separateInvoices>
        <UiToggle v-model="settings.separateInvoices" data-testid="client-profile-separateInvoices" />
      </template>
      <template #field-disableCcProcessing>
        <UiToggle v-model="settings.disableCcProcessing" data-testid="client-profile-disableCcProcessing" />
      </template>
      <template #field-marketingOptIn>
        <UiToggle v-model="settings.marketingOptIn" data-testid="client-profile-marketingOptIn" />
      </template>
      <template #field-statusUpdate>
        <UiToggle v-model="settings.statusUpdate" data-testid="client-profile-statusUpdate" />
      </template>
      <template #field-allowSso>
        <UiToggle v-model="settings.allowSso" data-testid="client-profile-allowSso" />
      </template>
      <template #field-taxId="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="settings.taxId"
          :type="UiInputType.Text"
          :maxlength="CLIENT_TAX_ID_MAX_LENGTH"
          :placeholder="t('shared.taxIdPlaceholder')"
          data-testid="client-profile-tax-id"
        />
      </template>
      <template #field-taxExemptionReason="{ id: fieldId }">
        <UiInput
          :id="fieldId"
          v-model="settings.taxExemptionReason"
          :type="UiInputType.Text"
          :maxlength="CLIENT_TAX_EXEMPTION_REASON_MAX_LENGTH"
          :placeholder="t('shared.taxExemptionReasonPlaceholder')"
          data-testid="client-profile-tax-exemption-reason"
        />
      </template>

      <template #field-adminNotes="{ id: fieldId }">
        <UiTextarea
          :id="fieldId"
          v-model="adminNotes"
          rows="6"
          :placeholder="t('shared.internalNotesVisibleOnlyTo')"
          data-testid="client-profile-admin-notes"
        />
      </template>

      <!-- Below md the notice and both buttons are one full-width column, so Cancel and Save are the same size; from md up they are a row at the right. -->
      <UiStack :direction="UiDirection.Row" :justify="UiJustify.End" :align="UiAlignItems.Center" :gap="UiGap.Sm" reflow-below-md>
        <UiAlert v-if="saveSuccess" :variant="UiTone.Success" class="flex-1" data-testid="client-profile-saved">
          {{ t('shared.changesSavedSuccessfully') }}
        </UiAlert>
        <UiAlert v-if="saveError" :variant="UiTone.Danger" class="flex-1" data-testid="client-profile-error">
          {{ saveError }}
        </UiAlert>
        <UiButton :action="UiActionKind.Cancel"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Secondary"
          data-testid="client-profile-cancel"
          @click="handleCancel"
        >
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Save"
          :type="UiButtonType.Submit"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          data-testid="client-profile-save"
        >
          {{ t('shared.save') }}
        </UiButton>
      </UiStack>
    </UiSchemaForm>

    <UiDataTable
      :empty-text="t('clients.clientProfile.noContactsAddedYet')"
      :density="UiTableDensity.Comfortable"
      :empty="store.current.contacts.length === 0"
      data-testid="client-profile-contacts"
    >
      <template #header>
        <UiHeading :level="2" :variant="UiHeadingVariant.Card" data-testid="client-profile-contacts-title">
          {{ t('clients.clientProfile.contactsLength', { length: store.current.contacts.length }) }}
        </UiHeading>
      </template>
      <UiSchemaTableHead :columns="PROFILE_CONTACTS_TABLE.columns" />
      <UiTableBody>
        <UiTableRow v-for="contact in store.current.contacts" :key="contact.id" hoverable>
          <UiTableCell>{{ contact.firstName }} {{ contact.lastName }}</UiTableCell>
          <UiTableCell muted>{{ contact.email }}</UiTableCell>
          <UiTableCell muted>{{ contact.phone || EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="contactTypeTone(contact.type)" small>{{ statusLabel(CONTACT_TYPE_OPTIONS, contact.type) }}</UiBadge>
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>

    <ClientStatusConfirmDialog
      v-if="pendingStatus"
      :status="pendingStatus"
      :loading="saving"
      testid="client-profile-status-confirm"
      @confirm="confirmStatusChange"
      @close="pendingStatus = null"
    />
  </UiStack>
</template>
