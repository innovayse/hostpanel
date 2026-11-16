<script setup lang="ts">
/**
 * The Advanced table: every setting no card owns, one editable value per row.
 *
 * Cards own the keys of {@link CARD_OWNED_KEYS}; this table lists the rest, with the setting's own
 * description as the help text. The keys carry their area as a prefix (`billing.`, `fraud.`, `portal.`),
 * so the key column already tells the areas apart and the rows need no extra headings.
 * Edits are drafts of the surrounding settings page, saved from its sticky bar.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import RawSettingRow from '@/components/settings/RawSettingRow.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { CARD_OWNED_KEYS } from '@/lib/settingKeyOwnership'
import type { Setting } from '@/types/setting'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for RawSettingsTable. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** True while the settings are read for the first time. */
  loading?: boolean
}>()

/** The settings no card owns, in the order they were loaded. */
const rows = computed<Setting[]>(() => props.settings.filter((setting) => !CARD_OWNED_KEYS.has(setting.key)))
</script>

<template>
  <UiCard class="overflow-hidden" data-testid="raw-settings-table">
    <UiTable :density="UiTableDensity.Comfortable" :empty="rows.length === 0" :loading="loading" :colspan="3">
      <template #empty>
        {{ t('settings.table.noSettings') }}
      </template>
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader>{{ t('settings.table.key') }}</UiTableHeader>
          <UiTableHeader>{{ t('settings.table.value') }}</UiTableHeader>
          <UiTableHeader>{{ t('settings.table.description') }}</UiTableHeader>
        </UiTableRow>
      </UiTableHead>
      <UiTableBody divided>
        <RawSettingRow v-for="setting in rows" :key="setting.id" :settings="settings" :setting-key="setting.key" />
      </UiTableBody>
    </UiTable>
  </UiCard>
</template>
