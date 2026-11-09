<script setup lang="ts">
/**
 * Knowledgebase management: tabs for adding categories (with the category browser) and articles.
 * Every change reports its outcome (success, or the API's sentence) under the header, deleting a
 * category is confirmed first, and a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { enumValueOf } from '@/lib/enumValue'
import { useKnowledgebaseStore } from '@/stores/knowledgebase'
import type { KbCategory } from '@/types/kbarticle'
import { KnowledgebaseTab } from '@/types/supporttab'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'

const { t } = useI18n()

const store = useKnowledgebaseStore()

/** Number of columns of the category table, spanned by the row of a category under edit. */
const CATEGORY_COLUMN_COUNT = 4

/** Currently active tab. */
const activeTab = ref<KnowledgebaseTab>(KnowledgebaseTab.Category)

/** Tab items for the kit's tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: KnowledgebaseTab.Category, label: t('shared.addCategory'), testid: 'knowledgebase-tab-category' },
  { value: KnowledgebaseTab.Article, label: t('support.knowledgebase.addArticle'), testid: 'knowledgebase-tab-article' },
])

/** New category form fields. */
const newCategory = ref({ name: '', description: '', isHidden: false })

/** Whether a category is being saved. */
const categorySaving = ref(false)

/** ID of the category waiting for delete confirmation; null when no dialog is open. */
const deleteCategoryTarget = ref<number | null>(null)

/** Whether a category deletion is in progress. */
const deletingCategory = ref(false)

/** ID of the category being edited in place; null when none. */
const editingCategoryId = ref<number | null>(null)

/** Edit form data of the category being edited. */
const editingCategory = ref({ name: '', description: '', isHidden: false })

/** New article form fields. */
const newArticle = ref({ title: '', categoryId: 0, content: '', published: false })

/** Whether an article is being saved. */
const articleSaving = ref(false)

/** The sentence of the last refused load, shown with Retry; null when the categories loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last change. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of one change: the success sentence, or the API's sentence of the refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Loads the categories and records whether the load was refused.
 *
 * @returns Resolves when the categories have settled.
 */
const load = async (): Promise<void> => {
  loadError.value = (await store.fetchCategories()) ? null : store.error
}

/** Category options for select dropdowns. */
const categoryOptions = computed(() => store.categories.map((category) => ({ value: category.id, label: category.name })))

/** Whether the article form is complete. */
const canSaveArticle = computed<boolean>(
  () => newArticle.value.title.trim() !== '' && newArticle.value.categoryId !== 0 && newArticle.value.content.trim() !== '',
)

/**
 * Switches tab.
 *
 * @param value - The tab key the tab strip emitted.
 */
const selectTab = (value: string): void => {
  const tab = enumValueOf(KnowledgebaseTab, value)
  if (tab !== null) {
    activeTab.value = tab
  }
}

/**
 * Creates a category from the add form and clears the form on success.
 */
const addCategory = async (): Promise<void> => {
  if (!newCategory.value.name.trim()) {
    return
  }
  feedback.clear()
  categorySaving.value = true
  const id = await store.createCategory({
    name: newCategory.value.name.trim(),
    description: newCategory.value.description.trim(),
    isHidden: newCategory.value.isHidden,
  })
  categorySaving.value = false
  report(id !== null, 'support.knowledgebase.categorySaved')
  if (id !== null) {
    newCategory.value = { name: '', description: '', isHidden: false }
  }
}

/**
 * Starts editing a category in place.
 *
 * @param category - The category to edit.
 */
const startEditCategory = (category: KbCategory): void => {
  editingCategoryId.value = category.id
  editingCategory.value = {
    name: category.name,
    description: category.description,
    isHidden: category.isHidden,
  }
}

/**
 * Saves the category being edited.
 */
const saveEditCategory = async (): Promise<void> => {
  if (editingCategoryId.value === null) {
    return
  }
  feedback.clear()
  const saved = await store.updateCategory(editingCategoryId.value, { ...editingCategory.value })
  report(saved, 'support.knowledgebase.categorySaved')
  if (saved) {
    editingCategoryId.value = null
  }
}

