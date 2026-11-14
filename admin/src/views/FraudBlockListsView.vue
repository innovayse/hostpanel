<script setup lang="ts">
/**
 * Fraud block lists: one tab per list (IP address, IP range, email domain, country), a form that
 * adds an entry with an optional note, and a paged table whose rows can be deleted after a
 * confirmation.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BlockListAddForm from '@/components/fraud/BlockListAddForm.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { BLOCK_LIST_TYPE_OPTIONS } from '@/lib/constants'
import { enumValueOf } from '@/lib/enumValue'
import { formatDateTime } from '@/lib/format'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { useFraudBlockListStore } from '@/stores/fraudBlockList'
import { BlockListType } from '@/types/fraud'
import type { BlockListEntry } from '@/types/fraud'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Block list store: the page, the total and the refusal sentences. */
const store = useFraudBlockListStore()

/** The result banner for an add or a delete. */
const banner = useResultBanner()

/** The list on show. */
const type = ref<BlockListType>(BlockListType.Ip)

/** Current page (1-based). */
const page = ref(1)

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

/** The tabs, one per list. */
const tabs = computed<UiTabItem[]>(() =>
  BLOCK_LIST_TYPE_OPTIONS.map((option) => ({ value: option.value, label: option.label, testid: `fraud-block-list-tab-${option.value}` })),
)

/**
 * Reads a page of the list on show.
 *
 * @param next - Page number (1-based).
 * @returns Promise that resolves when the page is read.
 */
const load = async (next: number): Promise<void> => {
  page.value = next
  await store.fetchPage(type.value, next, DEFAULT_PAGE_SIZE)
}

/**
 * Reads the current page again after a change; a page left empty by a delete steps back one page.
 *
 * @returns Promise that resolves when the page is read.
 */
const reload = async (): Promise<void> => {
  await load(page.value)
  if (store.entries.length === 0 && page.value > 1) {
    await load(page.value - 1)
  }
}

/**
 * Switches to another list, with an empty form and no refusal left over from the last one.
 *
 * @param next - The value of the tab the operator chose.
 * @returns Promise that resolves when the first page of that list is read.
 */
const selectType = async (next: string): Promise<void> => {
  const chosen = enumValueOf(BlockListType, next)
  if (chosen === null) {
    return
  }
  type.value = chosen
  value.value = ''
  note.value = ''
  store.clearAction()
  await load(1)
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
    await load(1)
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

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="fraud-block-lists-view">
    <UiPageHeader :title="t('fraud.blockList.title')" :subtitle="t('fraud.blockList.subtitle')" />

    <UiAlert
      v-if="banner.message.value"
      :variant="banner.isError.value ? UiTone.Danger : UiTone.Success"
      data-testid="fraud-block-list-result"
    >
      {{ banner.message.value }}
    </UiAlert>

    <UiTabs :items="tabs" :model-value="type" :aria-label="t('fraud.blockList.tabsLabel')" @update:model-value="selectType" />

    <UiCard padding="md">
      <BlockListAddForm
        v-model:value="value"
        v-model:note="note"
        :type="type"
        :adding="adding"
        :errors="store.fieldErrors"
        @add="addEntry"
      />
    </UiCard>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="fraud-block-list-error"
      @retry="load(page)"
    />

    <UiResultCount :loading="store.loading && store.entries.length === 0" tone="muted" size="md" data-testid="fraud-block-list-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiResultCount>

    <UiCard class="overflow-hidden">
      <UiTable
        :loading="store.loading && store.entries.length === 0"
        :empty="!store.loading && store.entries.length === 0"
        data-testid="fraud-block-list-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.value') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.blockList.note') }}</UiTableHeader>
            <UiTableHeader>{{ t('fraud.blockList.addedAt') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
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
        <template #empty>
          {{ t('fraud.blockList.empty') }}
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="DEFAULT_PAGE_SIZE"
      testid="fraud-block-list-pagination"
      @update:page="load"
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
