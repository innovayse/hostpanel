<script setup lang="ts">
/**
 * Downloads management: categories and downloadable files, with nested category navigation and
 * add, edit and delete for both. Every change reports its outcome (success, or the API's sentence)
 * under the header, a delete is confirmed first, and a refused load shows the sentence with a Retry. The tab strip, the add
 * forms, the cards of the two tables and their column heads are the ones of `DownloadsView.schema`, which `UiSkeletonOf`
 * also draws while the view downloads.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DownloadCategoryEditDialog from '@/components/support/DownloadCategoryEditDialog.vue'
import DownloadItemEditDialog from '@/components/support/DownloadItemEditDialog.vue'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useDownloadOptions } from '@/composables/useDownloadOptions'
import { useResultBanner } from '@/composables/useResultBanner'
import { enumValueOf } from '@/lib/enumValue'
import { DownloadTarget, useDownloadsStore } from '@/stores/downloads'
import type { DownloadCategory, DownloadCategoryPayload } from '@/types/downloadcategory'
import { DownloadType } from '@/types/downloaditem'
import type { DownloadItem, DownloadItemPayload } from '@/types/downloaditem'
import { DownloadsTab } from '@/types/supporttab'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'
import { EMPTY_VALUE } from '@/lib/format'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, {
  DOWNLOAD_CATEGORIES_PART,
  DOWNLOAD_CATEGORIES_TABLE,
  DOWNLOAD_CATEGORY_FORM_PART,
  DOWNLOAD_FILE_FORM_PART,
  DOWNLOAD_FILES_PART,
  DOWNLOAD_FILES_TABLE,
  DOWNLOADS_TABS_PART,
} from '@/views/DownloadsView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const store = useDownloadsStore()

/** One step of the category breadcrumb trail. */
interface Breadcrumb {
  /** Category the step leads to. */
  id: number
  /** Category name shown in the trail. */
  name: string
}

/** What a pending deletion points at. */
interface DeleteRequest {
  /** Whether a category or a download is deleted. */
  type: DownloadTarget
  /** Identifier of the item. */
  id: number
  /** Name shown in the confirmation message. */
  name: string
}

/** Currently active tab: add category or add download. */
const activeTab = ref<DownloadsTab>(DownloadsTab.Category)

/** Tab items for the kit's tab strip, the tabs of the schema. */
const tabItems = computed<UiTabItem[]>(() =>
  DOWNLOADS_TABS_PART.tabs.map((tab) => ({ value: tab.key, label: t(tab.label), testid: `downloads-tab-${tab.key}` })),
)

/** Whether data is loading. */
const loading = ref(false)

/** Whether a form submission is in progress. */
const saving = ref(false)

/** Current category ID for nested navigation (null = root). */
const currentCategoryId = ref<number | null>(null)

/** Breadcrumb trail for nested navigation. */
const breadcrumbs = ref<Breadcrumb[]>([])

/** New category name. */
const newCategoryName = ref('')

/** New category description. */
const newCategoryDescription = ref('')

/** Whether the new category should be hidden. */
const newCategoryHidden = ref(false)

/** New download file type. */
const newDownloadType = ref<DownloadType>(DownloadType.Zip)

/** New download title. */
const newDownloadTitle = ref('')

/** New download description. */
const newDownloadDescription = ref('')

/** New download filename. */
const newDownloadFilename = ref('')

/** New download category ID (0 = none). */
const newDownloadCategoryId = ref<number>(0)

/** Whether the new download is clients-only. */
const newDownloadClientsOnly = ref(false)

/** Whether the new download requires a product purchase. */
const newDownloadProductDownload = ref(false)

/** Whether the new download is hidden. */
const newDownloadHidden = ref(false)

/** Category currently being edited, null when its dialog is closed. */
const editingCategory = ref<DownloadCategory | null>(null)

/** Download currently being edited, null when its dialog is closed. */
const editingDownload = ref<DownloadItem | null>(null)

