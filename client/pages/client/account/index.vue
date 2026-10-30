<template>
  <UiStack gap="lg" data-testid="account-page">
    <UiPageHeader :title="$t('client.account.title')" :description="$t('client.account.subtitle')" />

    <UiTabs v-model="activeTab" :tabs="tabs" />

    <AccountProfileTab v-if="activeTab === AccountTab.Details" />
    <AccountContactsTab v-else-if="activeTab === AccountTab.Contacts" />
    <AccountTeamTab v-else-if="activeTab === AccountTab.Users" />
    <AccountEmailsTab v-else-if="activeTab === AccountTab.Emails" />
    <AccountPaymentMethodsTab v-else-if="activeTab === AccountTab.Payment" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Client account page: profile, contacts, team, mail history and saved payment methods.
 *
 * The page only chooses the tab and loads what the tabs read; each tab is a component under
 * `components/account/` and every change goes through the account store.
 *
 * @module pages/client/account/index
 */

import { Permission } from '~/composables/usePermissions'
import { useAccountStore } from '~/stores/account'
import { useCatalogStore } from '~/stores/catalog'
import { useClientStore } from '~/stores/client'
import { AccountTab } from '~/types/accounttab'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translator for the tab labels. */
const { t } = useI18n()

/** The current route; `?tab=` makes a tab directly linkable. */
const route = useRoute()

/** The router that keeps `?tab=` in step with the active tab. */
const router = useRouter()

/** The client store that holds the profile and knows what the signed-in person may do. */
const client = useClientStore()

/** Every tab with the permission that opens it; the details tab is for everyone. */
const allTabs = computed(() => [
  { value: AccountTab.Details, label: t('client.account.tabDetails'), permission: Permission.None },
  { value: AccountTab.Contacts, label: t('client.contacts.title'), permission: Permission.ViewManageContacts },
  // The flags have no user-management bit: the team is for a person with full access.
  { value: AccountTab.Users, label: t('client.account.tabUsers'), permission: Permission.All },
  { value: AccountTab.Emails, label: t('client.account.tabEmails'), permission: Permission.ViewEmails },
  // Always listed: a person without the invoices permission opens the tab and reads why it is
  // disabled, instead of the tab silently missing (the tab itself loads and offers nothing then).
  { value: AccountTab.Payment, label: t('client.account.tabPayment'), permission: Permission.None }
])

/** The tab strip: only the tabs the person may open. */
const tabs = computed(() => allTabs.value.filter(tab => client.mayDo(tab.permission)))

/**
 * The tab named by the query, or the details tab when it is missing, unknown or not permitted.
 *
 * @returns The tab to open first.
 */
const initialTab = (): AccountTab => {
  const requested = tabs.value.find(tab => tab.value === route.query.tab)
  return requested?.value ?? AccountTab.Details
}

/** The active tab. */
const activeTab = ref<AccountTab>(initialTab())

// Switching tabs rewrites the query so the address can be shared or reloaded.
watch(activeTab, (tab) => {
  router.replace({ query: { ...route.query, tab } })
})

/** The account store that holds every tab's list. */
const account = useAccountStore()

/** The catalogue store that holds the country list the forms offer. */
const catalog = useCatalogStore()

await useAsyncData('client-user', () => client.fetchUser())
await useAsyncData('client-account', () => Promise.all([
  catalog.loadCountries(),
  account.loadGatewayMethods(),
  account.loadUsers(),
  account.loadContacts(),
  account.loadEmails(),
  account.loadPaymentMethods()
]))
</script>
