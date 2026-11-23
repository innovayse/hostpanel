<script setup lang="ts">
/**
 * Dialog that edits one downloadable file (category, type, title, description, filename and the
 * access flags). It owns the form fields; the page owns saving: the dialog only reports the
 * finished payload.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useDownloadOptions } from '@/composables/useDownloadOptions'
import type { DownloadCategory } from '@/types/downloadcategory'
import { DownloadType } from '@/types/downloaditem'
import type { DownloadItem, DownloadItemPayload } from '@/types/downloaditem'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiDirection, UiGap } from '@/utils/ui/layout'
import { UiActionKind } from '@/utils/ui/actions'

/** Props for DownloadItemEditDialog. */
const props = defineProps<{
  /** The download being edited; null keeps the dialog closed. */
  download: DownloadItem | null
  /** Every category, to file the download under. */
  categories: DownloadCategory[]
  /** Whether a save is in flight. */
  saving: boolean
}>()

/** Events of DownloadItemEditDialog. */
const emit = defineEmits<{
  /** The form was confirmed; carries the payload to save. */
  save: [payload: DownloadItemPayload]
  /** The dialog was dismissed. */
  close: []
}>()

const { t } = useI18n()

const { typeOptions, categoryOptions } = useDownloadOptions(computed(() => props.categories))

/** Edit form: title. */
const title = ref('')

/** Edit form: description. */
const description = ref('')

/** Edit form: file type. */
const type = ref<DownloadType>(DownloadType.Zip)

/** Edit form: filename. */
const filename = ref('')

/** Edit form: category ID (0 = none). */
const categoryId = ref<number>(0)

/** Edit form: clients-only flag. */
const clientsOnly = ref(false)

/** Edit form: product download flag. */
const productDownload = ref(false)

/** Edit form: hidden flag. */
const isHidden = ref(false)

/**
 * Reports the finished payload.
 */
const submit = (): void => {
  emit('save', {
    title: title.value.trim(),
    description: description.value.trim(),
    type: type.value,
    filename: filename.value.trim(),
    categoryId: categoryId.value || null,
    clientsOnly: clientsOnly.value,
    productDownload: productDownload.value,
    isHidden: isHidden.value,
  })
}

// The form follows whichever download the page opens.
watch(
  () => props.download,
  (download) => {
    if (download) {
      title.value = download.title
      description.value = download.description
      type.value = download.type
      filename.value = download.filename
      categoryId.value = download.categoryId
      clientsOnly.value = download.clientsOnly
      productDownload.value = download.productDownload
      isHidden.value = download.isHidden
    }
  },
  { immediate: true },
)
</script>

<template>
  <UiDialog :open="download !== null" :title="t('support.downloads.editDownload')" :size="UiDialogSize.Lg" @close="emit('close')">
    <UiStack>
      <UiField :label="t('shared.category')">
        <UiSelect
          v-model="categoryId"
          :options="categoryOptions"
          :placeholder="t('shared.selectCategory')"
          data-testid="downloads-edit-download-category"
        />
      </UiField>
      <UiField :label="t('shared.type')">
        <UiSelect
          v-model="type"
          :options="typeOptions"
          :placeholder="t('support.downloads.selectType')"
          data-testid="downloads-edit-download-type"
        />
      </UiField>
      <UiField :label="t('shared.title')">
        <UiInput
          v-model="title"
          :placeholder="t('support.downloads.downloadTitle')"
          data-testid="downloads-edit-download-title"
        />
      </UiField>
      <UiField :label="t('shared.description')">
        <UiTextarea
          v-model="description"
          rows="3"
          :placeholder="t('support.downloads.description')"
          data-testid="downloads-edit-download-description"
        />
      </UiField>
      <UiField :label="t('support.downloads.filename')">
        <UiInput
          v-model="filename"
          :placeholder="t('support.downloads.filename')"
          data-testid="downloads-edit-download-filename"
        />
      </UiField>
      <UiField :label="t('support.downloads.downloads')">
        <UiInput
          :model-value="download ? String(download.downloadCount) : ''"
          type="number"
          disabled
          :placeholder="t('support.downloads.downloadCount')"
          data-testid="downloads-edit-download-count"
        />
      </UiField>
      <UiStack :direction="UiDirection.Row" :gap="UiGap.Lg" wrap>
        <SupportCheckboxField
          v-model="clientsOnly"
          :label="t('support.downloads.clientsOnly')"
          testid="downloads-edit-download-clients-only"
        />
        <SupportCheckboxField
          v-model="productDownload"
          :label="t('support.downloads.productDownload')"
          testid="downloads-edit-download-product"
        />
        <SupportCheckboxField
          v-model="isHidden"
          :label="t('shared.hidden')"
          testid="downloads-edit-download-hidden"
        />
      </UiStack>
    </UiStack>
    <template #footer>
      <UiButton :action="UiActionKind.Cancel" :variant="UiButtonVariant.Secondary" data-testid="downloads-edit-download-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton :action="UiActionKind.Save"
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        :disabled="!title.trim()"
        data-testid="downloads-edit-download-save"
        @click="submit"
      >
        {{ t('shared.save') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
