<script setup lang="ts">
/**
 * Knowledgebase management: tabs for adding categories (with the category browser) and articles (the article form
 * over the searchable, filterable, paged article list). Every change reports its outcome (success, or the API's
 * sentence) under the header, deleting a category is confirmed first, and a refused load shows the sentence with a
 * Retry. A saved article is read back through the article list, which knows the page and the filters. The tab strip, the add
 * category card and the head of the categories table are the ones of `KnowledgebaseView.schema`,
 * which `UiSkeletonOf` also draws while the view downloads.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import KnowledgebaseArticleList from '@/components/support/KnowledgebaseArticleList.vue'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { enumValueOf } from '@/lib/enumValue'
import { ContentLanguage } from '@/locales/contentLanguage'
import { blankTranslations, collectTranslations, filledLocales, formsFromTranslations } from '@/lib/storefrontTranslations'
import { useKnowledgebaseStore } from '@/stores/knowledgebase'
import type { KbArticle, KbArticleTranslation, KbCategory } from '@/types/kbarticle'
import { KnowledgebaseTab } from '@/types/supporttab'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'
import { EMPTY_VALUE } from '@/lib/format'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { KB_CATEGORIES_TABLE, KB_CATEGORY_FORM_PART, KB_TABS_PART } from '@/views/KnowledgebaseView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const store = useKnowledgebaseStore()

/** Rows of the article body field. */
const ARTICLE_CONTENT_ROWS = 12

/** Currently active tab. */
const activeTab = ref<KnowledgebaseTab>(KnowledgebaseTab.Category)

/** Tab items for the kit's tab strip, the tabs of the schema. */
const tabItems = computed<UiTabItem[]>(() =>
  tabItemsOfSchema(KB_TABS_PART.tabs, t, 'knowledgebase-tab-'),
)

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

/**
 * Creates a blank title and body.
 *
 * @returns An empty translation.
 */
const blankText = (): KbArticleTranslation => ({ title: '', content: '' })

/** The article form: the category, the published flag (new articles only) and the text per language. */
const articleForm = reactive({ categoryId: 0, isPublished: false, translations: blankTranslations(blankText) })

/** The language whose fields are on screen. */
const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

/**
 * Tells whether a language's text is written: it has a title or a body.
 *
 * @param text - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (text: KbArticleTranslation): boolean => text.title.trim() !== '' || text.content.trim() !== ''

/** Whether each language has its text written, for the markers on the tabs. */
const filled = computed(() => filledLocales(articleForm.translations, isFilled))

/** ID of the article being edited; null while the form creates a new one. */
const editingArticleId = ref<number | null>(null)

/** Whether an article is being saved. */
const articleSaving = ref(false)

/** The article list, asked to read its page again after an article is saved. */
const articleList = ref<InstanceType<typeof KnowledgebaseArticleList> | null>(null)

/** The sentence of the last refused load, shown with Retry; null when the categories loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last change. */
const feedback = useOutcomeToast()

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

