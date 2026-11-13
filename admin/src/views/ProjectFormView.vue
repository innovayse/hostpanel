<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront project.
 *
 * Detects edit mode from the route params, loads the project when editing, carries the title,
 * summary and description in en / ru / hy tabs, and manages the gallery images (the first is the
 * cover). Asks before leaving with unsaved edits.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { Icon } from '@/lib/icons'
import { blankTranslations, collectTranslations, formsFromTranslations } from '@/lib/storefrontTranslations'
import { useProjectsStore } from '@/stores/projects'
import type { Project, ProjectPayload, ProjectTranslation } from '@/types/project'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Projects store: loads, creates and updates the project and uploads its images. */
const store = useProjectsStore()

/**
 * Creates a blank text form.
 *
 * @returns Empty title, summary and description.
 */
const blankText = (): ProjectTranslation => ({ title: '', summary: '', description: '' })

/** One gallery slot; the id keeps its field and its upload state attached when slots are removed. */
interface GallerySlot {
  /** Stable identity of the slot. */
  id: number
  /** Address of the image, empty while unset. */
  url: string
}

/** Next slot id. */
let nextSlotId = 0

/**
 * Makes a gallery slot.
 *
 * @param url - Address of the image.
 * @returns A slot with a fresh id.
 */
const makeSlot = (url: string): GallerySlot => ({ id: nextSlotId++, url })

/** The form state; any change marks the form dirty. */
const form = reactive({
  slug: '',
  images: [] as GallerySlot[],
  url: '',
  sortOrder: 0,
  isActive: true,
  translations: blankTranslations(blankText),
})

/** The locale tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('project-form-tab')

/**
 * Copies a stored project into the form.
 *
 * @param item - The stored project.
 */
const populate = (item: Project): void => {
  form.slug = item.slug
  form.images = item.imageUrls.map(makeSlot)
  form.url = item.url ?? ''
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  form.translations = formsFromTranslations(item.translations, blankText)
}

/**
 * Builds the payload from the form: trimmed text, no empty gallery slots, and only the locales
 * with a title.
 *
 * @returns The create / update payload.
 */
const build = (): ProjectPayload => ({
  slug: form.slug.trim(),
  imageUrls: form.images.map((slot) => slot.url.trim()).filter((url) => url !== ''),
  url: form.url.trim(),
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  translations: collectTranslations(
    form.translations,
    (text) => text.title.trim() !== '',
    (text) => ({ title: text.title.trim(), summary: text.summary.trim(), description: text.description.trim() }),
  ),
})

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Project, ProjectPayload>({ store, form, listPath: AppRoute.StorefrontProjects, populate, build })

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('storefront.projects.title'), to: AppRoute.StorefrontProjects },
  { label: flow.isEditMode.value ? t('storefront.projects.editTitle') : t('storefront.projects.createTitle') },
])

/** Adds an empty gallery slot. */
const addImage = (): void => {
  form.images.push(makeSlot(''))
}

/**
 * Removes a gallery slot.
 *
 * @param index - Position of the slot.
 */
const removeImage = (index: number): void => {
  form.images.splice(index, 1)
}

/**
 * Sets the address of a gallery slot.
 *
 * @param index - Position of the slot.
 * @param url - The new address.
 */
const setImage = (index: number, url: string): void => {
  const slot = form.images[index]
  if (slot) {
    slot.url = url
  }
}
</script>

<template>
  <StorefrontFormShell
    testid="project"
    :breadcrumb="breadcrumb"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.projects.create')"
    :loading="flow.loadingEntry.value"
    :saving="flow.saving.value"
    :dirty="flow.isDirty.value"
    :error="flow.saveError.value"
    :load-error="flow.loadError.value"
    @retry="flow.reload"
    @save="flow.save"
    @cancel="flow.cancel"
  >
    <SettingsSection :title="t('storefront.form.general')">
      <UiField :label="t('storefront.projects.slug')" :hint="t('storefront.projects.slugHint')" :error="store.fieldErrors.slug">
        <template #default="{ id, describedby }">
          <UiInput
            :id="id"
            v-model="form.slug"
            type="text"
            :placeholder="t('storefront.projects.slugPlaceholder')"
            :aria-describedby="describedby"
            data-testid="project-form-slug"
          />
        </template>
      </UiField>

      <UiField :label="t('storefront.projects.liveUrl')" :error="store.fieldErrors.url">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.url" type="text" :placeholder="t('storefront.projects.liveUrlPlaceholder')" data-testid="project-form-url" />
        </template>
      </UiField>

      <UiField :label="t('storefront.form.sortOrder')" :error="store.fieldErrors.sortOrder">
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="project-form-sort-order" />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('storefront.projects.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="form.isActive" data-testid="project-form-active" />
      </UiStack>
    </SettingsSection>

    <SettingsSection :title="t('storefront.projects.gallery')" :description="t('storefront.projects.galleryHint')">
      <UiStack v-for="(slot, index) in form.images" :key="slot.id" direction="row" align="start" gap="sm">
        <StorefrontImageField
          class="flex-1"
          :model-value="slot.url"
          :label="t('storefront.projects.imageNumber', { number: index + 1 })"
          :upload="store.uploadImage"
          :upload-error="store.uploadError"
          :testid="`project-form-image-${index}`"
          @update:model-value="setImage(index, $event)"
        />
        <UiIconButton
          :label="t('storefront.projects.removeImage')"
          size="sm"
          :data-testid="`project-form-image-delete-${index}`"
          @click="removeImage(index)"
        >
          <UiIcon :markup="Icon.Trash" size="sm" />
        </UiIconButton>
      </UiStack>
      <UiButton type="button" variant="link" size="sm" class="self-start" data-testid="project-form-image-add" @click="addImage">
        <UiIcon :markup="Icon.Add" size="sm" />
        {{ t('storefront.projects.addImage') }}
      </UiButton>
    </SettingsSection>

    <SettingsSection :title="t('storefront.form.translations')">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiField :label="t('storefront.projects.projectTitle')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="form.translations[activeLocale].title"
            type="text"
            :placeholder="t('storefront.projects.titlePlaceholder')"
            data-testid="project-form-title"
          />
        </template>
      </UiField>

      <UiField :label="t('storefront.projects.summary')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="form.translations[activeLocale].summary"
            type="text"
            :placeholder="t('storefront.projects.summaryPlaceholder')"
            data-testid="project-form-summary"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.description')">
        <template #default="{ id }">
          <UiTextarea
            :id="id"
            v-model="form.translations[activeLocale].description"
            rows="5"
            :placeholder="t('storefront.projects.descriptionPlaceholder')"
            data-testid="project-form-description"
            class="resize-none"
          />
        </template>
      </UiField>
    </SettingsSection>
  </StorefrontFormShell>
</template>
