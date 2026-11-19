<script setup lang="ts">
/**
 * Network issue form shared by create and edit. The mode comes from the route: an `:id` param
 * means edit and loads the issue first. A refused load offers a retry instead of an empty form; a
 * refused save shows the API's sentence next to the save button, a saved issue returns to the list.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiTone } from '@/utils/ui/tone'
import UiButton from '@/components/ui/UiButton.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { NETWORK_ISSUE_PRIORITY_OPTIONS, NETWORK_ISSUE_STATUS_OPTIONS, NETWORK_ISSUE_TYPE_OPTIONS } from '@/lib/constants'
import { toDateInputValue, toIsoDay } from '@/lib/format'
import { useNetworkIssuesStore } from '@/stores/networkIssues'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'
import { UiButtonVariant } from '@/utils/ui/button'
import { AppRoute } from '@/lib/routes'
import { withoutFields } from '@/lib/formSchema'
import schema, { NETWORK_ISSUE_STATUS_FIELD } from '@/views/NetworkIssueFormView.schema'
import { UiActionKind } from '@/utils/ui/actions'

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

/** Whether the existing issue is being loaded; the form draws its fields as placeholders meanwhile. */
const loading = ref(false)

/** The columns of the form: the schema's, with the status only when an existing issue is edited. */
const columns = computed(() => (isEditMode.value ? schema.columns : withoutFields(schema.columns, [NETWORK_ISSUE_STATUS_FIELD])))

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

    <UiSchemaForm v-else :columns="columns" :loading="loading" :errors="store.fieldErrors">
      <template #field-title>
        <UiInput
          v-model="form.title"
          :placeholder="t('support.networkIssueForm.issueTitle')"
          data-testid="network-issue-form-title"
        />
      </template>

      <template #field-type>
        <UiSelect
          v-model="form.type"
          :options="NETWORK_ISSUE_TYPE_OPTIONS"
          :placeholder="t('support.networkIssueForm.selectType')"
          data-testid="network-issue-form-type"
        />
      </template>

      <template #field-server>
        <UiInput
          v-model="form.server"
          :placeholder="t('support.networkIssueForm.serverNameOptional')"
          data-testid="network-issue-form-server"
        />
      </template>

      <template #field-priority>
        <UiSelect
          v-model="form.priority"
          :options="NETWORK_ISSUE_PRIORITY_OPTIONS"
          :placeholder="t('shared.selectPriority')"
          data-testid="network-issue-form-priority"
        />
      </template>

      <template #field-status>
        <UiSelect
          v-model="form.status"
          :options="NETWORK_ISSUE_STATUS_OPTIONS"
          :placeholder="t('support.networkIssueForm.selectStatus')"
          data-testid="network-issue-form-status"
        />
      </template>

      <template #field-startDate>
        <UiDatePicker
          v-model="form.startDate"
          :placeholder="t('support.networkIssueForm.selectStartDate')"
          data-testid="network-issue-form-start"
        />
      </template>

      <template #field-endDate>
        <UiDatePicker
          v-model="form.endDate"
          :placeholder="t('support.networkIssueForm.selectEndDate')"
          data-testid="network-issue-form-end"
        />
      </template>

      <template #field-description>
        <UiTextarea
          v-model="form.description"
          rows="12"
          :placeholder="t('support.networkIssueForm.describeTheIssueInDetail')"
          data-testid="network-issue-form-description"
        />
      </template>

      <UiStack direction="row" gap="sm">
        <UiButton :action="UiActionKind.Add"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          :disabled="!form.title.trim()"
          data-testid="network-issue-form-save"
          @click="handleSave"
        >
          {{ isEditMode ? t('support.networkIssueForm.updateIssue') : t('support.networkIssueForm.createIssue') }}
        </UiButton>
        <UiLink :action="UiActionKind.Cancel"
          :to="AppRoute.SupportNetworkIssues"
          :variant="UiButtonVariant.Secondary"
          data-testid="network-issue-form-cancel"
        >
          {{ t('shared.cancel') }}
        </UiLink>
      </UiStack>
      <UiStatusMessage :message="saveError" :tone="UiTone.Danger" data-testid="network-issue-form-save-error" />
    </UiSchemaForm>
  </UiStack>
</template>
