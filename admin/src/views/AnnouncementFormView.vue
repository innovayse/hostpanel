<script setup lang="ts">
/**
 * Announcement form shared by create and edit. The mode comes from the route: an `:id` param
 * means edit and loads the announcement first.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { toDateInputValue } from '@/lib/format'
import { useAnnouncementsStore } from '@/stores/announcements'
import { UiButtonVariant } from '@/utils/ui/button'
import { AppRoute } from '@/lib/routes'
import schema, { ANNOUNCEMENT_DATE_FIELD_KEY } from '@/views/AnnouncementFormView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Shape of an announcement in the form. */
interface AnnouncementForm {
  /** Day the announcement was created as ISO `YYYY-MM-DD`; read-only, the server sets it. */
  createdOn: string | null
  /** Announcement title. */
  title: string
  /** Full announcement body. */
  content: string
  /** Whether the announcement is published. */
  published: boolean
}

const route = useRoute()
const router = useRouter()
const store = useAnnouncementsStore()

/** Whether we are editing an existing announcement. */
const isEditMode = computed<boolean>(() => Boolean(route.params.id))

/** The announcement ID when in edit mode. */
const announcementId = computed<number | null>(() => (route.params.id ? Number(route.params.id) : null))

/** Page title reflecting the current mode. */
const pageTitle = computed<string>(() =>
  isEditMode.value ? t('support.announcementForm.pageTitleEdit') : t('support.announcementForm.pageTitleCreate'),
)

/** Form data. */
const form = ref<AnnouncementForm>({
  createdOn: null,
  title: '',
  content: '',
  published: false,
})

/** Whether the form is being submitted. */
const saving = ref(false)

/** Whether the existing announcement is being loaded; true from the first paint of an edit, so the form never flashes empty. */
const loading = ref(isEditMode.value)

/**
 * The sections of the schema; a new announcement has no creation date yet, so its field is left out (the skeleton of an
 * announcement being read keeps it, because the stored one will have it).
 */
const columns = computed(() =>
  form.value.createdOn || loading.value
    ? schema.columns
    : schema.columns.map((column) =>
      column.map((section) => ({ ...section, fields: section.fields.filter((field) => field.key !== ANNOUNCEMENT_DATE_FIELD_KEY) })),
    ),
)

/** The sentence of a refused load of the announcement being edited, null when it loaded. */
const loadError = ref<string | null>(null)

/**
 * Loads the announcement being edited into the form.
 */
const fetchAnnouncement = async (): Promise<void> => {
  if (!announcementId.value) {
    return
  }
  loading.value = true
  loadError.value = (await store.fetchById(announcementId.value)) ? null : store.error
  loading.value = false
  const data = store.current
  if (data) {
    form.value = {
      createdOn: toDateInputValue(data.createdAt) || null,
      title: data.title,
      content: data.content,
      published: data.isPublished,
    }
  }
}

/**
 * Creates or updates the announcement according to the mode, then returns to the list.
 */
const handleSave = async (): Promise<void> => {
  saving.value = true
  // The API takes the title, body and publish flag; the creation date is set server-side.
  const payload = {
    title: form.value.title,
    content: form.value.content,
    isPublished: form.value.published,
  }

  let saved: boolean
  if (isEditMode.value && announcementId.value) {
    saved = await store.update(announcementId.value, payload)
  } else {
    saved = (await store.create(payload)) !== null
  }
  saving.value = false

  if (saved) {
    router.push(AppRoute.SupportAnnouncements)
  }
}

onMounted(() => {
  if (isEditMode.value) {
    fetchAnnouncement()
  }
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="pageTitle"
      :subtitle="isEditMode ? t('support.announcementForm.updateTheAnnouncementDetailsBelow') : t('support.announcementForm.createANewClientFacing')"
    />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="announcement-form-load-error"
      @retry="fetchAnnouncement"
    />

    <UiAlert v-else-if="store.error" variant="error" data-testid="announcement-form-error">{{ store.error }}</UiAlert>

    <UiSchemaForm
      v-if="loadError === null"
      :columns="columns"
      :loading="loading"
      :errors="store.fieldErrors"
      data-testid="announcement-form-loading"
    >
      <template #field-title="{ id, invalid, describedby }">
        <UiInput
          :id="id"
          v-model="form.title"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('support.announcementForm.announcementTitle')"
          data-testid="announcement-form-title"
        />
      </template>

      <template #field-createdOn="{ id }">
        <UiInput
          :id="id"
          :model-value="form.createdOn"
          readonly
          :placeholder="t('shared.date')"
          data-testid="announcement-form-created-on"
        />
      </template>

      <template #field-content="{ id, invalid, describedby }">
        <UiTextarea
          :id="id"
          v-model="form.content"
          rows="12"
          :invalid="invalid"
          :aria-describedby="describedby"
          :placeholder="t('support.announcementForm.announcementContent')"
          data-testid="announcement-form-content"
        />
      </template>

      <template #field-published="{ id }">
        <UiCheckbox :id="id" v-model="form.published" :aria-label="t('shared.published')" data-testid="announcement-form-published" />
      </template>

      <template #section-announcement>
        <UiStack direction="row" gap="sm">
          <UiButton :action="UiActionKind.Save"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!form.title.trim()"
            data-testid="announcement-form-save"
            @click="handleSave"
          >
            {{ isEditMode ? t('support.announcementForm.updateAnnouncement') : t('support.announcementForm.saveAnnouncement') }}
          </UiButton>
          <UiLink :action="UiActionKind.Cancel"
            :to="AppRoute.SupportAnnouncements"
            :variant="UiButtonVariant.Secondary"
            data-testid="announcement-form-cancel"
          >
            {{ t('shared.cancel') }}
          </UiLink>
        </UiStack>
      </template>
    </UiSchemaForm>
  </UiStack>
</template>
