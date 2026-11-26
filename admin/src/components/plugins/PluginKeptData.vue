<script setup lang="ts">
/**
 * Lists removed plugins whose data the server kept, one table row each with a "Purge data" action.
 *
 * Removing a plugin keeps its settings and stored data so a later install picks up where it left
 * off; purging deletes that data for good. The action only asks: the screen confirms first.
 */
import { useI18n } from 'vue-i18n'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiColumnSize } from '@/utils/ui/table'
import { UiTextTag, UiTextTone, UiHeadingVariant, UiTextSize } from '@/utils/ui/text'
import { Icon } from '@/lib/icons'
import { UiGap } from '@/utils/ui/layout'

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
const keptActions = (id: string): UiMenuAction[] => {
  /**
   * Asks the screen to purge this plugin's data (it confirms first).
   *
   * @returns Nothing.
   */
  const onPurge = (): void => {
    emit('purge', id)
  }
  return [{
    label: t('plugins.keptData.purge'),
    icon: Icon.Trash,
    testid: `plugin-purge-${id}`,
    danger: true,
    disabled: props.purgingId !== null,
    onSelect: onPurge,
  }]
}
</script>

<template>
  <UiStack :gap="UiGap.Sm" data-testid="plugins-kept-data">
    <UiHeading :level="2" :variant="UiHeadingVariant.Section">{{ t('plugins.keptData.title') }}</UiHeading>
    <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('plugins.keptData.description') }}</UiText>
    <UiDataTable data-testid="plugins-kept-data-table">
      <UiTableHead>
        <UiTableRow>
          <UiTableHeader :size="UiColumnSize.Grow">{{ t('shared.id') }}</UiTableHeader>
          <UiTableHeader actions />
        </UiTableRow>
      </UiTableHead>
      <UiTableBody>
        <UiTableRow v-for="id in ids" :key="id" hoverable :data-testid="`plugin-kept-${id}`">
          <UiTableCell>
            <UiText :as="UiTextTag.Span" :tone="UiTextTone.Primary" class="font-mono">{{ id }}</UiText>
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="keptActions(id)" :testid="`plugin-kept-actions-${id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>
  </UiStack>
</template>
