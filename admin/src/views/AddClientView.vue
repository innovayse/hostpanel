<script setup lang="ts">
/**
 * Full-page form for creating a new client account.
 *
 * All sections are displayed on a single scrollable page in a two-column layout.
 * Navigates back to the clients list on success. Creating a client as Suspended or Closed is first
 * put to the operator, with the effects of that status spelled out.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import type { AdminCreateClientPayload } from '@/types/admincreateclientpayload'
import type { ClientSettings } from '@/types/client'
import type { UserSearchHit } from '@/types/clientuser'
import type { ContactNotifications } from '@/types/contactnotifications'
import { useClientsStore } from '@/stores/clients'
import { CLIENT_STATUS_OPTIONS, LANGUAGE_OPTIONS } from '@/lib/constants'
import { ClientStatus } from '@/types/client'
import { restrictsAccount, type RestrictiveClientStatus } from '@/lib/clientStatus'
import { useGeoOptions } from '@/composables/useGeoOptions'
import ClientGroupSelect from '@/components/clients/ClientGroupSelect.vue'
import { CLIENT_TAX_EXEMPTION_REASON_MAX_LENGTH, CLIENT_TAX_ID_MAX_LENGTH } from '@/lib/limits'
import ClientStatusConfirmDialog from '@/components/clients/ClientStatusConfirmDialog.vue'
import ClientUserPicker from '@/components/clients/ClientUserPicker.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiPhoneInput from '@/components/ui/UiPhoneInput.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiLabelVariant } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { EXISTING_OWNER_SECTION, OWNER_SECTION_KEY } from '@/views/AddClientView.schema'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('shared.cancel'), icon: actionIcons[UiActionKind.Cancel], variant: UiButtonVariant.Secondary, testid: 'add-client-cancel', onSelect: goBack },
  { label: t('clients.addClient.createClient'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'add-client-submit', loading: saving.value, onSelect: handleSubmit },
])

/** Router, used to leave for the clients list. */
const router = useRouter()

/** Clients store, owner of the user search, currencies and creation. */
const store = useClientsStore()

/** True while the save request is in flight. */
const saving = ref(false)

/** Error message shown when client creation fails. */
const saveError = ref<string | null>(null)

/** Whether to create a new user or associate an existing one. */
const createNewUser = ref(true)

/** Email for new user creation. */
const email = ref('')

/** Password for new user creation. */
const password = ref('')

/** Selected existing user, null until one is picked. */
const selectedUser = ref<UserSearchHit | null>(null)

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

const { countryOptions, resolvePhoneCode } = useGeoOptions()

/** Resolves the selected country's phone code for the payload. */
const selectedPhoneCode = computed(() => resolvePhoneCode(phoneCountry.value))

/** Preferred language code. */
const language = ref('')

/** Client status. */
const status = ref<ClientStatus>(ClientStatus.Active)

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

/** Selected client group id; null lets the API put the client in the default group. */
const clientGroupId = ref<number | null>(null)

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

/** The columns of the form: the schema's, with the owner card of the chosen way of making the account owner. */
const columns = computed(() =>
  schema.columns.map(column =>
    column.map(section => (section.key === OWNER_SECTION_KEY && !createNewUser.value ? EXISTING_OWNER_SECTION : section)),
  ),
)

/** Internal admin notes. */
const adminNotes = ref('')

/**
 * Switches to creating a new user and drops any picked one.
 *
 * @returns Nothing.
 */
const chooseNewUser = (): void => {
  createNewUser.value = true
  selectedUser.value = null
}

/**
 * Switches to an existing user and drops the new-user credentials.
 *
 * @returns Nothing.
 */
const chooseExistingUser = (): void => {
  createNewUser.value = false
  email.value = ''
  password.value = ''
}

/** Currency options for the UiSelect dropdown. */
const currencyOptions = computed(() =>
  [{ value: '', label: t('shared.selectCurrency') }, ...store.currencies.map(c => ({ value: c.code, label: `${c.name} (${c.symbol})` }))],
)

/** Language options for the dropdown. */
const languageOptions = LANGUAGE_OPTIONS

/** Status options for the dropdown. */
const statusOptions = CLIENT_STATUS_OPTIONS

/**
 * Leaves for the clients list.
 *
 * @returns Nothing.
 */
const goBack = (): void => {
  router.push(AppRoute.Clients)
}

/** The restrictive status awaiting the operator's confirmation, null when no prompt is open. */
const pendingStatus = ref<RestrictiveClientStatus | null>(null)

/**
 * Builds the payload and submits to the API; a refusal fills `store.fieldErrors`.
 *
 * @returns Promise that resolves when the create attempt has finished.
 */
