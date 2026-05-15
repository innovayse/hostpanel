<script setup lang="ts">
/**
 * Client contacts sub-page — lists, adds, edits, and deletes contacts for the current client.
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useClientsStore } from '../stores/clientsStore'
import { CONTACT_TYPE_STYLES } from '../../../utils/constants'
import ContactFormModal from '../components/ContactFormModal.vue'
import type { Contact } from '../../../types/models'

const route = useRoute()
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
const contactError = ref<string | null>(null)

/** Success message after a contact operation. */
const contactSuccess = ref<string | null>(null)

/** Contact type badge styles. */
const contactTypeStyles = CONTACT_TYPE_STYLES

/**
 * Shows a temporary success message.
 *
 * @param message - The message to display.
 */
function showSuccess(message: string): void {
  contactSuccess.value = message
  setTimeout(() => { contactSuccess.value = null }, 3000)
}

/**
 * Handles adding a new contact.
 *
 * @param payload - Form data from the contact modal.
 * @returns Promise that resolves when the contact is added.
 */
async function handleAddContact(payload: Record<string, unknown>): Promise<void> {
  savingContact.value = true
  contactError.value = null
  try {
    await store.addContact(clientId.value, payload)
    showCreateModal.value = false
    showSuccess('Contact added successfully.')
  } catch {
    contactError.value = 'Failed to add contact.'
  } finally {
    savingContact.value = false
  }
}

/**
 * Handles updating an existing contact.
 *
 * @param payload - Updated form data from the contact modal.
 * @returns Promise that resolves when the contact is updated.
 */
async function handleUpdateContact(payload: Record<string, unknown>): Promise<void> {
  if (!editingContact.value) return
  savingContact.value = true
  contactError.value = null
  try {
    await store.updateContact(clientId.value, editingContact.value.id, payload)
    editingContact.value = null
    showSuccess('Contact updated successfully.')
  } catch {
    contactError.value = 'Failed to update contact.'
  } finally {
    savingContact.value = false
  }
}

/**
 * Handles deleting a contact by ID with confirmation.
 *
 * @param contactId - The ID of the contact to delete.
 * @returns Promise that resolves when the contact is removed.
 */
async function handleDeleteContact(contactId: number): Promise<void> {
  if (!confirm('Are you sure you want to delete this contact? This cannot be undone.')) return
  contactError.value = null
  try {
    await store.removeContact(clientId.value, contactId)
    showSuccess('Contact deleted successfully.')
  } catch {
    contactError.value = 'Failed to delete contact.'
  }
}

/**
 * Handles the delete event from the edit modal.
 *
 * @returns Promise that resolves when the contact is removed.
 */
async function handleDeleteFromModal(): Promise<void> {
  if (!editingContact.value) return
  const id = editingContact.value.id
  editingContact.value = null
  await handleDeleteContact(id)
}

/**
 * Opens the edit modal for a given contact.
 *
 * @param contact - The contact to edit.
 */
/**
 * Opens the create contact modal and clears any edit state.
 */
function openCreateModal(): void {
  editingContact.value = null
  showCreateModal.value = true
}

/**
 * Opens the edit modal for a given contact and clears create state.
 *
 * @param contact - The contact to edit.
 */
function openEditModal(contact: Contact): void {
  showCreateModal.value = false
  editingContact.value = contact
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Feedback messages -->
    <div v-if="contactSuccess" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-green bg-status-green/10 border border-status-green/20 rounded-xl">
      {{ contactSuccess }}
    </div>
    <div v-if="contactError" class="mb-4 px-4 py-2.5 text-[0.82rem] text-status-red bg-status-red/10 border border-status-red/20 rounded-xl">
      {{ contactError }}
    </div>

    <!-- Header -->
    <div class="flex items-center justify-between mb-5">
      <h2 class="font-display font-bold text-[1.15rem] text-text-primary">
        Contacts ({{ contacts.length }})
      </h2>
      <button
        class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity hover:opacity-90"
        @click="openCreateModal"
      >
        + Add Contact
      </button>
    </div>

    <!-- Table -->
    <div v-if="contacts.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[2fr_2fr_1.5fr_1fr_0.8fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Phone</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Type</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted text-right">Actions</span>
      </div>

      <!-- Rows -->
      <div
        v-for="contact in contacts"
        :key="contact.id"
        class="grid grid-cols-1 sm:grid-cols-[2fr_2fr_1.5fr_1fr_0.8fr] gap-2 sm:gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <span class="text-[0.875rem] font-medium text-text-primary">{{ contact.firstName }} {{ contact.lastName }}</span>
        <span class="text-[0.82rem] text-text-secondary truncate">{{ contact.email }}</span>
        <span class="text-[0.82rem] text-text-muted">{{ contact.phone || '\u2014' }}</span>
        <span
          class="inline-flex w-fit px-2 py-0.5 rounded-full text-[0.65rem] font-semibold border"
          :class="contactTypeStyles[contact.type] ?? contactTypeStyles.General"
        >
          {{ contact.type }}
        </span>
        <div class="flex items-center justify-end gap-1.5">
          <button
            class="px-2.5 py-1 text-[0.75rem] font-medium text-primary-400 bg-primary-500/10 border border-primary-500/20 rounded-lg hover:bg-primary-500/15 transition-colors"
            @click="openEditModal(contact)"
          >
            Edit
          </button>
          <button
            class="px-2.5 py-1 text-[0.75rem] font-medium text-status-red bg-status-red/10 border border-status-red/20 rounded-lg hover:bg-status-red/15 transition-colors"
            @click="handleDeleteContact(contact.id)"
          >
            Delete
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3">
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M23 21v-2a4 4 0 00-3-3.87" />
          <path d="M16 3.13a4 4 0 010 7.75" />
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No contacts added yet</p>
      <button
        class="gradient-brand px-4 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity hover:opacity-90 mt-1"
        @click="openCreateModal"
      >
        + Add Contact
      </button>
    </div>

    <!-- Create Modal -->
    <ContactFormModal
      v-if="showCreateModal"
      :contact="null"
      :saving="savingContact"
      @save="handleAddContact"
      @close="showCreateModal = false"
    />

    <!-- Edit Modal -->
    <ContactFormModal
      v-if="editingContact"
      :contact="editingContact"
      :saving="savingContact"
      @save="handleUpdateContact"
      @delete="handleDeleteFromModal"
      @close="editingContact = null"
    />
  </div>
</template>
