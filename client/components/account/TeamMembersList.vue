<template>
  <UiCard :title="$t('client.users.currentUsers')" data-testid="account-team-list">
    <UiTable
      scrollable
      :label="$t('client.users.currentUsers')"
      :columns="4"
      :loading="loading"
      :empty="!users.length"
      :empty-text="$t('client.users.empty')"
      :empty-icon="Users"
    >
      <template #head>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.users.colUser') }}</UiTableTh>
            <UiTableTh>{{ $t('client.users.colRole') }}</UiTableTh>
            <UiTableTh>{{ $t('client.users.lastLogin') }}</UiTableTh>
            <UiTableTh />
          </UiTableRow>
        </UiTableHead>
      </template>
      <UiTableBody>
        <UiTableRow v-for="member in users" :key="member.userId" :data-testid="`account-team-member-${member.userId}`">
          <UiTableTd>
            <UiStack direction="row" align="center" gap="sm">
              <UiAvatar :first-name="member.firstName" :last-name="member.lastName" :email="member.email" />
              <UiText :as="UiTextTag.Span" size="sm" weight="medium">{{ member.email }}</UiText>
            </UiStack>
          </UiTableTd>
          <UiTableTd>
            <UiBadge :variant="member.isOwner ? UiTone.Primary : UiTone.Default" size="sm" rounded>
              {{ member.isOwner ? $t('client.users.owner') : $t('client.users.member') }}
            </UiBadge>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ formatDateTime(member.lastLoginAt) }}</UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiButton
              v-if="!member.isOwner"
              variant="outline"
              size="sm"
              :title="$t('client.users.removeAccess')"
              :loading="removingUserId === member.userId"
              :data-testid="`account-team-remove-${member.userId}`"
              @click="pendingRemoval = member"
            >
              <UserMinus :size="14" :stroke-width="2" class="mr-1.5" />
              {{ $t('common.remove') }}
            </UiButton>
            <UiText v-else size="xs" tone="subtle" class="italic">{{ $t('client.users.ownerNote') }}</UiText>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>

    <UiConfirmDialog
      :open="pendingRemoval !== null"
      :title="$t('client.users.removeConfirm', { email: pendingRemoval?.email ?? '' })"
      :description="$t('client.users.removeConfirmDesc')"
      :confirm-label="$t('common.remove')"
      :loading="removingUserId !== null"
      variant="danger"
      @confirm="onConfirmRemoval"
      @cancel="pendingRemoval = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * TeamMembersList: the people with access to the account as a table (user, role, last login,
 * remove access), with a confirmed "remove access".
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
import { UiTextTag, UiTone } from '~/utils/ui/enums'

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
