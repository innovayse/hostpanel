<template>
  <UiStack gap="xl" data-testid="account-team-tab">
    <AccountTeamMembersList :users="users" :loading="usersLoading" @changed="account.loadUsers()" />
    <AccountInviteUserForm @invited="account.loadUsers()" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * TeamTab. The "User Management" tab: who has access, and the form to invite someone.
 *
 * Both halves ask this tab for a fresh list after they change it; the list itself lives in the
 * account store.
 *
 * @module components/account/TeamTab
 */

import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'

/** The account store that holds the team list. */
const account = useAccountStore()

/** The team members and whether they are being read. */
const { users, usersLoading } = storeToRefs(account)
</script>
