<script setup lang="ts">
/**
 * Predefined replies management: tabs for categories, reply templates and a search over the
 * templates. A refused load offers a retry; the outcome of each add, rename, delete or search
 * (success, or the API's sentence) shows under the tabs; deletes are confirmed in a danger prompt.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { enumValueOf } from '@/lib/enumValue'
import { usePredefinedRepliesStore } from '@/stores/predefinedReplies'
import type { PredefinedReply, PredefinedReplyCategory } from '@/types/predefinedreply'
import { PredefinedRepliesTab } from '@/types/supporttab'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiSize } from '@/utils/ui/size'
import type { UiTabItem } from '@/utils/ui/tabs'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = usePredefinedRepliesStore()

/** Number of columns of the category table, spanned by the row of a category under edit. */
const CATEGORY_COLUMN_COUNT = 2

/** Number of columns of the reply table, spanned by the row of a reply under edit. */
const REPLY_COLUMN_COUNT = 3

/** The outcome line of the last add, rename, delete or search. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of a change: the success sentence, or the API's refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/**
 * Loads the categories and the replies.
 *
 * @returns Resolves when both loads have settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([store.fetchCategories(), store.fetchReplies()])
}

/** Currently active tab. */
const activeTab = ref<PredefinedRepliesTab>(PredefinedRepliesTab.Category)

/** Tab items for the kit's tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: PredefinedRepliesTab.Category, label: t('shared.addCategory'), testid: 'predefined-replies-tab-category' },
  { value: PredefinedRepliesTab.Reply, label: t('support.predefinedReplies.addPredefinedReply'), testid: 'predefined-replies-tab-reply' },
  { value: PredefinedRepliesTab.Search, label: t('shared.searchFilter'), testid: 'predefined-replies-tab-search' },
])

/** New category name input. */
const newCategoryName = ref('')

/** Whether a category is being saved. */
const categorySaving = ref(false)

/** ID of the category being edited in place; null when none. */
const editingCategoryId = ref<number | null>(null)

/** Name being typed for the category under edit. */
const editingCategoryName = ref('')

/** ID of the category waiting for delete confirmation; null when no dialog is open. */
const deleteCategoryTarget = ref<number | null>(null)

/** Whether a category deletion is in progress. */
const deletingCategory = ref(false)

/** New reply name input. */
const newReplyName = ref('')

/** New reply category selection (0 = none chosen). */
const newReplyCategoryId = ref<number>(0)

/** New reply content. */
const newReplyContent = ref('')

/** Whether a reply is being saved. */
const replySaving = ref(false)

/** ID of the reply waiting for delete confirmation; null when no dialog is open. */
const deleteReplyTarget = ref<number | null>(null)

/** Whether a reply deletion is in progress. */
const deletingReply = ref(false)

/** ID of the reply being edited in place; null when none. */
const editingReplyId = ref<number | null>(null)

/** Fields of the reply under edit. */
const editingReply = ref({ name: '', categoryId: 0, content: '' })

/** Search query string. */
const searchQuery = ref('')

/** Search results. */
const searchResults = ref<PredefinedReply[]>([])

/** Whether a search is in progress. */
const searching = ref(false)

/** Debounce timer of the search box. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/** Category options for select dropdowns. */
const categoryOptions = computed(() => store.categories.map((category) => ({ value: category.id, label: category.name })))

/** Whether the add-reply form is complete. */
const canAddReply = computed<boolean>(
  () => newReplyName.value.trim() !== '' && newReplyCategoryId.value !== 0 && newReplyContent.value.trim() !== '',
)

/**
 * Switches tab.
 *
 * @param value - The tab key the tab strip emitted.
 */
const selectTab = (value: string): void => {
  const tab = enumValueOf(PredefinedRepliesTab, value)
  if (tab !== null) {
    activeTab.value = tab
  }
}

/**
 * Creates a category from the add form and clears it on success.
 */
const addCategory = async (): Promise<void> => {
  if (!newCategoryName.value.trim()) {
    return
  }
  feedback.clear()
  categorySaving.value = true
  const id = await store.createCategory(newCategoryName.value.trim())
  categorySaving.value = false
  report(id !== null, 'shared.recordCreated')
  if (id !== null) {
    newCategoryName.value = ''
  }
}

/**
 * Starts editing a category's name in place.
 *
 * @param category - The category to edit.
 */
const startEditCategory = (category: PredefinedReplyCategory): void => {
  editingCategoryId.value = category.id
  editingCategoryName.value = category.name
}

/**
 * Stops editing the category without saving.
 */
const cancelEditCategory = (): void => {
  editingCategoryId.value = null
  editingCategoryName.value = ''
}

