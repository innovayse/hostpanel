<script setup lang="ts">
/**
 * Client contacts sub-page: lists, adds, edits, and deletes contacts for the current client.
 */
import { ref, computed, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import { contactTypeTone } from '@/lib/tones'
import ContactFormModal from '@/components/clients/ContactFormModal.vue'
import type { Contact } from '@/types/contact'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiAlign } from '@/utils/ui/table'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Clients store, owner of the loaded client and its contact actions. */
const store = useClientsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Contacts from the loaded client detail. */
const contacts = computed(() => store.current?.contacts ?? [])

/** The contact being edited, null when not editing. */
const editingContact = ref<Contact | null>(null)

/** Whether the create modal is visible. */
const showCreateModal = ref(false)

/** True while a contact save or delete request is in flight. */
const savingContact = ref(false)

/** Error message for contact operations, null when no error. */
const contactError = computed(() => store.contactError)

/** Success message after a contact operation. */
const contactSuccess = ref<string | null>(null)

/** Timer that clears the success message. */
let successTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Shows a temporary success message.
 *
 * @param message - The message to display.
 * @returns Nothing.
 */
const showSuccess = (message: string): void => {
  contactSuccess.value = message
  clearTimeout(successTimer)
  successTimer = setTimeout(() => {
    contactSuccess.value = null
  }, NOTICE_VISIBLE_MS)
}

/**
 * Handles adding a new contact.
 *
 * @param payload - Form data from the contact modal.
 * @returns Promise that resolves when the contact is added.
 */
const handleAddContact = async (payload: Record<string, unknown>): Promise<void> => {
  savingContact.value = true
  const added = await store.addContact(clientId.value, payload)
  savingContact.value = false
  if (added) {
    showCreateModal.value = false
    showSuccess(t('clients.clientContacts.contactAddedSuccessfully'))
  }
}

/**
 * Handles updating an existing contact.
 *
 * @param payload - Updated form data from the contact modal.
 * @returns Promise that resolves when the contact is updated.
 */
const handleUpdateContact = async (payload: Record<string, unknown>): Promise<void> => {
  if (!editingContact.value) {
    return
  }
  savingContact.value = true
  const updated = await store.updateContact(clientId.value, editingContact.value.id, payload)
  savingContact.value = false
  if (updated) {
    editingContact.value = null
    showSuccess(t('clients.clientContacts.contactUpdatedSuccessfully'))
  }
}

/** ID of the contact awaiting delete confirmation, null when nothing is staged. */
const deleteContactTarget = ref<number | null>(null)

/** True while the staged contact is being deleted. */
const deletingContact = ref(false)

/**
 * Stages a contact for deletion. Only opens the prompt: {@link confirmDeleteContact} does the
 * work, so the question is asked by the app's own modal rather than the browser's dialog.
 *
 * @param contactId - The ID of the contact to delete.
 * @returns Nothing.
 */
const handleDeleteContact = (contactId: number): void => {
  deleteContactTarget.value = contactId
}

/**
 * Deletes the staged contact and clears the prompt.
 *
 * The target is cleared whether or not the store accepted the delete, so a failure closes the
 * prompt and leaves the reason on the inline error banner, which is where every other action on
 * this page reports one.
 *
 * @returns Promise that resolves when the contact is removed.
 */
const confirmDeleteContact = async (): Promise<void> => {
  const contactId = deleteContactTarget.value
  if (contactId === null) {
    return
  }
  deletingContact.value = true
  const removed = await store.removeContact(clientId.value, contactId)
  deletingContact.value = false
  deleteContactTarget.value = null
  if (removed) {
    showSuccess(t('clients.clientContacts.contactDeletedSuccessfully'))
  }
}

/**
 * Handles the delete event from the edit modal.
 *
 * Closes the edit modal first, then stages the same confirmation the row action uses: the edit
 * modal has already asked once, so this is the second half of a prompt chain that predates the
 * move off `confirm()` and is preserved exactly.
 *
 * @returns Nothing.
 */
