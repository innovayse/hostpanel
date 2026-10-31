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
import { CLIENT_STATUS_OPTIONS } from '@/lib/constants'
import { contactTypeTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { useGeoOptions } from '@/composables/useGeoOptions'
import { restrictsAccount, type RestrictiveClientStatus } from '@/lib/clientStatus'
import { ClientStatus, type ClientSettings } from '@/types/client'
import type { ContactNotifications } from '@/types/contactnotifications'
import ClientStatusConfirmDialog from '@/components/clients/ClientStatusConfirmDialog.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'

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
  separateInvoices: false,
  disableCcProcessing: false,
  marketingOptIn: false,
  statusUpdate: true,
  allowSso: true,
})

/** Labelled switch. */
interface ToggleDef<K extends string> {
  /** Payload field the switch drives. */
  key: K
  /** Translated label. */
  label: string
}

/** Email notification switches in display order. */
const notifyToggles = computed<ToggleDef<keyof typeof notify>[]>(() => [
  { key: 'general', label: t('shared.generalEmails') },
  { key: 'invoice', label: t('shared.invoiceEmails') },
  { key: 'support', label: t('shared.supportEmails') },
  { key: 'product', label: t('shared.productEmails') },
  { key: 'domain', label: t('shared.domainEmails') },
  { key: 'affiliate', label: t('shared.affiliateEmails') },
])

/** Account setting switches in display order. */
const settingToggles = computed<ToggleDef<keyof typeof settings>[]>(() => [
  { key: 'overdueNotices', label: t('shared.overdueNotices') },
  { key: 'taxExempt', label: t('shared.taxExempt') },
  { key: 'separateInvoices', label: t('shared.separateInvoices') },
  { key: 'disableCcProcessing', label: t('shared.disableCcProcessing') },
  { key: 'marketingOptIn', label: t('shared.marketingEmailsOptIn') },
  { key: 'statusUpdate', label: t('shared.statusUpdate') },
  { key: 'allowSso', label: t('shared.allowSingleSignOn') },
])

/** Internal admin notes. */
const adminNotes = ref('')