/** Whether the article form is complete: a category and the English title and body (English is required). */
const canSaveArticle = computed<boolean>(() => {
  const english = articleForm.translations[ContentLanguage.En]
  return articleForm.categoryId !== 0 && english.title.trim() !== '' && english.content.trim() !== ''
})

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
const categoryActions = (category: KbCategory): UiMenuAction[] => {
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
    { label: t('shared.edit'), icon: Icon.Edit, testid: `knowledgebase-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `knowledgebase-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDeleteCategory = (): void => {
  if (!deletingCategory.value) {
    deleteCategoryTarget.value = null
  }
}

/** The label of the save button: create for a new article, save while editing. */
const articleSaveLabel = computed<string>(() => (editingArticleId.value === null ? t('support.knowledgebase.createArticle') : t('shared.save')))

/**
 * Collects the translations worth sending: the trimmed texts of the languages that have any.
 *
 * @returns The `translations` object of the payload.
 */
const collectArticleTranslations = () =>
  collectTranslations(
    articleForm.translations,
    isFilled,
    (text) => ({ title: text.title.trim(), content: text.content.trim() }),
  )

/** Empties the article form and leaves edit mode. */
const resetArticleForm = (): void => {
  articleForm.categoryId = 0
  articleForm.isPublished = false
  articleForm.translations = blankTranslations(blankText)
  editingArticleId.value = null
  activeLocale.value = ContentLanguage.En
}

/**
 * Saves the article form: creates a new article, or replaces the translations of the one being edited.
 * The form is cleared and the article list and the category counts are read again on success.
 */
const saveArticle = async (): Promise<void> => {
  if (!canSaveArticle.value) {
    return
  }
  articleSaving.value = true
  const translations = collectArticleTranslations()
  const editingId = editingArticleId.value
  const saved =
    editingId === null
      ? (await store.createArticle({ categoryId: articleForm.categoryId, translations, isPublished: articleForm.isPublished })) !== null
      : await store.updateArticle(editingId, { categoryId: articleForm.categoryId, translations })
  articleSaving.value = false
  report(saved, 'support.knowledgebase.articleSaved')
  if (saved) {
    resetArticleForm()
    await Promise.all([load(), articleList.value?.reload()])
  }
}

/**
 * Loads an article into the form to edit its category and translations.
 *
 * @param article - The article to edit.
 */
const startEditArticle = (article: KbArticle): void => {
  editingArticleId.value = article.id
  articleForm.categoryId = article.categoryId
  articleForm.isPublished = article.isPublished
  articleForm.translations = formsFromTranslations(article.translations, blankText)
  activeLocale.value = ContentLanguage.En
}

/** Loads the categories; the article list loads itself when its tab appears. */
onMounted(() => {
  void load()
})
</script>

<template>
  <UiStack>
    <UiPageHeader v-bind="schemaHeader(schema, t)" />


    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="knowledgebase-error"
      @retry="load"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <template v-if="activeTab === KnowledgebaseTab.Category">
      <UiSchemaForm :columns="KB_CATEGORY_FORM_PART.columns" :errors="store.fieldErrors">
        <template #field-name>
          <UiInput
            v-model="newCategory.name"
            :placeholder="t('shared.enterCategoryName')"
            data-testid="knowledgebase-category-name"
          />
        </template>
        <template #field-hidden>
          <SupportCheckboxField
            v-model="newCategory.isHidden"
            :label="t('support.knowledgebase.checkToHide')"
            testid="knowledgebase-category-hidden"
          />
        </template>
        <template #field-description>
          <UiInput
            v-model="newCategory.description"
            :placeholder="t('support.knowledgebase.categoryDescription')"
            data-testid="knowledgebase-category-description"
          />
        </template>
        <template #section-category>
          <UiStack :direction="UiDirection.Row">
            <UiButton :action="UiActionKind.Add"
              :variant="UiButtonVariant.Primary"
              :loading="categorySaving"
              :disabled="!newCategory.name.trim()"
              data-testid="knowledgebase-category-add"
              @click="addCategory"
            >
              {{ t('shared.createCategory') }}
            </UiButton>
          </UiStack>
        </template>
      </UiSchemaForm>

      <UiBreadcrumb :items="[{ label: t('support.knowledgebase.knowledgebaseHome') }]" />

      <UiDataTable
        :loading="store.loading && store.categories.length === 0"
        :empty="!store.loading && store.categories.length === 0"
        data-testid="knowledgebase-categories"
      >
        <template #header>
          <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('support.knowledgebase.browseByCategory') }}</UiHeading>
        </template>
        <UiSchemaTableHead :columns="KB_CATEGORIES_TABLE.columns" />
        <UiTableBody>
          <UiTableRow
            v-for="category in store.categories"
            :key="category.id"
            hoverable
            :data-testid="`knowledgebase-category-${category.id}`"
          >
            <!-- The category under edit spans the whole row with its in-place form -->
            <UiTableCell v-if="editingCategoryId === category.id" :colspan="KB_CATEGORIES_TABLE.columns.length">
              <UiStack :gap="UiGap.Sm">
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
                <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm">
                  <UiButton :action="UiActionKind.Save"
                    :variant="UiButtonVariant.Primary"
                    :size="UiSize.Sm"
                    data-testid="knowledgebase-edit-save"
                    @click="saveEditCategory"
                  >
                    {{ t('shared.save') }}
                  </UiButton>
                  <UiButton :action="UiActionKind.Cancel"
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
                <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center">
                  <UiIcon :markup="Icon.Folder" />
                  <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ category.name }}</UiText>
                  <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">({{ category.articleCount }})</UiText>
                </UiStack>
              </UiTableCell>
              <UiTableCell muted>{{ category.description || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell>
                <UiBadge v-if="category.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="categoryActions(category)" :testid="`knowledgebase-category-${category.id}-actions`" />
              </UiTableCell>
            </template>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState
            :title="t('support.knowledgebase.noCategoriesFound')"
            :description="t('support.knowledgebase.createYourFirstCategoryAbove')"
            data-testid="knowledgebase-categories-empty"
          />
        </template>
      </UiDataTable>

      <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('support.knowledgebase.browseByTag') }}</UiHeading>
      <UiCard>
        <UiEmptyState
          :title="t('support.knowledgebase.noTagsFound')"
          :description="t('support.knowledgebase.tagsWillAppearHereWhen')"
        />
      </UiCard>
    </template>

    <template v-if="activeTab === KnowledgebaseTab.Article">
      <UiCard :padding="UiCardPadding.Md" data-testid="knowledgebase-article-form">
        <UiStack>
          <UiHeading v-if="editingArticleId !== null" :level="2" :variant="UiHeadingVariant.Card">{{ t('support.knowledgebase.editArticle') }}</UiHeading>
          <UiField :label="t('shared.category')" :error="store.fieldErrors.categoryId">
            <UiSelect
              v-model="articleForm.categoryId"
              :options="categoryOptions"
              :placeholder="t('shared.selectCategory')"
              data-testid="knowledgebase-article-category"
            />
          </UiField>
          <UiLocalizedField v-model="activeLocale" :filled="filled" testid="knowledgebase-article-locale" />
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('support.knowledgebase.englishRequired') }}</UiText>
          <UiField :label="t('shared.title')" :error="store.fieldErrors.translations">
            <UiInput
              v-model="articleForm.translations[activeLocale].title"
              :placeholder="t('support.knowledgebase.articleTitle')"
              data-testid="knowledgebase-article-title"
            />
          </UiField>
          <UiField :label="t('support.knowledgebase.content')">
            <UiTextarea
              v-model="articleForm.translations[activeLocale].content"
              :rows="ARTICLE_CONTENT_ROWS"
              :placeholder="t('support.knowledgebase.articleContent')"
              data-testid="knowledgebase-article-content"
            />
          </UiField>
          <SupportCheckboxField
            v-if="editingArticleId === null"
            v-model="articleForm.isPublished"
            :label="t('shared.published')"
            testid="knowledgebase-article-published"
          />
          <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm">
            <UiButton :action="UiActionKind.Save"
              :variant="UiButtonVariant.Primary"
              :loading="articleSaving"
              :disabled="!canSaveArticle"
              data-testid="knowledgebase-article-save"
              @click="saveArticle"
            >
              {{ articleSaveLabel }}
            </UiButton>
            <UiButton :action="UiActionKind.Cancel"
              v-if="editingArticleId !== null"
              :variant="UiButtonVariant.Secondary"
              data-testid="knowledgebase-article-cancel"
              @click="resetArticleForm"
            >
              {{ t('shared.cancel') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('support.knowledgebase.articles') }}</UiHeading>

      <KnowledgebaseArticleList ref="articleList" :categories="store.categories" @edit="startEditArticle" />
    </template>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteCategoryTarget !== null"
      :title="t('support.knowledgebase.deleteCategory')"
      :message="t('support.knowledgebase.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading="deletingCategory"
      :tone="UiTone.Danger"
      @confirm="handleDeleteCategory"
      @close="dismissDeleteCategory"
    />
  </UiStack>
</template>