/** The pending deletion, null when no confirmation is open. */
const deleteRequest = ref<DeleteRequest | null>(null)

/** Whether a deletion is in progress. */
const deleting = ref(false)

/** The sentence of the last refused load, shown with Retry; null when the page loaded. */
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

const { typeOptions, categoryOptions } = useDownloadOptions(computed(() => store.categories))

/** Categories visible at the current navigation level. */
const visibleCategories = computed<DownloadCategory[]>(() =>
  store.categories.filter((category) => category.parentCategoryId === currentCategoryId.value),
)

/**
 * Switches tab.
 *
 * @param value - The tab key the tab strip emitted.
 */
const selectTab = (value: string): void => {
  const tab = enumValueOf(DownloadsTab, value)
  if (tab !== null) {
    activeTab.value = tab
  }
}

/**
 * Loads the downloads of the level being browsed.
 */
const fetchDownloads = async (): Promise<void> => {
  loadError.value = (await store.fetchDownloads(currentCategoryId.value)) ? null : store.error
}

/**
 * Loads categories and downloads, showing the loading state meanwhile and recording a refusal.
 */
const fetchAll = async (): Promise<void> => {
  loading.value = true
  loadError.value = (await store.fetchAll(currentCategoryId.value)) ? null : store.error
  loading.value = false
}

/**
 * Creates a category at the level being browsed.
 */
const createCategory = async (): Promise<void> => {
  if (!newCategoryName.value.trim()) {
    return
  }
  feedback.clear()
  saving.value = true
  const created = await store.createCategory({
    name: newCategoryName.value.trim(),
    description: newCategoryDescription.value.trim(),
    isHidden: newCategoryHidden.value,
    parentCategoryId: currentCategoryId.value,
  })
  saving.value = false
  report(created, 'support.downloads.categorySaved')
  if (created) {
    newCategoryName.value = ''
    newCategoryDescription.value = ''
    newCategoryHidden.value = false
    await fetchAll()
  }
}

/**
 * Saves the category edit dialog's payload.
 *
 * @param payload - The edited category.
 */
const updateCategory = async (payload: DownloadCategoryPayload): Promise<void> => {
  if (!editingCategory.value) {
    return
  }
  feedback.clear()
  saving.value = true
  const updated = await store.updateCategory(editingCategory.value.id, payload)
  saving.value = false
  report(updated, 'support.downloads.categorySaved')
  if (updated) {
    editingCategory.value = null
    await fetchAll()
  }
}

/**
 * Resets the add-download form to its defaults.
 */
const resetDownloadForm = (): void => {
  newDownloadType.value = DownloadType.Zip
  newDownloadTitle.value = ''
  newDownloadDescription.value = ''
  newDownloadFilename.value = ''
  newDownloadCategoryId.value = currentCategoryId.value ?? 0
  newDownloadClientsOnly.value = false
  newDownloadProductDownload.value = false
  newDownloadHidden.value = false
}

/**
 * Creates a download from the add form.
 */
const createDownload = async (): Promise<void> => {
  if (!newDownloadTitle.value.trim()) {
    return
  }
  feedback.clear()
  saving.value = true
  const created = await store.createDownload({
    title: newDownloadTitle.value.trim(),
    description: newDownloadDescription.value.trim(),
    type: newDownloadType.value,
    filename: newDownloadFilename.value.trim(),
    categoryId: newDownloadCategoryId.value || null,
    clientsOnly: newDownloadClientsOnly.value,
    productDownload: newDownloadProductDownload.value,
    isHidden: newDownloadHidden.value,
  })
  saving.value = false
  report(created, 'support.downloads.downloadSaved')
  if (created) {
    resetDownloadForm()
    await fetchAll()
  }
}

/**
 * Saves the download edit dialog's payload.
 *
 * @param payload - The edited download.
 */