/**
 * Saves the category name under edit.
 */
const saveEditCategory = async (): Promise<void> => {
  if (editingCategoryId.value === null || !editingCategoryName.value.trim()) {
    return
  }
  feedback.clear()
  const saved = await store.updateCategory(editingCategoryId.value, editingCategoryName.value.trim())
  report(saved, 'shared.changesSavedSuccessfully')
  if (saved) {
    cancelEditCategory()
  }
}

/**
 * Deletes the category the dialog asked about.
 */
const handleDeleteCategory = async (): Promise<void> => {
  if (deleteCategoryTarget.value === null) {
    return
  }
  feedback.clear()
  deletingCategory.value = true
  const deleted = await store.deleteCategory(deleteCategoryTarget.value)
  deletingCategory.value = false
  deleteCategoryTarget.value = null
  report(deleted, 'shared.recordDeleted')
}

/** Closes the category delete confirmation; ignored while the delete runs. */
const dismissDeleteCategory = (): void => {
  if (!deletingCategory.value) {
    deleteCategoryTarget.value = null
  }
}

/**
 * Creates a reply from the add form, clears it and reloads the replies.
 */
const addReply = async (): Promise<void> => {
  if (!canAddReply.value) {
    return
  }
  feedback.clear()
  replySaving.value = true
  const id = await store.createReply({
    name: newReplyName.value.trim(),
    categoryId: newReplyCategoryId.value,
    content: newReplyContent.value.trim(),
  })
  replySaving.value = false
  report(id !== null, 'shared.recordCreated')
  if (id !== null) {
    newReplyName.value = ''
    newReplyCategoryId.value = 0
    newReplyContent.value = ''
    await store.fetchReplies()
  }
}

/**
 * Starts editing a reply in place.
 *
 * @param reply - The reply to edit.
 */
const startEditReply = (reply: PredefinedReply): void => {
  editingReplyId.value = reply.id
  editingReply.value = { name: reply.name, categoryId: reply.categoryId, content: reply.content }
}

/**
 * Saves the reply under edit and reloads the replies.
 */
const saveEditReply = async (): Promise<void> => {
  if (editingReplyId.value === null) {
    return
  }
  feedback.clear()
  const saved = await store.updateReply(editingReplyId.value, { ...editingReply.value })
  report(saved, 'shared.changesSavedSuccessfully')
  if (saved) {
    editingReplyId.value = null
    await store.fetchReplies()
  }
}

/**
 * Deletes the reply the dialog asked about, then reloads the replies.
 */
const handleDeleteReply = async (): Promise<void> => {
  if (deleteReplyTarget.value === null) {
    return
  }
  feedback.clear()
  deletingReply.value = true
  const deleted = await store.deleteReply(deleteReplyTarget.value)
  deletingReply.value = false
  deleteReplyTarget.value = null
  report(deleted, 'shared.recordDeleted')
  if (deleted) {
    await store.fetchReplies()
  }
}

/** Closes the reply delete confirmation; ignored while the delete runs. */
const dismissDeleteReply = (): void => {
  if (!deletingReply.value) {
    deleteReplyTarget.value = null
  }
}

// The search runs once typing pauses; an empty box clears the results.
watch(searchQuery, (query) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!query.trim()) {
      searchResults.value = []
      return
    }
    searching.value = true
    const hits = await store.searchReplies(query.trim())
    if (hits) {
      searchResults.value = hits
    } else {
      feedback.show(store.actionError ?? t('errors.noAnswer'), true)
    }
    searching.value = false
  }, SEARCH_DEBOUNCE_MS)
})

onBeforeUnmount(() => {
  clearTimeout(searchTimer)
})

onMounted(loadAll)

/**
 * The row actions of a category: edit in place, and delete last (confirmed first).
 *
 * @param category - The category of the row.
 * @returns The action list passed to UiRowActions.
 */
