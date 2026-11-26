<template>
  <UiCard data-testid="account-contacts-tab">
    <UiCardHeader :title="$t('client.contacts.title')">
      <UiButton size="sm" variant="subtle" data-testid="account-contact-add" @click="openAdd">
        <Plus :size="12" :stroke-width="2" class="mr-1" />
        {{ $t('client.contacts.add') }}
      </UiButton>
    </UiCardHeader>

    <UiTable
      scrollable
      :label="$t('client.contacts.title')"
      :columns="5"
      :loading="contactsLoading"
      :empty="!contacts.length"
      :empty-text="$t('client.contacts.empty')"
      :empty-description="$t('client.contacts.emptyDesc')"
      :empty-icon="Users"
    >
      <template #head>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.contacts.colName') }}</UiTableTh>
            <UiTableTh>{{ $t('client.contacts.colCompany') }}</UiTableTh>
            <UiTableTh>{{ $t('client.contacts.colEmail') }}</UiTableTh>
            <UiTableTh>{{ $t('client.contacts.colPhone') }}</UiTableTh>
            <UiTableTh actions />
          </UiTableRow>
        </UiTableHead>
      </template>
      <template #empty>
        <UiButton data-testid="contacts-add" size="sm" variant="subtle" @click="openAdd">{{ $t('client.contacts.add') }}</UiButton>
      </template>
      <UiTableBody>
        <UiTableRow v-for="contact in contacts" :key="contact.id" :data-testid="`account-contact-${contact.id}`">
          <UiTableTd>
            <UiStack direction="row" align="center" gap="sm">
              <UiAvatar :first-name="contact.firstName" :last-name="contact.lastName" :email="contact.email" />
              <UiText :as="UiTextTag.Span" size="sm" weight="medium">{{ contact.firstName }} {{ contact.lastName }}</UiText>
            </UiStack>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ contact.companyName || EMPTY_DATE }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm">{{ contact.email }}</UiText>
          </UiTableTd>
          <UiTableTd>
            <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ contact.phone || EMPTY_DATE }}</UiText>
          </UiTableTd>
          <UiTableTd align="right">
            <UiStack direction="row" align="center" justify="end" gap="xs">
              <UiButton size="sm" variant="subtle" :data-testid="`account-contact-edit-${contact.id}`" @click="openEdit(contact)">
                <Pencil :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('common.edit') }}
              </UiButton>
              <UiButton size="sm" variant="subtle" :data-testid="`account-contact-delete-${contact.id}`" @click="pendingDelete = contact">
                <Trash2 :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('common.delete') }}
              </UiButton>
            </UiStack>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>

    <AccountContactDialog v-model="dialogOpen" :contact="editing" @saved="account.loadContacts()" />

    <UiConfirmDialog
      :open="pendingDelete !== null"
      :title="$t('client.contacts.deleteConfirm', { name: `${pendingDelete?.firstName ?? ''} ${pendingDelete?.lastName ?? ''}` })"
      :description="$t('client.contacts.deleteConfirmDesc')"
      :confirm-label="$t('common.delete')"
      :loading="deleting"
      variant="danger"
      @confirm="onConfirmDelete"
      @cancel="pendingDelete = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ContactsTab. The "Contacts" tab: the account's sub-contacts as a table with add, edit and delete.
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
import { EMPTY_DATE } from '~/utils/formatDate'
import { UiTextTag } from '~/utils/ui/enums'

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
