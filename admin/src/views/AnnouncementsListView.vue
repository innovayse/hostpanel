<script setup lang="ts">
/**
 * Announcements list: every announcement with create, edit and delete actions, one page of records at a time,
 * narrowed by a search box (title or content) and by the filter dialog (published or draft) with its removable chip.
 * The search text, the filter and the page live in the address (`useListQuery`), so a reload, a shared link and the
 * back button restore the same list, and the API narrows the records before it pages. Deleting is confirmed first and
 * its outcome (success, or the API's sentence) shows above the table; a delete that empties the last page steps back to
 * the page before it. A refused load shows the sentence with a Retry.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useListQuery } from '@/composables/useListQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import { PUBLISHED_FILTER_OPTIONS } from '@/lib/constants'
import { formatDateTimeCompact } from '@/lib/format'
import { isPublishedOfFilter } from '@/lib/publishedFilter'
import { statusLabel } from '@/lib/tones'
import { useAnnouncementsStore } from '@/stores/announcements'
import type { Announcement, AnnouncementListFilters } from '@/types/announcement'
import { PublishedFilter } from '@/types/publishedfilter'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute, editPath } from '@/lib/routes'
import { UiTextSize } from '@/utils/ui/text'

const { t } = useI18n()

/** The filter field the dialog edits; it is one query key and one `AnnouncementListFilters` member. */
const FilterKey = {
  Published: 'published',
} as const

const store = useAnnouncementsStore()

/** The published filter, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The published state in the address as a filter choice, undefined when none is set or the value is not one. */
const publishedOfAddress = enumOf(FilterKey.Published, PublishedFilter)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<AnnouncementListFilters>(() => ({
  search: appliedSearch.value,
  isPublished: isPublishedOfFilter(publishedOfAddress.value),
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Published,
    label: t('shared.published'),
    value: publishedOfAddress.value === undefined ? undefined : statusLabel(PUBLISHED_FILTER_OPTIONS, publishedOfAddress.value),
  },
])

/** Published-state choices; the empty value means any. */
const publishedOptions = computed(() => [{ value: '', label: t('shared.any') }, ...PUBLISHED_FILTER_OPTIONS])

/** ID of the announcement waiting for delete confirmation; null when no dialog is open. */
const deleteTarget = ref<number | null>(null)

/** Whether a delete request is in flight. */
const deleting = ref(false)

/** The sentence of the last refused list load, shown with Retry; null when the list loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last delete. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the page the address names with the search and the filter it holds, and records whether the load was refused.
 *
 * @returns Resolves when the list has settled.
 */
const load = async (): Promise<void> => {
  loadError.value = (await store.fetchAll(page.value, filters.value)) ? null : store.error
}

// Every change of the filter, the search or the page loads the list the address names.
loadOnChange(load)

/**
 * Deletes the announcement the dialog asked about, shows the outcome and reloads the page on success, stepping back a
 * page when the delete emptied the last one.
 *
 * @returns Resolves when the delete (and the reload) has settled.
 */
const handleDelete = async (): Promise<void> => {
  if (deleteTarget.value === null) {
    return
  }
  feedback.clear()
  deleting.value = true
  const removed = await store.remove(deleteTarget.value)
  deleting.value = false
  deleteTarget.value = null
  if (removed) {
    feedback.show(t('support.announcementsList.announcementDeleted'), false)
    await load()
    await stepBackWhenEmpty(store.announcements.length === 0)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/**
 * The row actions of a announcement: edit as a real link to its page, and delete last (it opens the confirmation).
 *
 * @param announcement - The announcement of the row.
 * @returns The action list passed to UiRowActions.
 */
const rowActions = (announcement: Announcement): UiMenuAction[] => {
  /**
   * Stages this announcement for the delete confirmation.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteTarget.value = announcement.id
  }
  return [
    { label: t('shared.edit'), testid: `announcements-${announcement.id}-edit`, to: editPath(AppRoute.SupportAnnouncements, announcement.id) },
    { label: t('shared.delete'), testid: `announcements-${announcement.id}-delete`, danger: true, onSelect: onDelete },
  ]
}
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('shared.announcements')"
      :subtitle="t('support.announcementsList.manageClientFacingAnnouncements')"
    >
      <template #actions>
        <UiLink
          :to="AppRoute.SupportAnnouncementsNew"
          :variant="UiButtonVariant.Primary"
          data-testid="announcements-add"
        >
          {{ t('support.announcementsList.addNewAnnouncement') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="announcements-search"
      :search-placeholder="t('support.announcementsList.searchAnnouncements')"
      filter-testid="announcements-filter"
      :active-count="activeCount"
      :chips="chips"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('shared.published')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.published"
            :options="publishedOptions"
            :placeholder="t('shared.any')"
            data-testid="announcements-filter-published"
          />
        </template>
      </UiField>
    </UiListToolbar>

    <UiResultCount :loading="store.loading && store.announcements.length === 0" :size="UiTextSize.Sm" data-testid="announcements-count">
      {{ t('shared.totalcountRecordsFound', { totalCount: store.totalCount }) }}
    </UiResultCount>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="announcements-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="announcements-error"
      @retry="load"
    />

    <UiCard v-else>
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && store.announcements.length === 0"
        :empty="!store.loading && store.announcements.length === 0"
        data-testid="announcements-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.title') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.published') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="announcement in store.announcements"
            :key="announcement.id"
            hoverable
            :data-testid="`announcements-row-${announcement.id}`"
          >
            <UiTableCell muted>{{ formatDateTimeCompact(announcement.createdAt) }}</UiTableCell>
            <UiTableCell>{{ announcement.title }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="announcement.isPublished ? UiTone.Success : UiTone.Neutral">
                {{ announcement.isPublished ? t('shared.yes') : t('shared.no') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="rowActions(announcement)" :testid="`announcements-${announcement.id}-actions`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            v-if="narrowed"
            :title="t('shared.noRecordsFound')"
            data-testid="announcements-empty-narrowed"
          />
          <UiEmptyState
            v-else
            :title="t('support.announcementsList.noAnnouncementsFound')"
            :description="t('support.announcementsList.createYourFirstAnnouncementTo')"
          />
        </template>
      </UiTable>
    </UiCard>

    <UiPagination
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      testid="announcements-pagination"
      @update:page="setPage"
    />

    <UiDialog
      variant="confirm"
      :open="deleteTarget !== null"
      :title="t('support.announcementsList.deleteAnnouncement')"
      :message="t('support.announcementsList.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