const createClient = async (): Promise<void> => {
  const payload: AdminCreateClientPayload = {
    createNewUser: createNewUser.value,
    existingUserId: !createNewUser.value ? selectedUser.value?.id : undefined,
    email: createNewUser.value ? email.value.trim() : undefined,
    password: createNewUser.value ? password.value : undefined,
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    companyName: companyName.value.trim() || undefined,
    phone: phone.value.trim() ? `${selectedPhoneCode.value} ${phone.value.trim()}` : undefined,
    address: {
      street: street.value.trim() || undefined,
      address2: address2.value.trim() || undefined,
      city: city.value.trim() || undefined,
      state: state.value.trim() || undefined,
      postCode: postCode.value.trim() || undefined,
      country: country.value || undefined,
    },
    preferences: {
      currency: currency.value || undefined,
      language: language.value || undefined,
      paymentMethod: paymentMethod.value.trim() || undefined,
      billingContact: billingContact.value.trim() || undefined,
      adminNotes: adminNotes.value.trim() || undefined,
    },
    status: status.value,
    clientGroupId: clientGroupId.value,
    notifications: { ...notify },
    settings: {
      ...settings,
      taxId: settings.taxId?.trim() || null,
      taxExemptionReason: settings.taxExemptionReason?.trim() || null,
    },
  }

  saving.value = true
  const created = await store.createClient(payload)
  saving.value = false
  if (created) {
    router.push(AppRoute.Clients)
  } else {
    saveError.value = store.saveError
  }
}

/**
 * Submits the form. Creating the client as Suspended or Closed is first put to the operator, because
 * both change what the client and their services can do.
 *
 * @returns Promise that resolves when the create attempt has finished or the prompt is open.
 */
const handleSubmit = async (): Promise<void> => {
  if (restrictsAccount(status.value)) {
    pendingStatus.value = status.value
    return
  }

  await createClient()
}

/**
 * Confirms the staged status and creates the client.
 *
 * @returns Promise that resolves when the create attempt has finished.
 */
const confirmStatus = async (): Promise<void> => {
  pendingStatus.value = null
  await createClient()
}

