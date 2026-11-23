<script setup lang="ts">
/**
 * Read-only table displaying domain reminder history.
 * Shows the reminder type, the recipient and when it was sent for each reminder (the record holds
 * one timestamp, so there is one date column).
 */
import { useI18n } from 'vue-i18n'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiColumnSize } from '@/utils/ui/table'
import { UiHeadingVariant } from '@/utils/ui/text'
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
  <UiDataTable
    :empty-text="t('shared.noRecordsFound')"
    :density="UiTableDensity.Compact"
    :empty="reminders.length === 0"
    data-testid="clients-domain-reminders-table"
  >
    <template #header>
      <UiHeading :level="2" :variant="UiHeadingVariant.Section">{{ t('clients.domainRemindersTable.domainReminderHistory') }}</UiHeading>
    </template>
    <UiTableHead>
      <UiTableRow>
        <UiTableHeader :size="UiColumnSize.Grow">{{ t('clients.domainRemindersTable.reminder') }}</UiTableHeader>
        <UiTableHeader>{{ t('shared.to') }}</UiTableHeader>
        <UiTableHeader :size="UiColumnSize.Compact">{{ t('clients.domainRemindersTable.sent') }}</UiTableHeader>
      </UiTableRow>
    </UiTableHead>
    <UiTableBody>
      <UiTableRow v-for="reminder in reminders" :key="reminder.id" hoverable>
        <UiTableCell>{{ t(DOMAIN_REMINDER_TYPE_LABEL_KEYS[reminder.reminderType]) }}</UiTableCell>
        <UiTableCell>{{ reminder.sentTo }}</UiTableCell>
        <UiTableCell muted>{{ formatDate(reminder.sentAt) }}</UiTableCell>
      </UiTableRow>
    </UiTableBody>
  </UiDataTable>
</template>