const categoryActions = (category: PredefinedReplyCategory): UiMenuAction[] => {
  /**
   * Opens this category's in-place edit field.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    startEditCategory(category)
  }
  /**
   * Asks for confirmation before this category is deleted.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteCategoryTarget.value = category.id
  }
  return [
    { label: t('shared.edit'), testid: `predefined-replies-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `predefined-replies-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
}

/**
 * The row actions of a reply: edit in place, and delete last (confirmed first).
 *
 * @param reply - The reply of the row.
 * @returns The action list passed to UiRowActions.
 */
const replyActions = (reply: PredefinedReply): UiMenuAction[] => {
  /**
   * Opens this reply's in-place edit form.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    startEditReply(reply)
  }
  /**
   * Asks for confirmation before this reply is deleted.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteReplyTarget.value = reply.id
  }
  return [
    { label: t('shared.edit'), testid: `predefined-replies-reply-${reply.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `predefined-replies-reply-${reply.id}-delete`, onSelect: onDelete, danger: true },
  ]
}
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('support.predefinedReplies.predefinedReplies')"
      :subtitle="t('support.predefinedReplies.managePredefinedTicketReplyTemplates')"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="predefined-replies-error"
      @retry="loadAll"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="predefined-replies-feedback" />

    <template v-if="activeTab === PredefinedRepliesTab.Category">
      <UiCard padding="md">
        <UiStack direction="row" align="end">
          <UiField :label="t('shared.categoryName')" :error="store.fieldErrors.name" class="flex-1">
            <UiInput
              v-model="newCategoryName"
              :placeholder="t('shared.enterCategoryName')"
              data-testid="predefined-replies-category-name"
              @keyup.enter="addCategory"
            />
          </UiField>
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="categorySaving"
            :disabled="!newCategoryName.trim()"
            data-testid="predefined-replies-category-add"
            @click="addCategory"
          >
            {{ t('shared.addCategory') }}
          </UiButton>
        </UiStack>
      </UiCard>

      <UiStack direction="row" gap="xs" align="center">
        <UiText tone="muted" size="md">{{ t('shared.youAreHere') }}</UiText>
        <UiText tone="secondary" size="md" class="font-medium">{{ t('support.predefinedReplies.topLevel') }}</UiText>
      </UiStack>

      <UiCard v-if="store.loading || store.categories.length > 0" data-testid="predefined-replies-categories">
        <UiTable :density="UiTableDensity.Comfortable" :loading="store.loading">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
              <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow
              v-for="category in store.categories"
              :key="category.id"
              hoverable
              :data-testid="`predefined-replies-category-${category.id}`"
            >
              <!-- The category under edit spans the whole row with its in-place field -->
              <UiTableCell v-if="editingCategoryId === category.id" :colspan="CATEGORY_COLUMN_COUNT">
                <UiStack direction="row" align="center" gap="sm">
                  <UiInput
                    v-model="editingCategoryName"
                    :placeholder="t('shared.categoryName2')"
                    class="flex-1"
                    data-testid="predefined-replies-category-edit-name"
                    @keyup.enter="saveEditCategory"
                    @keyup.escape="cancelEditCategory"
                  />
                  <UiButton
                    :variant="UiButtonVariant.Primary"
                    :size="UiSize.Sm"
                    data-testid="predefined-replies-category-edit-save"
                    @click="saveEditCategory"
                  >
                    {{ t('shared.save') }}
                  </UiButton>
                  <UiButton
                    :variant="UiButtonVariant.Secondary"
                    :size="UiSize.Sm"
                    data-testid="predefined-replies-category-edit-cancel"
                    @click="cancelEditCategory"
                  >
                    {{ t('shared.cancel') }}
                  </UiButton>
                </UiStack>
              </UiTableCell>

              <template v-else>
                <UiTableCell>{{ category.name }}</UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="categoryActions(category)" :testid="`predefined-replies-category-${category.id}-actions`" />
                </UiTableCell>
              </template>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard v-else>
        <UiEmptyState
          :title="t('support.predefinedReplies.noCategoriesFound')"
          :description="t('support.predefinedReplies.createYourFirstCategoryAbove')"
          data-testid="predefined-replies-categories-empty"
        />
      </UiCard>
    </template>

    <template v-if="activeTab === PredefinedRepliesTab.Reply">
      <UiCard padding="md">
        <UiStack>
          <UiField :label="t('shared.name')" :error="store.fieldErrors.name">
            <UiInput
              v-model="newReplyName"
              :placeholder="t('support.predefinedReplies.replyName')"
              data-testid="predefined-replies-reply-name"
            />
          </UiField>
          <UiField :label="t('shared.category')" :error="store.fieldErrors.categoryId">
            <UiSelect
              v-model="newReplyCategoryId"
              :options="categoryOptions"
              :placeholder="t('shared.selectCategory')"
              data-testid="predefined-replies-reply-category"
            />
          </UiField>
          <UiField :label="t('support.predefinedReplies.content')" :error="store.fieldErrors.content">
            <UiTextarea
              v-model="newReplyContent"
              rows="8"
              :placeholder="t('support.predefinedReplies.replyContent')"
              data-testid="predefined-replies-reply-content"
            />
          </UiField>
          <UiStack direction="row">
            <UiButton
              :variant="UiButtonVariant.Primary"
              :loading="replySaving"
              :disabled="!canAddReply"
              data-testid="predefined-replies-reply-add"
              @click="addReply"
            >
              {{ t('shared.addReply') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiCard v-if="store.loading || store.replies.length > 0" data-testid="predefined-replies-replies">
        <UiTable :density="UiTableDensity.Comfortable" :loading="store.loading">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.category') }}</UiTableHeader>
              <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow
              v-for="reply in store.replies"
              :key="reply.id"
              hoverable
              :data-testid="`predefined-replies-reply-${reply.id}`"
            >
              <!-- The reply under edit spans the whole row with its in-place form -->
              <UiTableCell v-if="editingReplyId === reply.id" :colspan="REPLY_COLUMN_COUNT">
                <UiStack gap="sm">
                  <UiInput
                    v-model="editingReply.name"
                    :placeholder="t('support.predefinedReplies.replyName')"
                    data-testid="predefined-replies-reply-edit-name"
                  />
                  <UiSelect
                    v-model="editingReply.categoryId"
                    :options="categoryOptions"
                    :placeholder="t('shared.selectCategory')"
                    data-testid="predefined-replies-reply-edit-category"
                  />
                  <UiTextarea
                    v-model="editingReply.content"
                    rows="5"
                    :placeholder="t('support.predefinedReplies.replyContent2')"
                    data-testid="predefined-replies-reply-edit-content"
                  />
                  <UiStack direction="row" gap="sm">
                    <UiButton
                      :variant="UiButtonVariant.Primary"
                      :size="UiSize.Sm"
                      data-testid="predefined-replies-reply-edit-save"
                      @click="saveEditReply"
                    >
                      {{ t('shared.save') }}
                    </UiButton>
                    <UiButton
                      :variant="UiButtonVariant.Secondary"
                      :size="UiSize.Sm"
                      data-testid="predefined-replies-reply-edit-cancel"
                      @click="editingReplyId = null"
                    >
                      {{ t('shared.cancel') }}
                    </UiButton>
                  </UiStack>
                </UiStack>
              </UiTableCell>

              <template v-else>
                <UiTableCell>{{ reply.name }}</UiTableCell>
                <UiTableCell muted>{{ reply.categoryName }}</UiTableCell>
                <UiTableCell actions>
                  <UiRowActions :actions="replyActions(reply)" :testid="`predefined-replies-reply-${reply.id}-actions`" />
                </UiTableCell>
              </template>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard v-else>
        <UiEmptyState
          :title="t('support.predefinedReplies.noPredefinedRepliesFound')"
          :description="t('support.predefinedReplies.createYourFirstReplyUsing')"
          data-testid="predefined-replies-replies-empty"
        />
      </UiCard>
    </template>

    <template v-if="activeTab === PredefinedRepliesTab.Search">
      <UiInput
        v-model="searchQuery"
        :placeholder="t('support.predefinedReplies.searchPredefinedRepliesByName')"
        data-testid="predefined-replies-search"
      />

      <UiCard v-if="searching || searchResults.length > 0" data-testid="predefined-replies-results">
        <UiTable :density="UiTableDensity.Comfortable" :loading="searching">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.category') }}</UiTableHeader>
              <UiTableHeader>{{ t('support.predefinedReplies.content') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow
              v-for="result in searchResults"
              :key="result.id"
              hoverable
              :data-testid="`predefined-replies-result-${result.id}`"
            >
              <UiTableCell>{{ result.name }}</UiTableCell>
              <UiTableCell muted>{{ result.categoryName }}</UiTableCell>
              <UiTableCell muted>{{ result.content }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard v-else-if="searchQuery.trim()">
        <UiEmptyState
          :title="t('support.predefinedReplies.noResultsFound')"
          :description="t('support.predefinedReplies.tryADifferentSearchTerm')"
          data-testid="predefined-replies-no-results"
        />
      </UiCard>

      <UiCard v-else>
        <UiEmptyState
          :title="t('support.predefinedReplies.searchPredefinedReplies')"
          :description="t('support.predefinedReplies.typeAboveToSearchBy')"
          data-testid="predefined-replies-search-hint"
        />
      </UiCard>
    </template>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteCategoryTarget !== null"
      :title="t('support.predefinedReplies.deleteCategory')"
      :message="t('support.predefinedReplies.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading="deletingCategory"
      :tone="UiTone.Danger"
      @confirm="handleDeleteCategory"
      @close="dismissDeleteCategory"
    />

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteReplyTarget !== null"
      :title="t('support.predefinedReplies.deletePredefinedReply')"
      :message="t('support.predefinedReplies.areYouSureYouWant2')"
      :confirm-label="t('shared.delete')"
      :loading="deletingReply"
      :tone="UiTone.Danger"
      @confirm="handleDeleteReply"
      @close="dismissDeleteReply"
    />
  </UiStack>
</template>
