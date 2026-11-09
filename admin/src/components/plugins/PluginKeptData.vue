<script setup lang="ts">
/**
 * Lists removed plugins whose data the server kept, one table row each with a "Purge data" action.
 *
 * Removing a plugin keeps its settings and stored data so a later install picks up where it left
 * off; purging deletes that data for good. The action only asks: the screen confirms first.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiTableDensity } from '@/utils/ui/injection'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginKeptData. */
const props = defineProps<{
  /** Ids of the removed plugins whose data is still kept. */
  ids: string[]
  /** Id of the plugin whose purge is in flight, null when none. */
  purgingId: string | null
}>()

/** Events emitted by PluginKeptData. */
const emit = defineEmits<{
  /** Emitted with the plugin id when the admin asks to purge its data. */
  purge: [id: string]
}>()

/**
 * The row actions of a removed plugin: its one destructive action, purge, blocked while any purge runs.
 *
 * @param id - Id of the removed plugin.
 * @returns The action list passed to UiRowActions.
 */
const keptActions = (id: string): UiRowAction[] => {
  /**
   * Asks the screen to purge this plugin's data (it confirms first).
   *
   * @returns Nothing.
   */
  const onPurge = (): void => {
    emit('purge', id)
  }
  return [{
    label: props.purgingId === id ? t('plugins.keptData.purging') : t('plugins.keptData.purge'),
    testid: `plugin-purge-${id}`,
    danger: true,
    disabled: props.purgingId !== null,
    onSelect: onPurge,
  }]
}
</script>

<template>
  <UiStack gap="sm" data-testid="plugins-kept-data">
    <UiHeading :level="2" variant="section">{{ t('plugins.keptData.title') }}</UiHeading>
    <UiText tone="muted" size="sm">{{ t('plugins.keptData.description') }}</UiText>
    <UiCard class="overflow-hidden">
      <UiTable :density="UiTableDensity.Comfortable" data-testid="plugins-kept-data-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="id in ids" :key="id" hoverable :data-testid="`plugin-kept-${id}`">
            <UiTableCell class="font-mono text-text-primary">{{ id }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="keptActions(id)" :testid="`plugin-kept-actions-${id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>
  </UiStack>
</template>
