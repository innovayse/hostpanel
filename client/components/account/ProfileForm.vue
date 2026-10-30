<template>
  <UiForm spacing="lg" :error="saveError" :success="success" data-testid="account-profile-form" @submit="onSubmit">
    <UiGrid cols="2" gap="lg">
      <UiCard :title="$t('client.profile.accountInfo')">
        <UiGrid cols="2" gap="md">
          <UiInput
            v-model="form.firstName"
            :label="$t('client.profile.firstName')"
            :placeholder="$t('client.account.placeholders.firstName')"
            :error="errors.firstName"
            size="sm"
            required
            testid="account-firstname"
          />
          <UiInput
            v-model="form.lastName"
            :label="$t('client.profile.lastName')"
            :placeholder="$t('client.account.placeholders.lastName')"
            :error="errors.lastName"
            size="sm"
            required
            testid="account-lastname"
          />
          <UiInput
            v-model="form.companyName"
            data-testid="profile-company"
            :label="$t('client.profile.companyName')"
            :placeholder="$t('client.account.placeholders.companyName')"
            :error="errors.companyName"
            size="sm"
            class="sm:col-span-2"
          />
          <UiSelect
            v-model="form.paymentMethod"
            data-testid="profile-payment-method"
            :label="$t('client.profile.paymentMethod')"
            :placeholder="$t('ui.selectPlaceholder')"
            :options="paymentOptions"
            size="sm"
            class="sm:col-span-2"
          />
          <UiSelect
            v-if="ownsLanguage" v-model="form.language"
            data-testid="profile-language"
            :label="$t('client.profile.language')"
            :placeholder="$t('ui.selectPlaceholder')"
            :options="languageOptions"
            size="sm"
            class="sm:col-span-2"
          />
        </UiGrid>
      </UiCard>

      <UiCard :title="$t('client.profile.billingAddress')">
        <UiGrid cols="2" gap="md">
          <UiInput
            v-model="form.address.street"
            data-testid="profile-address1"
            :label="$t('client.profile.address1')"
            :placeholder="$t('client.account.placeholders.address1')"
            :error="errors['address.street']"
            size="sm"
            class="sm:col-span-2"
          />
          <UiInput
            v-model="form.address.address2"
            data-testid="profile-address2"
            :label="$t('client.profile.address2')"
            :placeholder="$t('client.account.placeholders.address2')"
            :error="errors['address.address2']"
            size="sm"
            class="sm:col-span-2"
          />
          <UiInput
            v-model="form.address.city"
            data-testid="profile-city"
            :label="$t('client.profile.city')"
            :placeholder="$t('client.account.placeholders.city')"
            :error="errors['address.city']"
            size="sm"
          />
          <UiInput
            v-model="form.address.state"
            data-testid="profile-state"
            :label="$t('client.profile.state')"
            :placeholder="$t('client.account.placeholders.state')"
            :error="errors['address.state']"
            size="sm"
          />
          <UiInput
            v-model="form.address.postCode"
            data-testid="profile-postcode"
            :label="$t('client.profile.postcode')"
            :placeholder="$t('client.account.placeholders.postcode')"
            :error="errors['address.postCode']"
            size="sm"
          />
          <UiSelect
            v-model="form.address.country"
            data-testid="profile-country"
            :label="$t('client.profile.country')"
            :placeholder="$t('ui.selectPlaceholder')"
            :options="countries"
            size="sm"
          />
          <UiInput
            v-model="form.phone"
            data-testid="profile-phone"
            type="tel"
            :label="$t('client.profile.phone')"
            :placeholder="$t('client.account.placeholders.phone')"
            :error="errors.phone"
            size="sm"
            class="sm:col-span-2"
          />
        </UiGrid>
      </UiCard>
    </UiGrid>

    <UiCard :title="$t('client.profile.emailPrefs')">
      <AccountProfileEmailPrefs v-model="emailPrefs" id-prefix="edit" />
    </UiCard>

    <template #actions>
      <UiButton type="button" size="sm" variant="subtle" data-testid="account-profile-cancel" @click="emit('cancel')">
        {{ $t('client.profile.cancel') }}
      </UiButton>
      <UiButton type="submit" size="sm" :loading="saving" data-testid="account-profile-save">
        <Check v-if="!saving" :size="14" :stroke-width="2" class="mr-1.5" />
        {{ saving ? $t('client.profile.saving') : $t('client.profile.save') }}
      </UiButton>
    </template>
  </UiForm>