onMounted(() => {
  // Drop field errors left by another screen sharing the store.
  store.fieldErrors = {}
  store.fetchCurrencies()
})
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions"
      v-bind="schemaHeader(schema, t)"
      :back-to="AppRoute.Clients"
      :back-label="t('shared.clients')"
      back-mobile-only
      back-testid="add-client-back"
    >
    </UiPageHeader>

    <UiAlert v-if="saveError" :variant="UiTone.Danger" data-testid="add-client-error">
      {{ saveError }}
    </UiAlert>

    <UiSchemaForm :columns="columns" :errors="store.fieldErrors" @submit="handleSubmit">
      <template #field-ownerNew>
        <UiLabel :variant="UiLabelVariant.Inline">
          <UiRadio
            :model-value="createNewUser"
            :value="true"
            data-testid="add-client-owner-new"
            @update:model-value="chooseNewUser"
          />
          {{ t('clients.addClient.createANewUser') }}
        </UiLabel>
      </template>
      <template #field-ownerExisting>
        <UiLabel :variant="UiLabelVariant.Inline">
          <UiRadio
            :model-value="createNewUser"
            :value="false"
            data-testid="add-client-owner-existing"
            @update:model-value="chooseExistingUser"
          />
          {{ t('clients.addClient.associateWithAnExistingUser') }}
        </UiLabel>
      </template>
      <template #field-email="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="email"
          type="email"
          :invalid="invalid"
          :placeholder="t('shared.examples.email')"
          data-testid="add-client-email"
        />
      </template>
      <template #field-password="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="password"
          type="password"
          :invalid="invalid"
          :placeholder="t('clients.addClient.min8Characters')"
          data-testid="add-client-password"
        />
      </template>
      <template #field-existingUserId="{ id, invalid }">
        <ClientUserPicker
          v-model="selectedUser"
          :search="store.searchUsers"
          :invalid="invalid"
          :input-id="id"
          testid="add-client-user"
        />
      </template>

      <template #field-firstName="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="firstName"
          type="text"
          :invalid="invalid"
          :placeholder="t('shared.john')"
          data-testid="add-client-first-name"
        />
      </template>
      <template #field-lastName="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="lastName"
          type="text"
          :invalid="invalid"
          :placeholder="t('shared.doe')"
          data-testid="add-client-last-name"
        />
      </template>
      <template #field-companyName="{ id }">
        <UiInput
          :id="id"
          v-model="companyName"
          type="text"
          :placeholder="t('shared.acmeInc')"
          data-testid="add-client-company"
        />
      </template>
      <template #field-phone>
        <UiPhoneInput
          v-model:phone-number="phone"
          v-model:country-code="phoneCountry"
          data-testid="add-client-phone"
        />
      </template>
      <template #field-language>
        <UiSelect data-testid="add-client-language" v-model="language" :options="languageOptions" :placeholder="t('shared.default')" />
      </template>
      <template #field-status>
        <UiSelect data-testid="add-client-status" v-model="status" :options="statusOptions" :placeholder="t('shared.status')" />
      </template>
      <template #field-clientGroupId="{ id }">
        <ClientGroupSelect
          :id="id"
          v-model="clientGroupId"
          :empty-label="t('clients.clientGroups.defaultGroup')"
          data-testid="add-client-group"
        />
      </template>

      <template #field-street="{ id }">
        <UiInput
          :id="id"
          v-model="street"
          type="text"
          :placeholder="t('clients.addClient.123MainStreet')"
          data-testid="add-client-street"
        />
      </template>
      <template #field-address2="{ id }">
        <UiInput
          :id="id"
          v-model="address2"
          type="text"
          :placeholder="t('shared.suite100')"
          data-testid="add-client-address2"
        />
      </template>
      <template #field-city="{ id }">
        <UiInput
          :id="id"
          v-model="city"
          type="text"
          :placeholder="t('shared.newYork')"
          data-testid="add-client-city"
        />
      </template>
      <template #field-state="{ id }">
        <UiInput
          :id="id"
          v-model="state"
          type="text"
          :placeholder="t('shared.examples.state')"
          data-testid="add-client-state"
        />
      </template>
      <template #field-postCode="{ id }">
        <UiInput
          :id="id"
          v-model="postCode"
          type="text"
          :placeholder="t('shared.examples.postalCode')"
          data-testid="add-client-postcode"
        />
      </template>
      <template #field-country>
        <UiSelect data-testid="add-client-country" v-model="country" :options="countryOptions" :placeholder="t('shared.selectCountry')" />
      </template>
      <template #field-currency>
        <UiSelect data-testid="add-client-currency" v-model="currency" :options="currencyOptions" :placeholder="t('shared.selectCurrency')" />
      </template>
      <template #field-paymentMethod="{ id }">
        <UiInput
          :id="id"
          v-model="paymentMethod"
          type="text"
          :placeholder="t('shared.creditCardBankTransfer')"
          data-testid="add-client-payment-method"
        />
      </template>
      <template #field-billingContact="{ id }">
        <UiInput
          :id="id"
          v-model="billingContact"
          type="text"
          :placeholder="t('clients.addClient.johnDoe')"
          data-testid="add-client-billing-contact"
        />
      </template>

      <template #field-notifyGeneral>
        <UiToggle v-model="notify.general" data-testid="add-client-notify-general" />
      </template>
      <template #field-notifyInvoice>
        <UiToggle v-model="notify.invoice" data-testid="add-client-notify-invoice" />
      </template>
      <template #field-notifySupport>
        <UiToggle v-model="notify.support" data-testid="add-client-notify-support" />
      </template>
      <template #field-notifyProduct>
        <UiToggle v-model="notify.product" data-testid="add-client-notify-product" />
      </template>
      <template #field-notifyDomain>
        <UiToggle v-model="notify.domain" data-testid="add-client-notify-domain" />
      </template>
      <template #field-notifyAffiliate>
        <UiToggle v-model="notify.affiliate" data-testid="add-client-notify-affiliate" />
      </template>

      <template #field-overdueNotices>
        <UiToggle v-model="settings.overdueNotices" data-testid="add-client-overdueNotices" />
      </template>
      <template #field-taxExempt>
        <UiToggle v-model="settings.taxExempt" data-testid="add-client-taxExempt" />
      </template>
      <template #field-separateInvoices>
        <UiToggle v-model="settings.separateInvoices" data-testid="add-client-separateInvoices" />
      </template>
      <template #field-disableCcProcessing>
        <UiToggle v-model="settings.disableCcProcessing" data-testid="add-client-disableCcProcessing" />
      </template>
      <template #field-marketingOptIn>
        <UiToggle v-model="settings.marketingOptIn" data-testid="add-client-marketingOptIn" />
      </template>
      <template #field-statusUpdate>
        <UiToggle v-model="settings.statusUpdate" data-testid="add-client-statusUpdate" />
      </template>
      <template #field-allowSso>
        <UiToggle v-model="settings.allowSso" data-testid="add-client-allowSso" />
      </template>
      <template #field-taxId="{ id }">
        <UiInput
          :id="id"
          v-model="settings.taxId"
          type="text"
          :maxlength="CLIENT_TAX_ID_MAX_LENGTH"
          :placeholder="t('shared.taxIdPlaceholder')"
          data-testid="add-client-tax-id"
        />
      </template>
      <template #field-taxExemptionReason="{ id }">
        <UiInput
          :id="id"
          v-model="settings.taxExemptionReason"
          type="text"
          :maxlength="CLIENT_TAX_EXEMPTION_REASON_MAX_LENGTH"
          :placeholder="t('shared.taxExemptionReasonPlaceholder')"
          data-testid="add-client-tax-exemption-reason"
        />
      </template>

      <template #field-adminNotes="{ id }">
        <UiTextarea
          :id="id"
          v-model="adminNotes"
          rows="4"
          :placeholder="t('shared.internalNotesVisibleOnlyTo')"
          data-testid="add-client-admin-notes"
        />
      </template>
    </UiSchemaForm>

    <ClientStatusConfirmDialog
      v-if="pendingStatus"
      :status="pendingStatus"
      :loading="saving"
      testid="add-client-status-confirm"
      @confirm="confirmStatus"
      @close="pendingStatus = null"
    />
  </UiStack>
</template>