const updateDownload = async (payload: DownloadItemPayload): Promise<void> => {
  if (!editingDownload.value) {
    return
  }
  feedback.clear()
  saving.value = true
  const updated = await store.updateDownload(editingDownload.value.id, payload)
  saving.value = false
  report(updated, 'support.downloads.downloadSaved')
  if (updated) {
    editingDownload.value = null
    await fetchAll()
  }
}

/**
 * Deletes what the confirmation asked about, shows the outcome and reloads on success.
 */
const handleDelete = async (): Promise<void> => {
  if (!deleteRequest.value) {
    return
  }
  feedback.clear()
  deleting.value = true
  const removed = await store.remove(deleteRequest.value.type, deleteRequest.value.id)
  deleting.value = false
  deleteRequest.value = null
  report(removed, 'support.downloads.deleted')
  if (removed) {
    await fetchAll()
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteRequest.value = null
  }
}

/**
 * Opens the edit dialog for a category.
 *
 * @param category - The category to edit.
 */
const editCategory = (category: DownloadCategory): void => {
  editingCategory.value = category
}

/**
 * Opens the delete confirmation for a category.
 *
 * @param category - The category to delete.
 */
const askToDeleteCategory = (category: DownloadCategory): void => {
  deleteRequest.value = { type: DownloadTarget.Category, id: category.id, name: category.name }
}

/**
 * Returns the row actions for a category card.
 *
 * @param category - The category the actions target.
 * @returns The actions array for UiRowActions.
 */
