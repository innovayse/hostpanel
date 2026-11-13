<script setup lang="ts">
/**
 * Announcement form shared by create and edit. The mode comes from the route: an `:id` param
 * means edit and loads the announcement first.
 */
import { computed, onMounted, provide, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { toDateInputValue } from '@/lib/format'
import { useAnnouncementsStore } from '@/stores/announcements'
import { UiButtonVariant } from '@/utils/ui/button'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { AppRoute } from '@/lib/routes'

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

// Every field of the form stays a placeholder while the announcement is read.
provide(UI_FIELD_LOADING, loading)

onMounted(() => {
  if (isEditMode.value) {
    fetchAnnouncement()
  }
})
</script>

<template>
  <UiStack>
    <UiStack direction="row" gap="sm" align="center">
      <UiLink :to="AppRoute.SupportAnnouncements" :variant="UiButtonVariant.Link" data-testid="announcement-form-breadcrumb">
        {{ t('shared.announcements') }}
      </UiLink>
      <UiText tone="muted" size="md">/</UiText>
      <UiText tone="secondary" size="md">{{ pageTitle }}</UiText>
    </UiStack>

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

    <UiCard v-if="loadError === null" padding="md" :aria-busy="loading ? 'true' : undefined" data-testid="announcement-form-loading">
      <UiStack>
        <UiGrid :columns="2">
          <UiField :label="t('shared.title')" :error="store.fieldErrors.title">
            <UiInput
              v-model="form.title"
              :placeholder="t('support.announcementForm.announcementTitle')"
              data-testid="announcement-form-title"
            />
          </UiField>
          <UiField v-if="form.createdOn || loading" :label="t('shared.date')">
            <UiInput
              :model-value="form.createdOn"
              readonly
              :placeholder="t('shared.date')"
              data-testid="announcement-form-created-on"
            />
          </UiField>
        </UiGrid>

        <UiField :label="t('support.announcementForm.announcement')" :error="store.fieldErrors.content">
          <UiTextarea
            v-model="form.content"
            rows="12"
            :placeholder="t('support.announcementForm.announcementContent')"
            data-testid="announcement-form-content"
          />
        </UiField>

        <UiStack direction="row" gap="sm" align="center">
          <UiCheckbox v-model="form.published" data-testid="announcement-form-published" />
          <UiText tone="secondary" size="md">{{ t('shared.published') }}</UiText>
        </UiStack>

        <UiStack direction="row" gap="sm">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!form.title.trim()"
            data-testid="announcement-form-save"
            @click="handleSave"
          >
            {{ isEditMode ? t('support.announcementForm.updateAnnouncement') : t('support.announcementForm.saveAnnouncement') }}
          </UiButton>
          <UiLink
            :to="AppRoute.SupportAnnouncements"
            :variant="UiButtonVariant.Secondary"
            data-testid="announcement-form-cancel"
          >
            {{ t('shared.cancel') }}
          </UiLink>
        </UiStack>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