const handleDeleteFromModal = (): void => {
  if (!editingContact.value) {
    return
  }
  const id = editingContact.value.id
  editingContact.value = null
  handleDeleteContact(id)
}

/**
 * Opens the create contact modal and clears any edit state.
 *
 * @returns Nothing.
 */
const openCreateModal = (): void => {
  editingContact.value = null
  showCreateModal.value = true
}

/**
 * Opens the edit modal for a given contact and clears create state.
 *
 * @param contact - The contact to edit.
 * @returns Nothing.
 */
const openEditModal = (contact: Contact): void => {
  showCreateModal.value = false
  editingContact.value = contact
}

onBeforeUnmount(() => {
  clearTimeout(successTimer)
})

/**
 * Row actions for a contact.
 *
 * @param contact - The contact whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const contactActions = (contact: Contact): UiRowAction[] => {
  /**
   * Opens the edit dialog for this contact.
   * @returns Nothing.
   */
  const onEdit = (): void => openEditModal(contact)
  /**
   * Forwards delete for this contact.
   * @returns Nothing.
   */
  const onDelete = (): void => handleDeleteContact(contact.id)
  return [
    { label: t('shared.edit'), testid: `client-contacts-edit-${contact.id}`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `client-contacts-delete-${contact.id}`, onSelect: onDelete, danger: true },
  ]
}
</script>

<template>
  <UiStack>
    <UiAlert v-if="contactSuccess" variant="success" data-testid="client-contacts-success">
      {{ contactSuccess }}
    </UiAlert>
    <UiAlert v-if="contactError" variant="error" data-testid="client-contacts-error">
      {{ contactError }}
    </UiAlert>

    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="2" variant="section">
        {{ t('clients.clientContacts.contactsLength', { length: contacts.length }) }}
      </UiHeading>
      <UiButton
        :variant="UiButtonVariant.Primary"
        data-testid="client-contacts-add"
        @click="openCreateModal"
      >
        {{ t('clients.clientContacts.addContact') }}
      </UiButton>
    </UiStack>

    <UiCard v-if="contacts.length > 0" class="overflow-x-auto">
      <UiTable density="comfortable" data-testid="client-contacts-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.phone') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader :align="UiAlign.Right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="contact in contacts" :key="contact.id" hoverable>
            <UiTableCell>{{ contact.firstName }} {{ contact.lastName }}</UiTableCell>
            <UiTableCell muted>{{ contact.email }}</UiTableCell>
            <UiTableCell muted>{{ contact.phone || '—' }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="contactTypeTone(contact.type)" small>{{ contact.type }}</UiBadge>
            </UiTableCell>
            <UiTableCell :align="UiAlign.Right">
              <UiRowActions :actions="contactActions(contact)" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('clients.clientContacts.noContactsAddedYet')">
        <template #actions>
          <UiButton
            :variant="UiButtonVariant.Primary"
            data-testid="client-contacts-add-empty"
            @click="openCreateModal"
          >
            {{ t('clients.clientContacts.addContact') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>

    <ContactFormModal
      v-if="showCreateModal"
      :contact="null"
      :saving="savingContact"
      @save="handleAddContact"
      @close="showCreateModal = false"
    />

    <ContactFormModal
      v-if="editingContact"
      :contact="editingContact"
      :saving="savingContact"
      @save="handleUpdateContact"
      @delete="handleDeleteFromModal"
      @close="editingContact = null"
    />

    <UiDialog
      v-if="deleteContactTarget !== null"
      variant="confirm"
      :title="t('clients.clientContacts.deleteContact')"
      :message="t('clients.clientContacts.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deletingContact"
      tone="danger"
      @confirm="confirmDeleteContact"
      @close="deleteContactTarget = null"
    />
  </UiStack>
</template>