const categoryActions = (category: DownloadCategory): UiMenuAction[] => {
  /**
   * Forwards to {@link editCategory} for this specific category row.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => editCategory(category)
  /**
   * Forwards to {@link askToDeleteCategory} for this specific category row.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => askToDeleteCategory(category)
  return [
    { label: t('shared.edit'), icon: Icon.Edit, testid: `downloads-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `downloads-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
}

/**
 * The row actions of a download: its one action, delete, asks for confirmation first. The title opens the edit dialog.
 *
 * @param download - The download of the row.
 * @returns The action list passed to UiRowActions.
 */
const downloadActions = (download: DownloadItem): UiMenuAction[] => {
  /**
   * Asks for confirmation before this download is deleted.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteRequest.value = { type: DownloadTarget.Download, id: download.id, name: download.title }
  }
  return [{ label: t('shared.delete'), icon: Icon.Trash, testid: `downloads-download-${download.id}-delete`, onSelect: onDelete, danger: true }]
}

/**
 * Drills into a category: pushes it onto the breadcrumb trail and loads its downloads.
 *
 * @param category - The category to navigate into.
 */
const navigateToCategory = (category: DownloadCategory): void => {
  breadcrumbs.value.push({ id: category.id, name: category.name })
  currentCategoryId.value = category.id
  fetchDownloads()
}

/**
 * Goes back to the root level and clears the trail.
 */
const navigateHome = (): void => {
  currentCategoryId.value = null
  breadcrumbs.value = []
  fetchDownloads()
}

/**
 * Goes to a step of the trail, dropping the deeper steps.
 *
 * @param index - Index of the step in the trail.
 */
const navigateToBreadcrumb = (index: number): void => {
  const crumb = breadcrumbs.value[index]
  // Only ever called with an index the template rendered; the guard satisfies the indexed read.
  if (!crumb) {
    return
  }
  breadcrumbs.value = breadcrumbs.value.slice(0, index + 1)
  currentCategoryId.value = crumb.id
  fetchDownloads()
}

/** The trail as the kit draws it: the root, then each category drilled into; every step but the last takes the operator back to it. */
const trail = computed<UiBreadcrumbItem[]>(() => [
  { label: t('support.downloads.downloadHome'), onSelect: breadcrumbs.value.length > 0 ? navigateHome : undefined, testid: 'downloads-breadcrumb-home' },
  ...breadcrumbs.value.map((crumb, index) => ({
    label: crumb.name,
    onSelect: index < breadcrumbs.value.length - 1 ? (): void => navigateToBreadcrumb(index) : undefined,
    testid: `downloads-breadcrumb-${crumb.id}`,
  })),
])

// The add-download form files new downloads under the level being browsed.
watch(currentCategoryId, (id) => {
  newDownloadCategoryId.value = id ?? 0
})

/** Loads the root level. */
onMounted(() => {
  void fetchAll()
})
</script>

<template>
  <UiStack>
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="downloads-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="downloads-error"
      @retry="fetchAll"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <UiSchemaForm
      v-if="activeTab === DownloadsTab.Category"
      :columns="DOWNLOAD_CATEGORY_FORM_PART.columns"
      :errors="store.fieldErrors"
    >
      <template #field-name>
        <UiInput
          v-model="newCategoryName"
          :placeholder="t('shared.enterCategoryName')"
          data-testid="downloads-category-name"
        />
      </template>
      <template #field-hidden>
        <SupportCheckboxField
          v-model="newCategoryHidden"
          :label="t('support.downloads.checkToHide')"
          testid="downloads-category-hidden"
        />
      </template>
      <template #field-description>
        <UiInput
          v-model="newCategoryDescription"
          :placeholder="t('support.downloads.categoryDescription')"
          data-testid="downloads-category-description"
        />
      </template>
      <template #section-category>
        <UiStack direction="row" justify="center">
          <UiButton :action="UiActionKind.Add"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!newCategoryName.trim()"
            data-testid="downloads-category-add"
            @click="createCategory"
          >
            {{ t('shared.addCategory') }}
          </UiButton>
        </UiStack>
      </template>
    </UiSchemaForm>

    <UiSchemaForm
      v-if="activeTab === DownloadsTab.Download"
      :columns="DOWNLOAD_FILE_FORM_PART.columns"
      :errors="store.fieldErrors"
    >
      <template #field-type>
        <UiSelect
          v-model="newDownloadType"
          :options="typeOptions"
          :placeholder="t('support.downloads.selectType')"
          data-testid="downloads-download-type"
        />
      </template>
      <template #field-title>
        <UiInput
          v-model="newDownloadTitle"
          :placeholder="t('support.downloads.enterDownloadTitle')"
          data-testid="downloads-download-title"
        />
      </template>
      <template #field-description>
        <UiTextarea
          v-model="newDownloadDescription"
          rows="3"
          :placeholder="t('support.downloads.enterDescription')"
          data-testid="downloads-download-description"
        />
      </template>
      <template #field-filename>
        <UiInput
          v-model="newDownloadFilename"
          :placeholder="t('support.downloads.enterFilename')"
          data-testid="downloads-download-filename"
        />
      </template>
      <template #field-categoryId>
        <UiSelect
          v-model="newDownloadCategoryId"
          :options="categoryOptions"
          :placeholder="t('shared.selectCategory')"
          data-testid="downloads-download-category"
        />
      </template>
      <template #field-clientsOnly>
        <SupportCheckboxField
          v-model="newDownloadClientsOnly"
          :label="t('support.downloads.clientsOnly')"
          testid="downloads-download-clients-only"
        />
      </template>
      <template #field-productDownload>
        <SupportCheckboxField
          v-model="newDownloadProductDownload"
          :label="t('support.downloads.productDownload')"
          testid="downloads-download-product"
        />
      </template>
      <template #field-hidden>
        <SupportCheckboxField
          v-model="newDownloadHidden"
          :label="t('shared.hidden')"
          testid="downloads-download-hidden"
        />
      </template>
      <template #section-download>
        <UiText tone="muted" size="sm">{{ t('support.downloads.clientsOnlyRestrictsToLogged') }}</UiText>
        <UiStack direction="row" gap="sm">
          <UiButton :action="UiActionKind.Download"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!newDownloadTitle.trim()"
            data-testid="downloads-download-add"
            @click="createDownload"
          >
            {{ t('support.downloads.addDownload') }}
          </UiButton>
          <UiButton :action="UiActionKind.Cancel"
            :variant="UiButtonVariant.Secondary"
            data-testid="downloads-download-reset"
            @click="resetDownloadForm"
          >
            {{ t('shared.cancel') }}
          </UiButton>
        </UiStack>
      </template>
    </UiSchemaForm>

    <UiBreadcrumb :items="trail" data-testid="downloads-breadcrumbs" />

    <UiDetailCards v-if="loading || visibleCategories.length > 0" :columns="DOWNLOAD_CATEGORIES_PART.columns" :loading="loading">
      <template #card-categories>
        <UiTable :density="UiTableDensity.Comfortable" data-testid="downloads-categories">
          <UiSchemaTableHead :columns="DOWNLOAD_CATEGORIES_TABLE.columns" />
          <UiTableBody divided>
            <UiTableRow
              v-for="category in visibleCategories"
              :key="category.id"
              hoverable
              :data-testid="`downloads-category-${category.id}`"
            >
              <UiTableCell>
                <UiStack direction="row" gap="sm" align="center">
                  <UiIcon :markup="Icon.Folder" />
                  <UiButton
                    :variant="UiButtonVariant.Link"
                    :data-testid="`downloads-category-${category.id}-open`"
                    @click="navigateToCategory(category)"
                  >
                    {{ category.name }} ({{ category.downloadCount }})
                  </UiButton>
                </UiStack>
              </UiTableCell>
              <UiTableCell muted>{{ category.description || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell>
                <UiBadge v-if="category.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="categoryActions(category)" :testid="`downloads-category-${category.id}-actions`" />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </template>
    </UiDetailCards>

    <UiDetailCards :columns="DOWNLOAD_FILES_PART.columns" :loading="loading">
      <template #card-files>
        <UiTable v-if="store.downloads.length > 0" :density="UiTableDensity.Comfortable" data-testid="downloads-files">
          <UiSchemaTableHead :columns="DOWNLOAD_FILES_TABLE.columns" />
          <UiTableBody divided>
            <UiTableRow
              v-for="download in store.downloads"
              :key="download.id"
              hoverable
              :data-testid="`downloads-download-${download.id}`"
            >
              <UiTableCell>
                <UiStack direction="row" gap="sm" align="center">
                  <UiIcon :markup="Icon.File" />
                  <UiButton
                    :variant="UiButtonVariant.Link"
                    :data-testid="`downloads-download-${download.id}-open`"
                    @click="editingDownload = download"
                  >
                    {{ download.title }}
                  </UiButton>
                </UiStack>
              </UiTableCell>
              <UiTableCell muted>{{ download.description || EMPTY_VALUE }}</UiTableCell>
              <UiTableCell numeric muted>{{ download.downloadCount }}</UiTableCell>
              <UiTableCell>
                <UiStack direction="row" gap="xs" wrap>
                  <UiBadge v-if="download.clientsOnly" :tone="UiTone.Primary" small>
                    {{ t('support.downloads.clientsOnly') }}
                  </UiBadge>
                  <UiBadge v-if="download.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
                </UiStack>
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="downloadActions(download)" :testid="`downloads-download-${download.id}-actions`" />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>

        <UiEmptyState
          v-else
          :title="t('support.downloads.noDownloadsFound')"
          :description="t('support.downloads.addADownloadUsingThe')"
          data-testid="downloads-files-empty"
        />
      </template>
    </UiDetailCards>

    <DownloadCategoryEditDialog
      :category="editingCategory"
      :categories="store.categories"
      :saving="saving"
      @save="updateCategory"
      @close="editingCategory = null"
    />

    <DownloadItemEditDialog
      :download="editingDownload"
      :categories="store.categories"
      :saving="saving"
      @save="updateDownload"
      @close="editingDownload = null"
    />

    <UiDialog
      variant="confirm"
      :open="deleteRequest !== null"
      :title="t('support.downloads.deleteTitle', { item: deleteRequest?.type === DownloadTarget.Category ? t('shared.category') : t('shared.download') })"
      :message="t('support.downloads.deleteMessage', { name: deleteRequest?.name ?? '' })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
