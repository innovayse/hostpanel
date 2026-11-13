<template>
  <UiCard :title="$t('client.users.inviteTitle')" data-testid="account-invite-card">
    <UiText size="sm" tone="muted" class="mb-6">{{ $t('client.users.inviteDesc') }}</UiText>
    <UiForm :error="inviteError" :success="success" spacing="md" data-testid="account-invite-form" @submit="onSubmit">
      <!-- Both names are asked for because the invitation carries them: the API rejects an
           invite without them, and inventing a placeholder here would put a made-up name on
           the account until the invitee happened to correct it. -->
      <UiGrid cols="2" gap="md">
        <UiInput
          v-model="firstName"
          :label="$t('client.users.firstNameLabel')"
          :placeholder="$t('client.account.placeholders.inviteFirstName')"
          required
          testid="account-invite-firstname"
        />
        <UiInput
          v-model="lastName"
          :label="$t('client.users.lastNameLabel')"
          :placeholder="$t('client.account.placeholders.inviteLastName')"
          required
          testid="account-invite-lastname"
        />
      </UiGrid>
      <UiInput
        v-model="email"
        type="email"
        :label="$t('client.users.emailLabel')"
        :placeholder="$t('client.account.placeholders.inviteEmail')"
        required
        testid="account-invite-email"
      />

      <UiStack gap="sm">
        <UiText size="sm" weight="medium">{{ $t('client.users.permissions') }}</UiText>
        <UiStack direction="row" gap="lg">
          <UiRadio
            v-model="mode"
            name="invite-permissions"
            :value="InvitePermissionMode.All"
            :label="$t('client.users.permAll')"
            testid="account-invite-perm-all"
          />
          <UiRadio
            v-model="mode"
            name="invite-permissions"
            :value="InvitePermissionMode.Choose"
            :label="$t('client.users.permChoose')"
            testid="account-invite-perm-choose"
          />
        </UiStack>

        <!-- Granular permissions, offered only when "choose" is selected. -->
        <UiStack v-if="mode === InvitePermissionMode.Choose" gap="sm" data-testid="account-invite-perms">
          <UiCheckbox
            v-for="perm in permissionOptions"
            :id="`perm-${perm.key}`"
            :key="perm.key"
            v-model="chosen[perm.key]"
            :label="perm.label"
            :description="perm.description"
            :testid="`account-invite-perm-${perm.key}`"
          />
        </UiStack>
      </UiStack>

      <template #actions>
        <UiButton type="submit" :loading="inviting" data-testid="account-invite-submit">
          <Send v-if="!inviting" :size="15" :stroke-width="2" class="mr-1.5" />
          {{ $t('client.users.sendInvite') }}
        </UiButton>
      </template>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * InviteUserForm: invites a person to the account with all or chosen permissions.
 *
 * Sending goes through the account store; on success the form empties, says so, and asks the
 * tab to read the team list again. A refusal (also one from removing a member) is the store's
 * `inviteError`.
 *
 * @module components/account/InviteUserForm
 */

import { Send } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import { InvitePermissionMode } from '~/types/invitepermissionmode'
import { TeamPermission } from '~/types/teampermission'

/** Events of {@link InviteUserForm}. */
const emit = defineEmits<{
  /** An invitation went out, so the team list should be read again. */
  invited: []
}>()

/** Translator for the permission labels and the success sentence. */
const { t } = useI18n()

/** The account store that sends the invitation. */
const account = useAccountStore()

/** Sending progress and the API's refusal sentence. */
const { inviting, inviteError } = storeToRefs(account)

/** The invitee's email. */
const email = ref('')

/** The invitee's first name. */
const firstName = ref('')

/** The invitee's last name. */
const lastName = ref('')

/** Whether every permission is granted or only the ticked ones. */
const mode = ref<InvitePermissionMode>(InvitePermissionMode.All)

/** Success sentence after an invitation went out; empty otherwise. */
const success = ref('')

/**
 * Builds a record with every permission set to the same state.
 *
 * @param state - Whether every permission is granted.
 * @returns One entry per {@link TeamPermission}.
 */
const allPermissions = (state: boolean): Record<TeamPermission, boolean> => {
  const record = {} as Record<TeamPermission, boolean>
  for (const key of Object.values(TeamPermission)) {
    record[key] = state
  }
  return record
}

/** Which permissions are ticked while "choose" is selected. */
const chosen = reactive<Record<TeamPermission, boolean>>(allPermissions(false))

/** The grantable permissions with their translated label and description. */
const permissionOptions = computed(() => [
  { key: TeamPermission.Profile, label: t('client.users.permProfile'), description: t('client.users.permProfileDesc') },
  { key: TeamPermission.Contacts, label: t('client.users.permContacts'), description: t('client.users.permContactsDesc') },
  { key: TeamPermission.Products, label: t('client.users.permProducts'), description: t('client.users.permProductsDesc') },
  { key: TeamPermission.Passwords, label: t('client.users.permPasswords'), description: t('client.users.permPasswordsDesc') },
  { key: TeamPermission.Sso, label: t('client.users.permSso'), description: t('client.users.permSsoDesc') },
  { key: TeamPermission.Domains, label: t('client.users.permDomains'), description: t('client.users.permDomainsDesc') },
  { key: TeamPermission.DomainSettings, label: t('client.users.permDomainMgmt'), description: t('client.users.permDomainMgmtDesc') },
  { key: TeamPermission.Invoices, label: t('client.users.permInvoices'), description: t('client.users.permInvoicesDesc') },
  { key: TeamPermission.Quotes, label: t('client.users.permQuotes'), description: t('client.users.permQuotesDesc') },
  { key: TeamPermission.Tickets, label: t('client.users.permTickets'), description: t('client.users.permTicketsDesc') },
  { key: TeamPermission.Affiliate, label: t('client.users.permAffiliate'), description: t('client.users.permAffiliateDesc') },
  { key: TeamPermission.Emails, label: t('client.users.permEmails'), description: t('client.users.permEmailsDesc') },
  { key: TeamPermission.Orders, label: t('client.users.permOrders'), description: t('client.users.permOrdersDesc') }
])

/** Sends the invitation; on success empties the form and asks for a fresh team list. */
const onSubmit = async (): Promise<void> => {
  success.value = ''
  const permissions = mode.value === InvitePermissionMode.All
    ? []
    : Object.values(TeamPermission).filter(key => chosen[key])
  const outcome = await account.inviteUser({
    email: email.value,
    firstName: firstName.value,
    lastName: lastName.value,
    permissionMode: mode.value,
    permissions
  })
  if (outcome === ActionOutcome.Succeeded) {
    success.value = t('client.users.inviteSent')
    email.value = ''
    firstName.value = ''
    lastName.value = ''
    mode.value = InvitePermissionMode.All
    Object.assign(chosen, allPermissions(false))
    emit('invited')
  }
}
</script>
