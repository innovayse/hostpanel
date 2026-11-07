<template>
  <div data-testid="account-profile-tab">
    <!-- Only while there is no profile to show: a refresh after a save keeps the page (and the edit
         form, whose confirmation would otherwise be lost with the form that is unmounted). -->
    <UiStack v-if="userLoading && !user" gap="md" data-testid="account-profile-loading">
      <UiSkeleton shape="block" class="h-24" />
      <UiSkeleton shape="block" class="h-64" />
    </UiStack>

    <UiStack v-else-if="user" gap="lg">
      <AccountProfileSummary :user="user" :two-factor-enabled="twoFactorEnabled" />

      <AccountProfileForm
        v-if="editing"
        :user="user"
        :owns-language="ownsLanguage"
        :payment-options="paymentOptions"
        :language-options="languageOptions"
        @cancel="editing = false"
      />

      <template v-else>
        <UiStack v-if="canEdit" direction="row" justify="end">
          <UiButton size="sm" variant="subtle" data-testid="account-profile-edit" @click="editing = true">
            <Pencil :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.profile.edit') }}
          </UiButton>
        </UiStack>
        <AccountProfileView
          :user="user"
          :payment-method-label="paymentMethodLabel"
          :language-label="languageLabel"
          :owns-language="ownsLanguage"
          :email-prefs="emailPrefs"
        />
        <AccountEmailChangeCard v-if="canEdit && ownsAccount" :email="user.email" />
        <AccountTwoFactorCard :enabled="twoFactorEnabled" />
      </template>
    </UiStack>

    <UiEmptyState
      v-else
      :icon="AlertCircle"
      :title="$t('client.profile.failedToLoad')"
      data-testid="account-profile-failed"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * ProfileTab - the "Account Details" tab: summary, details or edit form, the sign-in address and two-factor.
 *
 * Reads the profile from the client store and the gateways from the account store; it derives
 * the human wording (default payment method, language) once and hands it to the view and the
 * form, and owns the single edit/view switch.
 *
 * @module components/account/ProfileTab
 */

import { AlertCircle, Pencil } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { APP_LOCALES } from '~/constants/appLocales'
import { Permission } from '~/types/permission'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import { AppLocale } from '~/types/applocale'
import { decodeEmailPrefs } from '~/utils/emailPreferences'

/** Translator for the derived labels. */
const { t } = useI18n()
/** Whether the deployment signs in through the SSO. */
const { isSso } = useAuthMode()
/** The client store that holds the profile. */
const client = useClientStore()
/** The profile and whether it is loading. */
const { user, userLoading } = storeToRefs(client)
/** The gateways the default payment method can name. */
const { gatewayMethods } = storeToRefs(useAccountStore())

/** Whether the person may change the master profile; without it the details are read-only. */
const canEdit = computed<boolean>(() => client.mayDo(Permission.ModifyMasterProfile))

/** Whether the edit form is showing instead of the read-only details. */
const editing = ref(false)

/**
 * Whether this deployment owns the account the language is stored on.
 *
 * Under `AUTH_MODE=sso` the SSO owns the person and so their chosen language: this portal can
 * neither read one back nor write one. The field is therefore not offered at all rather than
 * shown as a control whose value never changes.
 */
const ownsLanguage = computed<boolean>(() => !isSso.value)

/**
 * Whether this deployment owns the account's sign-in address. Under SSO the SSO owns it, so
 * changing it is not offered here at all.
 */
const ownsAccount = computed<boolean>(() => !isSso.value)

/** Whether two-factor is on; read from the profile the client store already holds. */
const twoFactorEnabled = computed<boolean>(() => user.value?.twoFactorEnabled ?? false)

/** The choices for the default payment method: the deployment default, then each gateway. */
const paymentOptions = computed(() => [
  { value: '', label: t('client.profile.paymentDefault') },
  ...(gatewayMethods.value ?? []).map(gateway => ({ value: gateway.module, label: gateway.displayName }))
])

/** Locale key of each language's display name in the profile's language list. */
const LANGUAGE_LABEL_KEYS: Record<AppLocale, string> = {
  [AppLocale.En]: 'client.profile.langEnglish',
  [AppLocale.Ru]: 'client.profile.langRussian',
  [AppLocale.Hy]: 'client.profile.langArmenian'
}

/**
 * The languages the backend accepts, keyed by the codes in `LocaleOptions.SupportedLocales`.
 * One vocabulary end to end: a language word such as "english" matches no supported locale and
 * would be served as English by accident.
 */
const languageOptions = computed(() => [
  { value: '', label: t('client.profile.langDefault') },
  ...APP_LOCALES.map(code => ({ value: code, label: t(LANGUAGE_LABEL_KEYS[code]) }))
])

/** The default payment method as a name a person reads. */
const paymentMethodLabel = computed<string>(() => {
  const gateway = user.value?.paymentMethod
  if (!gateway) {
    return t('client.profile.paymentDefault')
  }
  return (gatewayMethods.value ?? []).find(method => method.module === gateway)?.displayName ?? gateway
})

/**
 * The stored language as a name a person reads, resolved through the same option list the
 * dropdown renders rather than by capitalising the code, which would print `en` as "En".
 */
const languageLabel = computed<string>(() => {
  const language = user.value?.language
  if (!language) {
    return t('client.profile.langDefault')
  }
  return languageOptions.value.find(option => option.value === language)?.label ?? language
})

/** The decoded email preferences of the stored profile. */
const emailPrefs = computed(() => decodeEmailPrefs(user.value?.notifications))
</script>
