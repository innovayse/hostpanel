<script setup lang="ts">
/**
 * Downloads management: categories and downloadable files, with nested category navigation and
 * add, edit and delete for both. Every change reports its outcome (success, or the API's sentence)
 * under the header, a delete is confirmed first, and a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DownloadCategoryEditDialog from '@/components/support/DownloadCategoryEditDialog.vue'
import DownloadItemEditDialog from '@/components/support/DownloadItemEditDialog.vue'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
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
import { UiSize } from '@/utils/ui/size'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiTone } from '@/utils/ui/tone'

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

/** Tab items for the kit's tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: DownloadsTab.Category, label: t('shared.addCategory'), testid: 'downloads-tab-category' },
  { value: DownloadsTab.Download, label: t('support.downloads.addDownload'), testid: 'downloads-tab-download' },
])

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
const categoryActions = (category: DownloadCategory): UiRowAction[] => {
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
    { label: t('shared.edit'), testid: `downloads-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `downloads-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
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
    <UiPageHeader
      :title="t('support.downloads.downloads')"
      :subtitle="t('support.downloads.manageDownloadableFilesAndCategories')"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="downloads-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="downloads-error"
      @retry="fetchAll"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <UiCard v-if="activeTab === DownloadsTab.Category" padding="md">
      <UiStack>
        <UiStack direction="row" align="end" wrap>
          <UiField :label="t('shared.categoryName')" :error="store.fieldErrors.name" class="flex-1">
            <UiInput
              v-model="newCategoryName"
              :placeholder="t('shared.enterCategoryName')"
              data-testid="downloads-category-name"
            />
          </UiField>
          <SupportCheckboxField
            v-model="newCategoryHidden"
            :label="t('support.downloads.checkToHide')"
            testid="downloads-category-hidden"
          />
        </UiStack>
        <UiField :label="t('shared.description')">
          <UiInput
            v-model="newCategoryDescription"
            :placeholder="t('support.downloads.categoryDescription')"
            data-testid="downloads-category-description"
          />
        </UiField>
        <UiStack direction="row" justify="center">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!newCategoryName.trim()"
            data-testid="downloads-category-add"
            @click="createCategory"
          >
            {{ saving ? t('shared.saving') : t('shared.addCategory') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiCard v-if="activeTab === DownloadsTab.Download" padding="md">
      <UiStack>
        <UiField :label="t('shared.type')">
          <UiSelect
            v-model="newDownloadType"
            :options="typeOptions"
            :placeholder="t('support.downloads.selectType')"
            data-testid="downloads-download-type"
          />
        </UiField>
        <UiField :label="t('shared.title')" :error="store.fieldErrors.title">
          <UiInput
            v-model="newDownloadTitle"
            :placeholder="t('support.downloads.enterDownloadTitle')"
            data-testid="downloads-download-title"
          />
        </UiField>
        <UiField :label="t('shared.description')">
          <UiTextarea
            v-model="newDownloadDescription"
            rows="3"
            :placeholder="t('support.downloads.enterDescription')"
            data-testid="downloads-download-description"
          />
        </UiField>
        <UiField :label="t('support.downloads.filename')" :error="store.fieldErrors.filename">
          <UiInput
            v-model="newDownloadFilename"
            :placeholder="t('support.downloads.enterFilename')"
            data-testid="downloads-download-filename"
          />
        </UiField>
        <UiField :label="t('shared.category')">
          <UiSelect
            v-model="newDownloadCategoryId"
            :options="categoryOptions"
            :placeholder="t('shared.selectCategory')"
            data-testid="downloads-download-category"
          />
        </UiField>
        <UiStack direction="row" gap="lg" wrap>
          <SupportCheckboxField
            v-model="newDownloadClientsOnly"
            :label="t('support.downloads.clientsOnly')"
            testid="downloads-download-clients-only"
          />
          <SupportCheckboxField
            v-model="newDownloadProductDownload"
            :label="t('support.downloads.productDownload')"
            testid="downloads-download-product"
          />
          <SupportCheckboxField
            v-model="newDownloadHidden"
            :label="t('shared.hidden')"
            testid="downloads-download-hidden"
          />
        </UiStack>
        <UiText tone="muted" size="sm">{{ t('support.downloads.clientsOnlyRestrictsToLogged') }}</UiText>
        <UiStack direction="row" gap="sm">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!newDownloadTitle.trim()"
            data-testid="downloads-download-add"
            @click="createDownload"
          >
            {{ saving ? t('shared.saving') : t('support.downloads.addDownload') }}
          </UiButton>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            data-testid="downloads-download-reset"
            @click="resetDownloadForm"
          >
            {{ t('shared.cancel') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiStack direction="row" gap="xs" align="center" wrap data-testid="downloads-breadcrumbs">
      <UiText tone="muted" size="md">{{ t('shared.youAreHere') }}</UiText>
      <UiButton
        :variant="breadcrumbs.length > 0 ? UiButtonVariant.Link : UiButtonVariant.Plain"
        :size="UiSize.Sm"
        data-testid="downloads-breadcrumb-home"
        @click="navigateHome"
      >
        {{ t('support.downloads.downloadHome') }}
      </UiButton>
      <template v-for="(crumb, index) in breadcrumbs" :key="crumb.id">
        <UiText tone="muted" size="md">&gt;</UiText>
        <UiButton
          v-if="index < breadcrumbs.length - 1"
          :variant="UiButtonVariant.Link"
          :size="UiSize.Sm"
          :data-testid="`downloads-breadcrumb-${crumb.id}`"
          @click="navigateToBreadcrumb(index)"
        >
          {{ crumb.name }}
        </UiButton>
        <UiText v-else tone="secondary" size="md" class="font-medium">{{ crumb.name }}</UiText>
      </template>
    </UiStack>

    <UiSkeleton v-if="loading" :lines="4" data-testid="downloads-loading" />

    <template v-else>
      <UiCard v-if="visibleCategories.length > 0" padding="md" data-testid="downloads-categories">
        <UiStack>
          <UiHeading :level="2" variant="card">{{ t('shared.categories') }}</UiHeading>
          <UiGrid :columns="3">
            <UiCard
              v-for="category in visibleCategories"
              :key="category.id"
              padding="md"
              :data-testid="`downloads-category-${category.id}`"
            >
              <UiStack gap="sm">
                <UiStack direction="row" justify="between" align="start">
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
                  <UiRowActions :actions="categoryActions(category)" />
                </UiStack>
                <UiText v-if="category.description" tone="muted" size="sm">{{ category.description }}</UiText>
                <UiStack direction="row">
                  <UiBadge v-if="category.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
                </UiStack>
              </UiStack>
            </UiCard>
          </UiGrid>
        </UiStack>
      </UiCard>

      <UiCard padding="md" data-testid="downloads-files">
        <UiStack>
          <UiHeading :level="2" variant="card">{{ t('support.downloads.files') }}</UiHeading>

          <UiStack v-if="store.downloads.length > 0" gap="sm">
            <UiCard
              v-for="download in store.downloads"
              :key="download.id"
              padding="sm"
              :data-testid="`downloads-download-${download.id}`"
            >
              <UiStack gap="sm">
                <UiStack direction="row" justify="between" align="start">
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
                  <UiIconButton
                    :label="t('shared.delete')"
                    :size="UiSize.Sm"
                    :data-testid="`downloads-download-${download.id}-delete`"
                    @click="deleteRequest = { type: DownloadTarget.Download, id: download.id, name: download.title }"
                  >
                    <UiIcon :markup="Icon.Trash" :size="UiSize.Sm" />
                  </UiIconButton>
                </UiStack>
                <UiText v-if="download.description" tone="muted" size="sm">{{ download.description }}</UiText>
                <UiStack direction="row" gap="sm" align="center">
                  <UiText tone="muted" size="xs">
                    {{ t('support.downloads.downloadsDownloadcount', { downloadCount: download.downloadCount }) }}
                  </UiText>
                  <UiBadge v-if="download.clientsOnly" :tone="UiTone.Primary" small>
                    {{ t('support.downloads.clientsOnly') }}
                  </UiBadge>
                  <UiBadge v-if="download.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
                </UiStack>
              </UiStack>
            </UiCard>
          </UiStack>

          <UiEmptyState
            v-else
            :title="t('support.downloads.noDownloadsFound')"
            :description="t('support.downloads.addADownloadUsingThe')"
            data-testid="downloads-files-empty"
          />
        </UiStack>
      </UiCard>
    </template>

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
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      tone="danger"
      @confirm="handleDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
