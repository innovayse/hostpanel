<template>
  <UiCard :title="$t('client.users.currentUsers')" data-testid="account-team-list">
    <UiStack v-if="loading" gap="sm" data-testid="account-team-loading">
      <UiSkeleton v-for="index in 2" :key="index" shape="block" class="h-16" />
    </UiStack>

    <UiEmptyState
      v-else-if="!users.length"
      :icon="Users"
      :title="$t('client.users.empty')"
      data-testid="account-team-empty"
    />

    <UiList v-else list-style="divided">
      <UiListItem v-for="member in users" :key="member.userId" :data-testid="`account-team-member-${member.userId}`">
        <UiStack direction="row" align="center" justify="between" gap="sm" wrap class="py-2">
          <UiStack direction="row" align="center" gap="sm">
            <UiAvatar :first-name="member.firstName" :last-name="member.lastName" :email="member.email" />
            <UiStack gap="none">
              <UiStack direction="row" align="center" gap="xs" wrap>
                <UiText size="sm" weight="medium">{{ member.email }}</UiText>
                <UiBadge v-if="member.isOwner" variant="primary" size="sm" rounded>
                  {{ $t('client.users.owner') }}
                </UiBadge>
              </UiStack>
              <UiText v-if="member.lastLoginAt" size="xs" tone="subtle">
                {{ $t('client.users.lastLogin') }}: {{ formatDateTime(member.lastLoginAt) }}
              </UiText>
            </UiStack>
          </UiStack>
          <UiButton
            v-if="!member.isOwner"
            variant="outline"
            size="sm"
            :loading="removingUserId === member.userId"
            :data-testid="`account-team-remove-${member.userId}`"
            @click="pendingRemoval = member"
          >
            <UserMinus :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.users.removeAccess') }}
          </UiButton>
          <UiText v-else size="xs" tone="subtle" class="italic">{{ $t('client.users.ownerNote') }}</UiText>
        </UiStack>
      </UiListItem>
    </UiList>

    <UiConfirmDialog
      :open="pendingRemoval !== null"
      :title="$t('client.users.removeConfirm', { email: pendingRemoval?.email ?? '' })"
      :description="$t('client.users.removeConfirmDesc')"
      :confirm-label="$t('client.users.removeAccess')"
      :loading="removingUserId !== null"
      variant="danger"
      @confirm="onConfirmRemoval"
      @cancel="pendingRemoval = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * TeamMembersList: the people with access to the account, with a confirmed "remove access".
 *
 * The owner cannot be removed. A removal that the API refuses leaves its sentence in the
 * account store (shown by the invitation form); the dialog closes either way.
 *
 * @module components/account/TeamMembersList
 */

import { UserMinus, Users } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import type { TeamMember } from '~/types/teammember'
import { formatDateTime } from '~/utils/formatDate'

/** Props of {@link TeamMembersList}. */
interface Props {
  /** The team members to list. */
  users: TeamMember[]
  /** Whether the list is still being read. */
  loading: boolean
}

defineProps<Props>()

/** Events of {@link TeamMembersList}. */
const emit = defineEmits<{
  /** A member was removed, so the list should be read again. */
  changed: []
}>()

/** The account store that removes a member. */
const account = useAccountStore()

/** Id of the member being removed, or null. */
const { removingUserId } = storeToRefs(account)

/** The member the confirmation dialog is about, or null when it is closed. */
const pendingRemoval = ref<TeamMember | null>(null)

/** Removes the confirmed member and asks for a fresh list when it worked. */
const onConfirmRemoval = async (): Promise<void> => {
  const member = pendingRemoval.value
  if (member === null) {
    return
  }
  // A refusal lands in the store, worded by the API; the dialog closes either way.
  if (await account.removeUser(member.userId) === ActionOutcome.Succeeded) {
    emit('changed')
  }
  pendingRemoval.value = null
}
</script>