const { countryOptions, phoneCodeOptions, resolvePhoneCode, parsePhone } = useGeoOptions()

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
    notifications: { ...notify },
    settings: { ...settings },
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
  <UiForm v-if="store.current" stacked @submit="handleSave">
    <UiStack direction="row" :justify="UiJustify.End" :align="UiAlignItems.Center" gap="sm">
      <UiAlert v-if="saveSuccess" variant="success" class="flex-1" data-testid="client-profile-saved">
        {{ t('shared.changesSavedSuccessfully') }}
      </UiAlert>
      <UiAlert v-if="saveError" variant="error" class="flex-1" data-testid="client-profile-error">
        {{ saveError }}
      </UiAlert>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-profile-cancel"
        @click="handleCancel"
      >
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        type="submit"
        :variant="UiButtonVariant.Primary"
        :disabled="saving"
        data-testid="client-profile-save"
      >
        {{ saving ? t('shared.saving') : t('shared.saveChanges') }}
      </UiButton>
    </UiStack>

    <UiGrid :columns="2">
      <UiStack>
        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientProfile.profile') }}</UiHeading>
            <UiStack direction="row" gap="lg">
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                {{ t('clients.clientProfile.clientId') }} {{ id }}
              </UiText>
              <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                {{ t('clients.clientProfile.joined') }} {{ formatDate(createdAt) }}
              </UiText>
            </UiStack>
            <UiField :label="t('shared.email')">
              <template #default="{ id: fieldId }">
                <UiInput
                  :id="fieldId"
                  v-model="email"
                  type="email"
                  :placeholder="t('shared.examples.email')"
                  data-testid="client-profile-email"
                />
              </template>
            </UiField>
            <UiGrid :columns="2" gap="sm">
              <UiField :label="t('shared.firstName')" :error="errors.firstName || undefined" required>
                <template #default="{ id: fieldId, invalid }">
                  <UiInput
                    :id="fieldId"
                    v-model="firstName"
                    type="text"
                    :invalid="invalid"
                    :placeholder="t('shared.firstName')"
                    data-testid="client-profile-first-name"
                    @input="errors.firstName = ''"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.lastName')" :error="errors.lastName || undefined" required>
                <template #default="{ id: fieldId, invalid }">
                  <UiInput
                    :id="fieldId"
                    v-model="lastName"
                    type="text"
                    :invalid="invalid"
                    :placeholder="t('shared.lastName')"
                    data-testid="client-profile-last-name"
                    @input="errors.lastName = ''"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.companyName')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="companyName"
                    type="text"
                    :placeholder="t('shared.company')"
                    data-testid="client-profile-company"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.phone')">
                <UiStack direction="row" gap="sm">
                  <UiSelect data-testid="client-profile-phone-country" v-model="phoneCountry" :options="phoneCodeOptions" :placeholder="t('shared.phone')" searchable wide />
                  <UiInput
                    v-model="phone"
                    type="text"
                    :placeholder="t('shared.phoneNumber2')"
                    data-testid="client-profile-phone"
                  />
                </UiStack>
              </UiField>
            </UiGrid>
            <UiField :label="t('shared.status')">
              <UiSelect data-testid="client-profile-status" v-model="status" :options="statusOptions" :placeholder="t('shared.status')" />
              <UiText v-if="chosenRestrictiveStatus" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-profile-status-effects">
                {{ t(`clients.clientProfile.statusEffects.${chosenRestrictiveStatus}`) }}
              </UiText>
            </UiField>
          </UiStack>
        </UiCard>

        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientProfile.billingAddress') }}</UiHeading>
            <UiGrid :columns="2" gap="sm">
              <UiField :label="t('shared.address1')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="street"
                    type="text"
                    :placeholder="t('clients.clientProfile.streetAddress')"
                    data-testid="client-profile-street"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.address2')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="address2"
                    type="text"
                    :placeholder="t('clients.clientProfile.suiteAptEtc')"
                    data-testid="client-profile-address2"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.city')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="city"
                    type="text"
                    :placeholder="t('shared.city')"
                    data-testid="client-profile-city"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.stateRegion')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="state"
                    type="text"
                    :placeholder="t('shared.state')"
                    data-testid="client-profile-state"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.postcode')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="postCode"
                    type="text"
                    :placeholder="t('shared.postcode')"
                    data-testid="client-profile-postcode"
                  />
                </template>
              </UiField>
              <UiField :label="t('shared.country')">
                <UiSelect data-testid="client-profile-country" v-model="country" :options="countryOptions" searchable wide :placeholder="t('shared.selectCountry')" />
              </UiField>
            </UiGrid>
          </UiStack>
        </UiCard>

        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('clients.clientProfile.billingPreferences') }}</UiHeading>
            <UiGrid :columns="2" gap="sm">
              <UiField :label="t('shared.currency')" :hint="currencyHint">
                <UiSelect data-testid="client-profile-currency" v-model="currency" :options="currencyOptions" :placeholder="t('shared.selectCurrency')" />
              </UiField>
              <UiField :label="t('shared.paymentMethod')">
                <template #default="{ id: fieldId }">
                  <UiInput
                    :id="fieldId"
                    v-model="paymentMethod"
                    type="text"
                    :placeholder="t('shared.creditCardBankTransfer')"
                    data-testid="client-profile-payment-method"
                  />
                </template>
              </UiField>
            </UiGrid>
            <UiField :label="t('shared.billingContact')">
              <template #default="{ id: fieldId }">
                <UiInput
                  :id="fieldId"
                  v-model="billingContact"
                  type="text"
                  :placeholder="t('clients.clientProfile.billingContactName')"
                  data-testid="client-profile-billing-contact"
                />
              </template>
            </UiField>
          </UiStack>
        </UiCard>
      </UiStack>

      <UiStack>
        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('shared.emailNotifications') }}</UiHeading>
            <UiLabel v-for="toggle in notifyToggles" :key="toggle.key" :variant="UiLabelVariant.Inline">
              <UiToggle v-model="notify[toggle.key]" :data-testid="`client-profile-notify-${toggle.key}`" />
              {{ toggle.label }}
            </UiLabel>
          </UiStack>
        </UiCard>

        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('shared.settings') }}</UiHeading>
            <UiLabel v-for="toggle in settingToggles" :key="toggle.key" :variant="UiLabelVariant.Inline">
              <UiToggle v-model="settings[toggle.key]" :data-testid="`client-profile-${toggle.key}`" />
              {{ toggle.label }}
            </UiLabel>
          </UiStack>
        </UiCard>

        <UiCard padding="md">
          <UiStack>
            <UiHeading :level="2" variant="card">{{ t('shared.adminNotes') }}</UiHeading>
            <UiTextarea
              v-model="adminNotes"
              rows="6"
              :placeholder="t('shared.internalNotesVisibleOnlyTo')"
              data-testid="client-profile-admin-notes"
            />
          </UiStack>
        </UiCard>
      </UiStack>
    </UiGrid>

    <UiCard>
      <UiStack>
        <UiHeading :level="2" variant="card">
          {{ t('clients.clientProfile.contactsLength', { length: store.current.contacts.length }) }}
        </UiHeading>
        <UiTable
          density="comfortable"
          :empty="store.current.contacts.length === 0"
          :colspan="4"
          data-testid="client-profile-contacts"
        >
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.phone') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="contact in store.current.contacts" :key="contact.id" hoverable>
              <UiTableCell>{{ contact.firstName }} {{ contact.lastName }}</UiTableCell>
              <UiTableCell muted>{{ contact.email }}</UiTableCell>
              <UiTableCell muted>{{ contact.phone || '—' }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="contactTypeTone(contact.type)" small>{{ contact.type }}</UiBadge>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
          <template #empty>
            {{ t('clients.clientProfile.noContactsAddedYet') }}
          </template>
        </UiTable>
      </UiStack>
    </UiCard>

    <ClientStatusConfirmDialog
      v-if="pendingStatus"
      :status="pendingStatus"
      :loading="saving"
      testid="client-profile-status-confirm"
      @confirm="confirmStatusChange"
      @close="pendingStatus = null"
    />
  </UiForm>
</template>
