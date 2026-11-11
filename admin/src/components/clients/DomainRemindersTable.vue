<script setup lang="ts">
/**
 * Read-only table displaying domain reminder history.
 * Shows the reminder type, the recipient and when it was sent for each reminder (the record holds
 * one timestamp, so there is one date column).
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { formatDate } from '@/lib/format'
import { DOMAIN_REMINDER_TYPE_LABEL_KEYS } from '@/types/domain'
import type { DomainReminderItem } from '@/types/domain'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for DomainRemindersTable. */
defineProps<{
  /** List of domain reminder history entries. */
  reminders: DomainReminderItem[]
}>()
</script>

<template>
  <UiCard padding="md">
    <UiStack>
      <UiHeading :level="2" variant="section">
        {{ t('clients.domainRemindersTable.domainReminderHistory') }}
      </UiHeading>
      <UiTable
        :density="UiTableDensity.Compact"
        :empty="reminders.length === 0"
        :colspan="3"
        data-testid="clients-domain-reminders-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('clients.domainRemindersTable.reminder') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.to') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.domainRemindersTable.sent') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="reminder in reminders" :key="reminder.id" hoverable>
            <UiTableCell>{{ t(DOMAIN_REMINDER_TYPE_LABEL_KEYS[reminder.reminderType]) }}</UiTableCell>
            <UiTableCell>{{ reminder.sentTo }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(reminder.sentAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('shared.noRecordsFound') }}
        </template>
      </UiTable>
    </UiStack>
  </UiCard>
</template>