/**
 * Deletes the category the dialog asked about and shows the outcome.
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
  report(deleted, 'support.knowledgebase.categoryDeleted')
}

/**
 * The row actions of a category: edit in place, and delete last (confirmed first).
 *
 * @param category - The category of the row.
 * @returns The action list passed to UiRowActions.
 */
const categoryActions = (category: KbCategory): UiRowAction[] => {
  /**
   * Opens this category's in-place edit form.
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
    { label: t('shared.edit'), testid: `knowledgebase-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `knowledgebase-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDeleteCategory = (): void => {
  if (!deletingCategory.value) {
    deleteCategoryTarget.value = null
  }
}

/**
 * Creates an article from the add form, clears the form and refreshes the category counts.
 */
const addArticle = async (): Promise<void> => {
  if (!canSaveArticle.value) {
    return
  }
  // The API files an article under a category by its name, not its id.
  const category = store.categories.find((candidate) => candidate.id === newArticle.value.categoryId)
  if (!category) {
    return
  }
  feedback.clear()
  articleSaving.value = true
  const id = await store.createArticle({
    title: newArticle.value.title.trim(),
    content: newArticle.value.content.trim(),
    category: category.name,
    isPublished: newArticle.value.published,
  })
  articleSaving.value = false
  report(id !== null, 'support.knowledgebase.articleSaved')
  if (id !== null) {
    newArticle.value = { title: '', categoryId: 0, content: '', published: false }
    await load()
  }
}

/** Loads the categories. */
onMounted(() => {
  void load()
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('support.knowledgebase.knowledgebase')"
      :subtitle="t('support.knowledgebase.manageKnowledgebaseArticlesAndCategories')"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="knowledgebase-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="knowledgebase-error"
      @retry="load"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <template v-if="activeTab === KnowledgebaseTab.Category">
      <UiCard padding="md">
        <UiStack>
          <UiField :label="t('shared.categoryName')" :error="store.fieldErrors.name">
            <UiInput
              v-model="newCategory.name"
              :placeholder="t('shared.enterCategoryName')"
              data-testid="knowledgebase-category-name"
            />
          </UiField>
          <SupportCheckboxField
            v-model="newCategory.isHidden"
            :label="t('support.knowledgebase.checkToHide')"
            testid="knowledgebase-category-hidden"
          />
          <UiField :label="t('shared.description')">
            <UiInput
              v-model="newCategory.description"
              :placeholder="t('support.knowledgebase.categoryDescription')"
              data-testid="knowledgebase-category-description"
            />
          </UiField>
          <UiStack direction="row">
            <UiButton
              :variant="UiButtonVariant.Primary"
              :loading="categorySaving"
              :disabled="!newCategory.name.trim()"
              data-testid="knowledgebase-category-add"
              @click="addCategory"
            >
              {{ t('shared.addCategory') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiStack direction="row" gap="xs" align="center">
        <UiText tone="muted" size="md">{{ t('shared.youAreHere') }}</UiText>
        <UiText tone="secondary" size="md" class="font-medium">{{ t('support.knowledgebase.knowledgebaseHome') }}</UiText>
      </UiStack>

      <UiSkeleton v-if="store.loading" :lines="3" data-testid="knowledgebase-loading" />

      <template v-else>
        <UiHeading :level="2" variant="card">{{ t('support.knowledgebase.browseByCategory') }}</UiHeading>

        <UiCard v-if="store.categories.length > 0" data-testid="knowledgebase-categories">
          <UiTable :density="UiTableDensity.Comfortable">
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
                <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow
                v-for="category in store.categories"
                :key="category.id"
                hoverable
                :data-testid="`knowledgebase-category-${category.id}`"
              >
                <!-- The category under edit spans the whole row with its in-place form -->
                <UiTableCell v-if="editingCategoryId === category.id" :colspan="CATEGORY_COLUMN_COUNT">
                  <UiStack gap="sm">
                    <UiInput
                      v-model="editingCategory.name"
                      :placeholder="t('shared.categoryName2')"
                      data-testid="knowledgebase-edit-name"
                    />
                    <UiInput
                      v-model="editingCategory.description"
                      :placeholder="t('shared.description')"
                      data-testid="knowledgebase-edit-description"
                    />
                    <SupportCheckboxField
                      v-model="editingCategory.isHidden"
                      :label="t('shared.hidden')"
                      testid="knowledgebase-edit-hidden"
                    />
                    <UiStack direction="row" gap="sm">
                      <UiButton
                        :variant="UiButtonVariant.Primary"
                        :size="UiSize.Sm"
                        data-testid="knowledgebase-edit-save"
                        @click="saveEditCategory"
                      >
                        {{ t('shared.save') }}
                      </UiButton>
                      <UiButton
                        :variant="UiButtonVariant.Secondary"
                        :size="UiSize.Sm"
                        data-testid="knowledgebase-edit-cancel"
                        @click="editingCategoryId = null"
                      >
                        {{ t('shared.cancel') }}
                      </UiButton>
                    </UiStack>
                  </UiStack>
                </UiTableCell>

                <template v-else>
                  <UiTableCell>
                    <UiStack direction="row" gap="sm" align="center">
                      <UiIcon :markup="Icon.Folder" />
                      <UiText tone="primary" size="md">{{ category.name }}</UiText>
                      <UiText tone="muted" size="sm">({{ category.articleCount }})</UiText>
                    </UiStack>
                  </UiTableCell>
                  <UiTableCell muted>{{ category.description || '—' }}</UiTableCell>
                  <UiTableCell>
                    <UiBadge v-if="category.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
                  </UiTableCell>
                  <UiTableCell actions>
                    <UiRowActions :actions="categoryActions(category)" :testid="`knowledgebase-category-${category.id}-actions`" />
                  </UiTableCell>
                </template>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
        </UiCard>

        <UiCard v-else>
          <UiEmptyState
            :title="t('support.knowledgebase.noCategoriesFound')"
            :description="t('support.knowledgebase.createYourFirstCategoryAbove')"
            data-testid="knowledgebase-categories-empty"
          />
        </UiCard>

        <UiHeading :level="2" variant="card">{{ t('support.knowledgebase.browseByTag') }}</UiHeading>
        <UiCard>
          <UiEmptyState
            :title="t('support.knowledgebase.noTagsFound')"
            :description="t('support.knowledgebase.tagsWillAppearHereWhen')"
          />
        </UiCard>
      </template>
    </template>

    <UiCard v-if="activeTab === KnowledgebaseTab.Article" padding="md">
      <UiStack>
        <UiField :label="t('shared.title')" :error="store.fieldErrors.title">
          <UiInput
            v-model="newArticle.title"
            :placeholder="t('support.knowledgebase.articleTitle')"
            data-testid="knowledgebase-article-title"
          />
        </UiField>
        <UiField :label="t('shared.category')" :error="store.fieldErrors.category">
          <UiSelect
            v-model="newArticle.categoryId"
            :options="categoryOptions"
            :placeholder="t('shared.selectCategory')"
            data-testid="knowledgebase-article-category"
          />
        </UiField>
        <UiField :label="t('support.knowledgebase.content')" :error="store.fieldErrors.content">
          <UiTextarea
            v-model="newArticle.content"
            rows="12"
            :placeholder="t('support.knowledgebase.articleContent')"
            data-testid="knowledgebase-article-content"
          />
        </UiField>
        <SupportCheckboxField
          v-model="newArticle.published"
          :label="t('shared.published')"
          testid="knowledgebase-article-published"
        />
        <UiStack direction="row">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="articleSaving"
            :disabled="!canSaveArticle"
            data-testid="knowledgebase-article-save"
            @click="addArticle"
          >
            {{ articleSaving ? t('shared.saving') : t('support.knowledgebase.saveArticle') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiDialog
      variant="confirm"
      :open="deleteCategoryTarget !== null"
      :title="t('support.knowledgebase.deleteCategory')"
      :message="t('support.knowledgebase.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deletingCategory"
      tone="danger"
      @confirm="handleDeleteCategory"
      @close="dismissDeleteCategory"
    />
  </UiStack>
</template>
