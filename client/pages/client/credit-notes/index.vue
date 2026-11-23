<template>
  <UiStack gap="lg" data-testid="client-credit-notes">
    <UiPageHeader :title="$t('client.creditNotes.title')" :description="$t('client.creditNotes.subtitle')" :icon="FileMinus" class="!mb-0" />

    <UiAlert variant="info" data-testid="credit-notes-explain">{{ $t('client.creditNotes.explain') }}</UiAlert>

    <UiAlert v-if="store.error" variant="error" data-testid="credit-notes-error">{{ store.error }}</UiAlert>

    <template v-else>
      <UiAlert v-if="store.downloadError" variant="error" data-testid="credit-notes-download-error">{{ store.downloadError }}</UiAlert>
      <ClientCreditnotesTable :notes="store.notes" :downloading-id="store.downloadingId" :loading="store.loading" @download="store.downloadPdf" />
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Credit notes: the documents issued to the client that reverse part of an invoice they already paid, each downloadable as a PDF.
 *
 * A credit note is not invoice credit (the balance the client spends on an invoice, on the Credit page). Only the caller's own notes
 * are listed: the backend takes the client from the session. Loading, empty and failure states come from the credit notes store.
 */
import { FileMinus } from 'lucide-vue-next'
import { useCreditNotesStore } from '~/stores/creditnotes'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** The store that owns the notes and their loading and failure state. */
const store = useCreditNotesStore()

await useAsyncData('client-credit-notes', () => store.load())
</script>
