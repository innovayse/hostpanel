<script setup lang="ts">
/**
 * Network issue form shared by create and edit. The mode comes from the route: an `:id` param
 * means edit and loads the issue first. A refused load offers a retry instead of an empty form; a
 * refused save shows the API's sentence next to the save button, a saved issue returns to the list.
 */
import { computed, onMounted, provide, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { toDateInputValue, toIsoDay } from '@/lib/format'
import { useNetworkIssuesStore } from '@/stores/networkIssues'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'
import { UiButtonVariant } from '@/utils/ui/button'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Shape of the network issue form data. */
interface NetworkIssueForm {
  /** Issue title. */
  title: string
  /** Issue type. */
  type: NetworkIssueType
  /** Affected server name (optional). */
  server: string
  /** Priority level. */
  priority: NetworkIssuePriority
  /** Current status (edit mode only). */
  status: NetworkIssueStatus
  /** Start day as ISO `YYYY-MM-DD`. */
  startDate: string | null
  /** End day as ISO `YYYY-MM-DD` (optional). */
  endDate: string | null
  /** Detailed description of the issue. */
  description: string
}

const route = useRoute()
const router = useRouter()
const store = useNetworkIssuesStore()

/** Whether we are editing an existing issue. */
const isEditMode = computed<boolean>(() => Boolean(route.params.id))

/** The issue ID when in edit mode. */
const issueId = computed<number | null>(() => (route.params.id ? Number(route.params.id) : null))

/** Page title reflecting the current mode. */
const pageTitle = computed<string>(() =>
  isEditMode.value ? t('support.networkIssueForm.pageTitleEdit') : t('support.networkIssueForm.pageTitleCreate'),
)

/** Type select options. */
const typeOptions = computed(() => [
  { value: NetworkIssueType.Server, label: t('clients.clientServiceDetail.server') },
  { value: NetworkIssueType.Other, label: t('support.networkIssueForm.other') },
])

/** Priority select options. */
const priorityOptions = computed(() => [
  { value: NetworkIssuePriority.Low, label: t('support.networkIssueForm.low') },
  { value: NetworkIssuePriority.Medium, label: t('support.networkIssueForm.medium') },
  { value: NetworkIssuePriority.High, label: t('support.networkIssueForm.high') },
  { value: NetworkIssuePriority.Critical, label: t('support.networkIssueForm.critical') },
])

/** Status select options (edit mode only). */
const statusOptions = computed(() => [
  { value: NetworkIssueStatus.Reported, label: t('support.networkIssueForm.reported') },
  { value: NetworkIssueStatus.Investigating, label: t('support.networkIssueForm.investigating') },
  { value: NetworkIssueStatus.Scheduled, label: t('support.networkIssueForm.scheduled') },
  { value: NetworkIssueStatus.Resolved, label: t('support.networkIssueForm.resolved') },
])

/** Form data. */
const form = ref<NetworkIssueForm>({
  title: '',
  type: NetworkIssueType.Server,
  server: '',
  priority: NetworkIssuePriority.Medium,
  status: NetworkIssueStatus.Reported,
  startDate: toIsoDay(new Date()),
  endDate: null,
  description: '',
})

/** Whether the form is being submitted. */
const saving = ref(false)

/** Whether the existing issue is being loaded. */
const loading = ref(false)

// While the issue loads every field of the form draws a placeholder in place of its control.
provide(UI_FIELD_LOADING, loading)

/** Whether the last load of the issue was refused. */
const loadFailed = ref(false)

/** The API's sentence for the last refused save, null when none. */
const saveError = ref<string | null>(null)

/**
 * Loads the issue being edited into the form.
 */
const fetchIssue = async (): Promise<void> => {
  if (!issueId.value) {
    return
  }
  loading.value = true
  loadFailed.value = !(await store.fetchById(issueId.value))
  loading.value = false
  const data = store.current
  if (data) {
    form.value = {
      title: data.title,
      type: data.type,
      server: data.server ?? '',
      priority: data.priority,
      status: data.status,
      startDate: toDateInputValue(data.startDate) || null,
      endDate: toDateInputValue(data.endDate) || null,
      description: data.description ?? '',
    }
  }
}

/**
 * Creates or updates the issue according to the mode, then returns to the list.
 */
const handleSave = async (): Promise<void> => {
  saveError.value = null
  saving.value = true
  const payload = {
    title: form.value.title,
    type: form.value.type,
    server: form.value.server || null,
    priority: form.value.priority,
    status: form.value.status,
    startDate: form.value.startDate ? new Date(form.value.startDate).toISOString() : '',
    endDate: form.value.endDate ? new Date(form.value.endDate).toISOString() : null,
    description: form.value.description,
  }

  let saved: boolean
  if (isEditMode.value && issueId.value) {
    saved = await store.update(issueId.value, payload)
  } else {
    saved = (await store.create(payload)) !== null
  }
  saving.value = false

  if (saved) {
    router.push(AppRoute.SupportNetworkIssues)
  } else {
    saveError.value = store.error ?? t('errors.noAnswer')
  }
}

onMounted(() => {
  if (isEditMode.value) {
    fetchIssue()
  }
})
</script>

<template>
  <UiStack>
    <UiStack direction="row" gap="sm" align="center">
      <UiLink :to="AppRoute.SupportNetworkIssues" :variant="UiButtonVariant.Link" data-testid="network-issue-form-breadcrumb">
        {{ t('shared.networkIssues') }}
      </UiLink>
      <UiText tone="muted" size="md">/</UiText>
      <UiText tone="secondary" size="md">{{ pageTitle }}</UiText>
    </UiStack>

    <UiPageHeader
      :title="pageTitle"
      :subtitle="isEditMode ? t('support.networkIssueForm.updateTheNetworkIssueDetails') : t('support.networkIssueForm.reportANewNetworkIssue')"
    />

    <UiErrorState
      v-if="loadFailed"
      :message="store.error ?? t('errors.noAnswer')"
      testid="network-issue-form-error"
      @retry="fetchIssue"
    />

    <UiCard v-else padding="md" :aria-busy="loading ? 'true' : undefined">
      <UiStack>
        <UiField :label="t('shared.title')" :error="store.fieldErrors.title">
          <UiInput
            v-model="form.title"
            :placeholder="t('support.networkIssueForm.issueTitle')"
            data-testid="network-issue-form-title"
          />
        </UiField>

        <UiGrid :columns="2">
          <UiField :label="t('shared.type')">
            <UiSelect
              v-model="form.type"
              :options="typeOptions"
              :placeholder="t('support.networkIssueForm.selectType')"
              data-testid="network-issue-form-type"
            />
          </UiField>
          <UiField :label="t('support.networkIssueForm.server')">
            <UiInput
              v-model="form.server"
              :placeholder="t('support.networkIssueForm.serverNameOptional')"
              data-testid="network-issue-form-server"
            />
          </UiField>
        </UiGrid>

        <UiGrid :columns="2">
          <UiField :label="t('shared.priority')">
            <UiSelect
              v-model="form.priority"
              :options="priorityOptions"
              :placeholder="t('shared.selectPriority')"
              data-testid="network-issue-form-priority"
            />
          </UiField>
          <UiField v-if="isEditMode" :label="t('shared.status')">
            <UiSelect
              v-model="form.status"
              :options="statusOptions"
              :placeholder="t('support.networkIssueForm.selectStatus')"
              data-testid="network-issue-form-status"
            />
          </UiField>
        </UiGrid>

        <UiGrid :columns="2">
          <UiField :label="t('support.networkIssueForm.startDate')" :error="store.fieldErrors.startDate">
            <UiDatePicker
              v-model="form.startDate"
              :placeholder="t('support.networkIssueForm.selectStartDate')"
              data-testid="network-issue-form-start"
            />
          </UiField>
          <UiField :label="t('support.networkIssueForm.endDate')">
            <UiDatePicker
              v-model="form.endDate"
              :placeholder="t('support.networkIssueForm.selectEndDate')"
              data-testid="network-issue-form-end"
            />
          </UiField>
        </UiGrid>

        <UiField :label="t('shared.description')" :error="store.fieldErrors.description">
          <UiTextarea
            v-model="form.description"
            rows="12"
            :placeholder="t('support.networkIssueForm.describeTheIssueInDetail')"
            data-testid="network-issue-form-description"
          />
        </UiField>

        <UiStack direction="row" gap="sm">
          <UiButton
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            :disabled="!form.title.trim()"
            data-testid="network-issue-form-save"
            @click="handleSave"
          >
            {{ isEditMode ? t('support.networkIssueForm.updateIssue') : t('support.networkIssueForm.createIssue') }}
          </UiButton>
          <UiLink
            :to="AppRoute.SupportNetworkIssues"
            :variant="UiButtonVariant.Secondary"
            data-testid="network-issue-form-cancel"
          >
            {{ t('shared.cancel') }}
          </UiLink>
        </UiStack>
        <UiStatusMessage :message="saveError" :tone="UiTone.Danger" data-testid="network-issue-form-save-error" />
      </UiStack>
    </UiCard>
  </UiStack>
</template>
