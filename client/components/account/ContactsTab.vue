<template>
  <UiCard data-testid="account-contacts-tab">
    <UiCardHeader :title="$t('client.contacts.title')">
      <UiButton size="sm" variant="subtle" data-testid="account-contact-add" @click="openAdd">
        <Plus :size="12" :stroke-width="2" class="mr-1" />
        {{ $t('client.contacts.addNew') }}
      </UiButton>
    </UiCardHeader>

    <UiStack v-if="contactsLoading" gap="sm" data-testid="account-contacts-loading">
      <UiSkeleton v-for="index in 2" :key="index" shape="block" class="h-16" />
    </UiStack>

    <UiEmptyState
      v-else-if="!contacts.length"
      :icon="Users"
      :title="$t('client.contacts.empty')"
      :description="$t('client.contacts.emptyDesc')"
    >
      <UiButton data-testid="contacts-add" size="sm" variant="subtle" @click="openAdd">{{ $t('client.contacts.addNew') }}</UiButton>
    </UiEmptyState>

    <UiList v-else list-style="divided">
      <UiListItem v-for="contact in contacts" :key="contact.id" :data-testid="`account-contact-${contact.id}`">
        <UiStack direction="row" align="center" justify="between" gap="sm" wrap class="py-2">
          <UiStack direction="row" align="center" gap="sm">
            <UiAvatar :first-name="contact.firstName" :last-name="contact.lastName" :email="contact.email" />
            <UiStack gap="none">
              <UiStack direction="row" align="center" gap="xs" wrap>
                <UiText size="sm" weight="medium">{{ contact.firstName }} {{ contact.lastName }}</UiText>
                <UiText v-if="contact.companyName" size="sm" tone="subtle">· {{ contact.companyName }}</UiText>
              </UiStack>
              <UiText size="xs" tone="subtle">
                {{ contact.email }}<template v-if="contact.phone"> · {{ contact.phone }}</template>
              </UiText>
            </UiStack>
          </UiStack>
          <UiStack direction="row" align="center" gap="xs">
            <UiButton size="sm" variant="subtle" :data-testid="`account-contact-edit-${contact.id}`" @click="openEdit(contact)">
              <Pencil :size="13" :stroke-width="2" class="mr-1" />
              {{ $t('client.profile.edit') }}
            </UiButton>
            <UiButton size="sm" variant="subtle" :data-testid="`account-contact-delete-${contact.id}`" @click="pendingDelete = contact">
              <Trash2 :size="13" :stroke-width="2" class="mr-1" />
              {{ $t('client.contacts.delete') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiListItem>
    </UiList>

    <AccountContactDialog v-model="dialogOpen" :contact="editing" @saved="account.loadContacts()" />

    <UiConfirmDialog
      :open="pendingDelete !== null"
      :title="$t('client.contacts.deleteConfirm', { name: `${pendingDelete?.firstName ?? ''} ${pendingDelete?.lastName ?? ''}` })"
      :description="$t('client.contacts.deleteConfirmDesc')"
      :confirm-label="$t('client.contacts.delete')"
      :loading="deleting"
      variant="danger"
      @confirm="onConfirmDelete"
      @cancel="pendingDelete = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ContactsTab. The "Contacts" tab: the account's sub-contacts with add, edit and delete.
 *
 * The list lives in the account store. Add and edit share one dialog; delete asks first.
 *
 * @module components/account/ContactsTab
 */

import { Pencil, Plus, Trash2, Users } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { ActionOutcome } from '~/types/actionoutcome'
import type { AccountContact } from '~/types/accountcontact'

/** The account store that holds and changes the contacts. */
const account = useAccountStore()

/** The contacts and whether they are being read. */
const { contacts, contactsLoading } = storeToRefs(account)

/** Whether the add/edit dialog is open. */
const dialogOpen = ref(false)

/** The contact the dialog edits, or null when it adds. */
const editing = ref<AccountContact | null>(null)

/** The contact the delete confirmation is about, or null when it is closed. */
const pendingDelete = ref<AccountContact | null>(null)

/** Whether a deletion is in flight. */
const deleting = ref(false)

/** Opens the dialog for a new contact. */
const openAdd = (): void => {
  editing.value = null
  dialogOpen.value = true
}

/**
 * Opens the dialog for an existing contact.
 *
 * @param contact - The contact to edit.
 */
const openEdit = (contact: AccountContact): void => {
  editing.value = contact
  dialogOpen.value = true
}

/** Deletes the confirmed contact and reads the list again when it worked. */
const onConfirmDelete = async (): Promise<void> => {
  const contact = pendingDelete.value
  if (contact === null) {
    return
  }
  deleting.value = true
  if (await account.removeContact(contact.id) === ActionOutcome.Succeeded) {
    await account.loadContacts()
  }
  deleting.value = false
  pendingDelete.value = null
}
</script>
