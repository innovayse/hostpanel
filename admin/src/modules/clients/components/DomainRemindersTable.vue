<script setup lang="ts">
/**
 * Read-only table displaying domain reminder history.
 * Shows date, reminder type, recipient, and sent timestamp for each reminder.
 */
import { formatDate } from '../../../utils/format'
import type { DomainReminderItem } from '../../../types/models'

defineProps<{
  /** List of domain reminder history entries. */
  reminders: DomainReminderItem[]
}>()
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl p-5">

    <!-- Section header -->
    <h2 class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-4">Domain Reminder History</h2>

    <!-- Table -->
    <div class="overflow-hidden">

      <!-- Header row -->
      <div class="hidden sm:grid grid-cols-[1fr_1.5fr_1.5fr_0.8fr] gap-3 px-3 py-2 border-b border-border bg-white/[0.02] rounded-t-xl">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Date</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Reminder</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">To</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Sent</span>
      </div>

      <!-- Data rows -->
      <div
        v-for="reminder in reminders"
        :key="reminder.id"
        class="grid grid-cols-1 sm:grid-cols-[1fr_1.5fr_1.5fr_0.8fr] gap-2 sm:gap-3 px-3 py-2.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
      >
        <span class="text-[0.82rem] text-text-muted">{{ formatDate(reminder.sentAt) }}</span>
        <span class="text-[0.82rem] text-text-primary">{{ reminder.reminderType }}</span>
        <span class="text-[0.82rem] text-text-secondary truncate">{{ reminder.sentTo }}</span>
        <span class="text-[0.82rem] text-text-muted">{{ formatDate(reminder.sentAt) }}</span>
      </div>

      <!-- Empty state -->
      <div v-if="reminders.length === 0" class="py-8 text-center">
        <p class="text-[0.82rem] text-text-muted">No Records Found</p>
      </div>
    </div>
  </div>
</template>