</template>

<script setup lang="ts">
/**
 * ProfileForm - the edit form for the profile, billing address and email preferences.
 *
 * Owns only the draft; saving goes through the account store, which keeps the refusal sentence
 * and the per-field messages this form renders. The required fields are checked before anything
 * is sent; the sign-in address is not edited here but through `EmailChangeCard`, which asks for the
 * password and a mailed confirmation; a saved draft stays open with a confirmation, and a refused
 * save leaves the form open with the reason.
 *
 * @module components/account/ProfileForm
 */

import { Check } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { useCatalogStore } from '~/stores/catalog'
import type { ClientUser } from '~/types/clientuser'
import { ActionOutcome } from '~/types/actionoutcome'
import { EmailPreference } from '~/types/emailpreference'
import type { ProfileUpdate } from '~/types/profileupdate'
import { decodeEmailPrefs } from '~/utils/emailPreferences'

/** The draft of the profile: a {@link ProfileUpdate} whose address is always present, so its inputs bind to it. */
type ProfileDraft = ProfileUpdate & { address: Required<NonNullable<ProfileUpdate['address']>> }

/** One option of a select. */
interface Option {
  /** Value stored when chosen. */
  value: string
  /** Text shown for it. */
  label: string
}

/** Props of {@link ProfileForm}. */
interface Props {
  /** The profile being edited; the draft starts from it. */
  user: ClientUser
  /** Whether this deployment stores the language (false under SSO). */
  ownsLanguage: boolean
  /** The default-payment-method choices. */
  paymentOptions: Option[]
  /** The language choices. */
  languageOptions: Option[]
}

/** The props. */
const props = defineProps<Props>()

/** Events of {@link ProfileForm}. */
const emit = defineEmits<{
  /** The edit was abandoned. */
  cancel: []
}>()

/** The account store: save action and its refusal state. */
const account = useAccountStore()
/** Save progress, the API's sentence and the per-field messages. */
const { saving, saveError, fieldErrors } = storeToRefs(account)
/** The catalogue's country list. */
const { countries: storedCountries } = storeToRefs(useCatalogStore())

/** The country choices; empty until the catalogue has loaded them. */
const countries = computed<Option[]>(() => storedCountries.value ?? [])

/** Translator for the confirmation. */
const { t } = useI18n()

/** The per-field messages returned by the API. */
const errors = computed<Record<string, string>>(() => fieldErrors.value)

/** The confirmation shown after a successful save; empty otherwise. */
const success = ref('')

/**
 * The draft of the editable fields, seeded from the profile and keyed by the backend's names, so
 * it is the save body as is and a refused field's message lands on its input.
 */
const form = reactive<ProfileDraft>({
  firstName: props.user.firstName,
  lastName: props.user.lastName,
  companyName: props.user.companyName ?? '',
  phone: props.user.phone ?? '',
  address: {
    street: props.user.address?.street ?? '',
    address2: props.user.address?.address2 ?? '',
    city: props.user.address?.city ?? '',
    state: props.user.address?.state ?? '',
    postCode: props.user.address?.postCode ?? '',
    country: props.user.address?.country ?? ''
  },
  paymentMethod: props.user.paymentMethod ?? '',
  // Sent only where this deployment stores the language; left out, the backend keeps it as is.
  language: props.ownsLanguage ? (props.user.language ?? '') : undefined
})

/** The draft of the email preferences. */
const emailPrefs = ref(decodeEmailPrefs(props.user.notifications))

// A stale sentence from an earlier attempt must not greet a fresh edit.
account.clearModalErrors()

/**
 * Checks the draft and, when it passes, saves it; a saved draft shows the confirmation.
 *
 * @returns Resolves when the save has ended, or at once when the local check refused the draft.
 */
const onSubmit = async (): Promise<void> => {
  success.value = ''
  // A refusal stays in the store as `saveError` and `fieldErrors`, worded by the API.
  const prefs = emailPrefs.value
  const profile: ProfileUpdate = {
    ...form,
    notifications: {
      general: prefs[EmailPreference.General],
      invoice: prefs[EmailPreference.Invoice],
      support: prefs[EmailPreference.Support],
      product: prefs[EmailPreference.Product],
      domain: prefs[EmailPreference.Domain],
      affiliate: prefs[EmailPreference.Affiliate]
    }
  }
  if (await account.saveProfile(profile) === ActionOutcome.Succeeded) {
    success.value = t('client.profile.saved')
  }
}
</script>
