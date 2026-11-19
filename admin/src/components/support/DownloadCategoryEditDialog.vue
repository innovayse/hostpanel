<script setup lang="ts">
/**
 * Dialog that edits one download category (name, description, parent, hidden). It owns the form
 * fields; the page owns saving: the dialog only reports the finished payload.
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
import type { DownloadCategory, DownloadCategoryPayload } from '@/types/downloadcategory'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiActionKind } from '@/utils/ui/actions'

/** Props for DownloadCategoryEditDialog. */
const props = defineProps<{
  /** The category being edited; null keeps the dialog closed. */
  category: DownloadCategory | null
  /** Every category, to choose a parent from. */
  categories: DownloadCategory[]
  /** Whether a save is in flight. */
  saving: boolean
}>()

/** Events of DownloadCategoryEditDialog. */
const emit = defineEmits<{
  /** The form was confirmed; carries the payload to save. */
  save: [payload: DownloadCategoryPayload]
  /** The dialog was dismissed. */
  close: []
}>()

const { t } = useI18n()

/** Edit form: category name. */
const name = ref('')

/** Edit form: category description. */
const description = ref('')

/** Edit form: hidden flag. */
const isHidden = ref(false)

/** Edit form: parent category ID (0 = top level). */
const parentId = ref<number>(0)

/** Parent options: top level plus every category except the one being edited. */
const parentOptions = computed(() => [
  { value: 0, label: t('support.predefinedReplies.topLevel') },
  ...props.categories
    .filter((candidate) => candidate.id !== props.category?.id)
    .map((candidate) => ({ value: candidate.id, label: candidate.name })),
])

/**
 * Reports the finished payload.
 */
const submit = (): void => {
  emit('save', {
    name: name.value.trim(),
    description: description.value.trim(),
    isHidden: isHidden.value,
    parentCategoryId: parentId.value === 0 ? null : parentId.value,
  })
}

// The form follows whichever category the page opens.
watch(
  () => props.category,
  (category) => {
    if (category) {
      name.value = category.name
      description.value = category.description
      isHidden.value = category.isHidden
      parentId.value = category.parentCategoryId ?? 0
    }
  },
  { immediate: true },
)
</script>

<template>
  <UiDialog :open="category !== null" :title="t('support.downloads.editCategory')" @close="emit('close')">
    <UiStack>
      <UiField :label="t('support.downloads.parentCategory')">
        <UiSelect
          v-model="parentId"
          :options="parentOptions"
          :placeholder="t('shared.selectCategory')"
          data-testid="downloads-edit-category-parent"
        />
      </UiField>
      <UiField :label="t('shared.categoryName')">
        <UiInput
          v-model="name"
          :placeholder="t('shared.categoryName2')"
          data-testid="downloads-edit-category-name"
        />
      </UiField>
      <UiField :label="t('shared.description')">
        <UiInput
          v-model="description"
          :placeholder="t('support.downloads.categoryDescription')"
          data-testid="downloads-edit-category-description"
        />
      </UiField>
      <SupportCheckboxField
        v-model="isHidden"
        :label="t('shared.hidden')"
        testid="downloads-edit-category-hidden"
      />
    </UiStack>
    <template #footer>
      <UiButton :action="UiActionKind.Cancel" :variant="UiButtonVariant.Secondary" data-testid="downloads-edit-category-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton :action="UiActionKind.Save"
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        :disabled="!name.trim()"
        data-testid="downloads-edit-category-save"
        @click="submit"
      >
        {{ t('shared.saveChanges') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
