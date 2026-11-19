<script setup lang="ts">
/**
 * Fraud block lists: one tab per list (IP address, IP range, email domain, country), a form that
 * adds an entry with an optional note, and a searched, paged table whose rows can be deleted after a
 * confirmation.
 *
 * The tab strip, the add card's captions and the head of the table are the ones of `FraudBlockListsView.schema`, which
 * `UiSkeletonOf` also draws while the view downloads.
 *
 * The tab, the search text and the page live in the address (`useListQuery`), so a reload or a shared link
 * restores the same list. Switching tab returns to the first page and empties the search, because a search
 * belongs to the list it was typed in.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BlockListAddForm from '@/components/fraud/BlockListAddForm.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { useListQuery } from '@/composables/useListQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDateTime } from '@/lib/format'
import { useFraudBlockListStore } from '@/stores/fraudBlockList'
import { BlockListType } from '@/types/fraud'
import type { BlockListEntry } from '@/types/fraud'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { BLOCK_LIST_TABLE, BLOCK_LIST_TABS_PART } from '@/views/FraudBlockListsView.schema'

const { t } = useI18n()

/** Block list store: the page, the total and the refusal sentences. */
const store = useFraudBlockListStore()

/** The result banner for an add or a delete. */
const banner = useResultBanner()

/** The query key the tab is kept under. */
const FilterKey = {
  Type: 'type',
} as const

/** The tab, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, apply, setPage, loadOnChange, stepBackWhenEmpty, enumOf } = useListQuery(Object.values(FilterKey))

/** The type in the address as a block list type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, BlockListType)

/** The list on show: the one in the address, the IP list when it names none. */
const type = computed<BlockListType>(() => typeOfAddress.value ?? BlockListType.Ip)

/** Whether the list is narrowed by a search. */
const searching = computed<boolean>(() => appliedSearch.value !== '')

/** The value being typed into the add form. */
const value = ref('')

/** The note being typed into the add form. */
const note = ref('')

/** True while an add is in flight. */
const adding = ref(false)

/** The entry waiting for a delete confirmation, null when the dialog is closed. */
const deleteTarget = ref<BlockListEntry | null>(null)

/** True while a delete is in flight. */
const deleting = ref(false)

/** The tabs, one per list, as the schema's strip names them. */
const tabs = computed<UiTabItem[]>(() =>
  tabItemsOfSchema(BLOCK_LIST_TABS_PART.tabs, t, 'fraud-block-list-tab-'),
)

/**
 * Reads the page of the list the address names, with the search it holds.
 *
 * @returns True when read; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchPage(page.value, { type: type.value, search: appliedSearch.value })

// Every change of the tab, the search or the page loads the list the address names.
loadOnChange(load)

/** True while the first load is in flight and there are no rows yet; the toolbar then keeps its layout. */
const initialLoading = computed<boolean>(() => store.loading && store.entries.length === 0)

/**
 * Shows the first page, where a newly added entry (newest first) is.
 *
 * @returns Promise that resolves when the first page is read.
 */
const showFirstPage = async (): Promise<void> => {
  if (page.value > 1) {
    // The address changes, so the list loads itself.
    await setPage(1)
  } else {
    await load()
  }
}

/**
 * Reads the current page again after a change; a page left empty by a delete steps back one page.
 *
 * @returns Promise that resolves when the page is read.
 */
const reload = async (): Promise<void> => {
  await load()
  await stepBackWhenEmpty(store.entries.length === 0)
}

/**
 * Switches to another list, with an empty form, an empty search and no refusal left over from the last one.
 *
 * @param next - The value of the tab the operator chose.
 * @returns Promise that resolves when the address names the new list.
 */
const selectType = async (next: string): Promise<void> => {
  // The tab values come from this very list, so finding the one that was chosen also gives the typed member.
  const chosen = Object.values(BlockListType).find((member) => member === next)
  if (chosen === undefined) {
    return
  }
  value.value = ''
  note.value = ''
  store.clearAction()
  // The tab and the emptied search box reach the address in one step, so the new list loads once, from its first page.
  draft.value.type = chosen
  search.value = ''
  await apply()
}

/**
 * Adds the typed value to the list on show and shows how it went.
 *
 * @returns Promise that resolves when the add and the following read are over.
 */
const addEntry = async (): Promise<void> => {
  adding.value = true
  const trimmedNote = note.value.trim()
  const added = await store.add({ type: type.value, value: value.value.trim(), note: trimmedNote === '' ? null : trimmedNote })
  adding.value = false
  if (added) {
    value.value = ''
    note.value = ''
    banner.show(t('fraud.blockList.added'), false)
    await showFirstPage()
  } else if (store.actionError && Object.keys(store.fieldErrors).length === 0) {
    // A refusal that names a field is shown under that field; any other refusal has no place but the banner.
    banner.show(store.actionError, true)
  }
}

/** Removes the confirmed entry and shows how it went. */
const confirmDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  deleting.value = true
  const removed = await store.remove(deleteTarget.value.id)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    banner.show(t('fraud.blockList.removed'), false)
    await reload()
  } else if (store.actionError) {
    banner.show(store.actionError, true)
  }
}

/**
 * The one row action of an entry: delete it, after a confirmation.
 *
 * @param entry - The row.
 * @returns The action list passed to UiRowActions.
 */
const entryActions = (entry: BlockListEntry): UiMenuAction[] => [
  {
    label: t('shared.delete'),
    icon: Icon.Trash,
    danger: true,
    testid: `fraud-block-list-delete-${entry.id}`,
    /**
     * Opens the confirmation for this entry.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      deleteTarget.value = entry
    },
  },
]

</script>

<template>
  <UiStack data-testid="fraud-block-lists-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? UiTone.Danger : UiTone.Success"
      data-testid="fraud-block-list-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <UiTabs :items="tabs" :model-value="type" :aria-label="t('fraud.blockList.tabsLabel')" @update:model-value="selectType" />

    <BlockListAddForm
      v-model:value="value"
      v-model:note="note"
      :type="type"
      :adding="adding"
      :errors="store.fieldErrors"
      @add="addEntry"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="fraud-block-list-search"
      :search-placeholder="t('fraud.blockList.searchPlaceholder')"
      :loading="initialLoading"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="fraud-block-list-error"
      @retry="load"
    />

    <UiResultCount :loading="initialLoading" data-testid="fraud-block-list-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiResultCount>

    <UiCard clip>
      <UiTable
        :empty-text="searching ? t('fraud.blockList.noMatches') : t('fraud.blockList.empty')"
        :loading="initialLoading"
        :empty="!store.loading && store.entries.length === 0"
        data-testid="fraud-block-list-table"
      >
        <UiSchemaTableHead :columns="BLOCK_LIST_TABLE.columns" />
        <UiTableBody divided>
          <UiTableRow v-for="entry in store.entries" :key="entry.id" hoverable :data-testid="`fraud-block-list-row-${entry.id}`">
            <UiTableCell class="font-mono">{{ entry.value }}</UiTableCell>
            <UiTableCell muted>{{ entry.note ?? t('shared.notAvailable') }}</UiTableCell>
            <UiTableCell muted>{{ formatDateTime(entry.createdAt) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="entryActions(entry)" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="fraud-block-list-pagination"
      @update:page="setPage"
    />

    <UiDialog
      v-if="deleteTarget"
      variant="confirm"
      :title="t('fraud.blockList.deleteTitle')"
      :message="t('fraud.blockList.deleteMessage', { value: deleteTarget.value })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="deleteTarget = null"
    />
  </UiStack>
</template>
