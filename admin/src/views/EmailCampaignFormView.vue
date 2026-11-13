<script setup lang="ts">
/**
 * Email campaign form, shared by create and open. A new campaign and a draft are edited here:
 * subject, HTML body and the audience rules, with the number of clients the rules match counted
 * live, a preview of the mail, and the actions Save draft, Schedule and Send now (the last one
 * asked to confirm). A campaign that is scheduled, sending or sent is shown read-only with its
 * delivery counters; a scheduled one can have its schedule cancelled, which makes it a draft.
 *
 * Preview, Schedule and Send now work on the saved campaign, so they save the form first; a new
 * campaign is created by the first of them and the page moves to its address.
 */
import { computed, onBeforeUnmount, onMounted, provide, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import EmailCampaignPreviewDialog from '@/components/campaigns/EmailCampaignPreviewDialog.vue'
import EmailCampaignScheduleDialog from '@/components/campaigns/EmailCampaignScheduleDialog.vue'
import EmailCampaignSegmentRow from '@/components/campaigns/EmailCampaignSegmentRow.vue'
import type { CampaignOption } from '@/components/campaigns/EmailCampaignSegmentRow.vue'
import EmailCampaignStats from '@/components/campaigns/EmailCampaignStats.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import {
  areSegmentsComplete,
  CAMPAIGN_BODY_MAX_LENGTH,
  CAMPAIGN_SUBJECT_MAX_LENGTH,
  draftsFromSegments,
  newSegmentDraft,
  offeredFilterTypes,
  segmentKey,
  segmentsFromDrafts,
} from '@/lib/emailCampaigns'
import { formatDateTimeCompact } from '@/lib/format'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { AppRoute, emailCampaignPath } from '@/lib/routes'
import { campaignStatusTone } from '@/lib/tones'
import { useAuthStore } from '@/stores/auth'
import { useClientGroupsStore } from '@/stores/clientGroups'
import { useEmailCampaignsStore } from '@/stores/emailCampaigns'
import { useSettingsStore } from '@/stores/settings'
import { AuthMode } from '@/types/auth'
import { CampaignFilterType, CampaignStatus } from '@/types/emailcampaign'
import type { EmailCampaignSegment, EmailCampaignSegmentDraft } from '@/types/emailcampaign'
import { UiButtonVariant } from '@/utils/ui/button'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const route = useRoute()
const router = useRouter()
const store = useEmailCampaignsStore()
const settings = useSettingsStore()
const clientGroups = useClientGroupsStore()
const auth = useAuthStore()

/** The campaign ID of the address, null for a new campaign. */
const campaignId = computed<number | null>(() => (route.params.id ? Number(route.params.id) : null))

/** Whether the campaign has been saved before (an `:id` in the address). */
const isSaved = computed<boolean>(() => campaignId.value !== null)

/** Whether the loaded campaign is past the draft stage, so the form only shows it. */
const readOnly = computed<boolean>(() => store.current !== null && store.current.status !== CampaignStatus.Draft)

/** Page title reflecting the mode. */
const pageTitle = computed<string>(() => {
  if (readOnly.value) {
    return t('emailCampaigns.form.titleView')
  }
  return isSaved.value ? t('emailCampaigns.form.titleEdit') : t('emailCampaigns.form.titleCreate')
})

/** Subject and body as typed. */
const form = ref({ subject: '', body: '' })

/** Next key to hand to an audience row; the rows are keyed by it, never by position. */
let lastSegmentKey = 0

/**
 * Hands out the key of a new audience row.
 *
 * @returns A key no other row of this form has had.
 */
const takeSegmentKey = (): number => {
  lastSegmentKey += 1
  return lastSegmentKey
}

/** The audience rules as the form edits them; {@link load} fills them (a blank rule for a new campaign). */
const segments = ref<EmailCampaignSegmentDraft[]>([])

/** Whether the campaign is being loaded; true from the first paint of an existing campaign, so the form never flashes empty. */
const loading = ref(campaignId.value !== null)

// Every field of the form stays a placeholder while the campaign is read.
provide(UI_FIELD_LOADING, loading)

/** The sentence of a refused load, null when it loaded. */
const loadError = ref<string | null>(null)

/** Whether a save or a campaign action is running. */
const acting = ref(false)

/** The sentence of the last refused save or action, null when none. */
const actionError = ref<string | null>(null)

/** The outcome line of the last successful save or action. */
const feedback = useResultBanner()

/** Whether the Send now confirmation is open. */
const sendNowOpen = ref(false)

/** Whether the schedule dialog is open. */
const scheduleOpen = ref(false)

/** Whether the cancel-schedule confirmation is open. */
const cancelOpen = ref(false)

/** Whether the preview dialog is open. */
const previewOpen = ref(false)

/** The products the product rule offers, from the settings store. */
const productOptions = computed<CampaignOption[]>(() =>
  settings.products.map((product) => ({ value: String(product.id), label: product.name })),
)

/** The client groups the group rule offers, from the client groups store. */
const groupOptions = computed<CampaignOption[]>(() =>
  clientGroups.groups.map((group) => ({ value: String(group.id), label: group.name })),
)

/** The kinds of rule on offer: the sign-in rule only where the deployment owns its accounts and so knows the last sign-in. */
const filterTypes = computed(() => offeredFilterTypes(auth.mode === AuthMode.Local))

/** Whether every audience rule is filled in and there is at least one. */
const rulesComplete = computed<boolean>(() => areSegmentsComplete(segments.value))

/** Whether the form holds everything a save needs. */
const canSave = computed<boolean>(
  () => form.value.subject.trim() !== '' && form.value.body.trim() !== '' && rulesComplete.value,
)

/** The matching-clients count as a sentence for the Send now confirmation. */
const sendNowMessage = computed<string>(() =>
  store.audienceCount === null
    ? t('emailCampaigns.form.sendNowMessage')
    : t('emailCampaigns.form.sendNowMessageCount', { count: store.audienceCount }),
)

/**
 * Describes one rule of a read-only campaign in a sentence, naming a product or a client group instead of its id.
 *
 * @param segment - The rule.
 * @returns For example "Clients with a service of Basic".
 */
const describeSegment = (segment: EmailCampaignSegment): string => {
  const raw = segment.filterValue ?? ''
  const named = segment.filterType === CampaignFilterType.ClientGroup ? groupOptions.value : productOptions.value
  const value = segment.filterType === CampaignFilterType.Product || segment.filterType === CampaignFilterType.ClientGroup
    ? (named.find((option) => option.value === raw)?.label ?? raw)
    : raw
  return t(`emailCampaigns.segmentSummary.${segment.filterType}`, { value })
}

/** Fills the form with a blank campaign. */
const resetForm = (): void => {
  form.value = { subject: '', body: '' }
  segments.value = [newSegmentDraft(takeSegmentKey())]
}

/**
 * Loads the campaign of the address into the form, or blanks the form for a new one.
 *
 * @returns Resolves when the load has settled.
 */
const load = async (): Promise<void> => {
  if (campaignId.value === null) {
    store.clearCurrent()
    loadError.value = null
    resetForm()
    return
  }
  loading.value = true
  loadError.value = (await store.fetchById(campaignId.value)) ? null : store.error
  loading.value = false
  const campaign = store.current
  if (campaign) {
    form.value = { subject: campaign.subject, body: campaign.body }
    segments.value = draftsFromSegments(campaign.segments, lastSegmentKey + 1)
    lastSegmentKey += campaign.segments.length
  }
}

/**
 * Replaces one audience row with the edited rule.
 *
 * @param index - Position of the row.
 * @param segment - The rule as edited.
 */
const replaceSegment = (index: number, segment: EmailCampaignSegmentDraft): void => {
  segments.value = segments.value.map((current, position) => (position === index ? segment : current))
}

/** Adds a blank audience row. */
const addSegment = (): void => {
  segments.value = [...segments.value, newSegmentDraft(takeSegmentKey())]
}

/**
 * Removes one audience row.
 *
 * @param index - Position of the row.
 */
const removeSegment = (index: number): void => {
  segments.value = segments.value.filter((_, position) => position !== index)
}

/** Handle of the pending audience count, null when none is waiting. */
let countTimer: ReturnType<typeof setTimeout> | null = null

/** Cancels the pending audience count. */
const cancelCount = (): void => {
  if (countTimer !== null) {
    clearTimeout(countTimer)
    countTimer = null
  }
}

/**
 * Counts the matching clients once the rules stop changing. The old count is dropped at once, since
 * it belongs to the old rules. Nothing is counted for a campaign that is only shown or still
 * loading, or while a rule is unfinished.
 */
const scheduleCount = (): void => {
  cancelCount()
  store.clearAudience()
  if (loading.value || readOnly.value || !rulesComplete.value) {
    return
  }
  countTimer = setTimeout(() => {
    countTimer = null
    void store.fetchAudienceCount(segmentsFromDrafts(segments.value))
  }, SEARCH_DEBOUNCE_MS)
}

/**
 * Saves the form: creates the campaign when it is new, replaces the draft otherwise. A campaign that
 * is only shown is not saved, its id is returned as it is.
 *
 * @returns The campaign id, or null when the API refused (see `store.fieldErrors`).
 */
const persist = async (): Promise<number | null> => {
  if (readOnly.value && campaignId.value !== null) {
    return campaignId.value
  }
  const payload = {
    subject: form.value.subject.trim(),
    body: form.value.body,
    segments: segmentsFromDrafts(segments.value),
  }
  if (campaignId.value !== null) {
    return (await store.update(campaignId.value, payload)) ? campaignId.value : null
  }
  return store.create(payload)
}

/**
 * Saves the form, runs one step on the saved campaign and reports how it went. A new campaign is
 * created first, and the page then moves to its address (which loads it), so a step that fails
 * after the creation leaves the draft saved instead of lost.
 *
 * @param step - What to do with the saved campaign; returns whether it worked.
 * @param successText - The sentence for success, null for none.
 * @param reload - Whether to load the campaign again afterwards (its status changed).
 * @returns Resolves when the step and the follow-up have settled.
 */
const perform = async (step: (id: number) => Promise<boolean>, successText: string | null, reload: boolean): Promise<void> => {
  actionError.value = null
  feedback.clear()
  acting.value = true
  const wasNew = campaignId.value === null
  const id = await persist()
  if (id === null) {
    actionError.value = store.error
    acting.value = false
    return
  }
  const worked = await step(id)
  if (!worked) {
    actionError.value = store.error
  } else if (successText !== null) {
    feedback.show(successText, false)
  }
  acting.value = false
  if (wasNew) {
    await router.replace(emailCampaignPath(id))
  } else if (reload) {
    await load()
  }
}

/**
 * Saves the form as a draft.
 *
 * @returns Resolves when the save has settled.
 */
const saveDraft = (): Promise<void> => perform(() => Promise.resolve(true), t('emailCampaigns.form.saved'), false)

/**
 * Saves the form and opens the preview of the saved mail.
 *
 * @returns Resolves when the save and the preview have settled.
 */
const openPreview = (): Promise<void> =>
  perform(async (id) => {
    const rendered = await store.fetchPreview(id)
    previewOpen.value = rendered
    return rendered
  }, null, false)

/** Closes the preview. */
const closePreview = (): void => {
  previewOpen.value = false
  store.clearPreview()
}

/**
 * Saves the form and starts sending at once, after the operator confirmed.
 *
 * @returns Resolves when the send has been accepted or refused.
 */
const sendNow = async (): Promise<void> => {
  sendNowOpen.value = false
  await perform((id) => store.schedule(id, null), t('emailCampaigns.form.sendStarted'), true)
}

/**
 * Saves the form and schedules the send for the chosen moment.
 *
 * @param at - The send time.
 * @returns Resolves when the schedule has been accepted or refused.
 */
const scheduleAt = async (at: Date): Promise<void> => {
  scheduleOpen.value = false
  await perform((id) => store.schedule(id, at), t('emailCampaigns.form.scheduled'), true)
}

/**
 * Cancels the schedule after the operator confirmed; the campaign is a draft again.
 *
 * @returns Resolves when the cancel has been accepted or refused.
 */
const cancelSchedule = async (): Promise<void> => {
  cancelOpen.value = false
  if (campaignId.value === null) {
    return
  }
  actionError.value = null
  feedback.clear()
  acting.value = true
  const cancelled = await store.cancel(campaignId.value)
  if (cancelled) {
    feedback.show(t('emailCampaigns.form.scheduleCancelled'), false)
  } else {
    actionError.value = store.error
  }
  acting.value = false
  await load()
}

/** Reloads the campaign when the address changes (opening another one, or after the first save). */
watch(campaignId, () => {
  void load()
}, { immediate: true })

/** Recounts the audience whenever the rules, the stage of the campaign or the loading state change. */
watch([segments, readOnly, loading], scheduleCount, { deep: true, immediate: true })

/** Loads the products and the client groups the rules offer. */
onMounted(() => {
  void settings.fetchProducts()
  void clientGroups.fetchAll()
})

onBeforeUnmount(cancelCount)
</script>

<template>
  <UiStack>
    <UiStack direction="row" gap="sm" align="center">
      <UiLink :to="AppRoute.SupportEmailCampaigns" :variant="UiButtonVariant.Link" data-testid="email-campaign-breadcrumb">
        {{ t('emailCampaigns.list.title') }}
      </UiLink>
      <UiText tone="muted" size="md">/</UiText>
      <UiText tone="secondary" size="md">{{ pageTitle }}</UiText>
    </UiStack>

    <UiPageHeader :title="pageTitle" :subtitle="readOnly ? t('emailCampaigns.form.subtitleView') : t('emailCampaigns.form.subtitleEdit')" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="loading"
      testid="email-campaign-load-error"
      @retry="load"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="UiTone.Success" data-testid="email-campaign-feedback" />
    <UiAlert v-if="actionError" variant="error" data-testid="email-campaign-error">{{ actionError }}</UiAlert>

    <template v-if="loadError === null">
      <UiCard v-if="store.current && readOnly" padding="md">
        <UiStack>
          <UiStack direction="row" gap="sm" align="center" wrap>
            <UiBadge :tone="campaignStatusTone[store.current.status]" data-testid="email-campaign-status">
              {{ t(`emailCampaigns.status.${store.current.status}`) }}
            </UiBadge>
            <UiText v-if="store.current.scheduledAt" tone="muted" size="sm" data-testid="email-campaign-scheduled-at">
              {{ t('emailCampaigns.form.scheduledFor', { at: formatDateTimeCompact(store.current.scheduledAt) }) }}
            </UiText>
            <UiText v-if="store.current.sentAt" tone="muted" size="sm" data-testid="email-campaign-sent-at">
              {{ t('emailCampaigns.form.sentOn', { at: formatDateTimeCompact(store.current.sentAt) }) }}
            </UiText>
            <UiText tone="muted" size="sm" data-testid="email-campaign-created">
              {{ t('emailCampaigns.form.createdBy', { by: store.current.createdBy, at: formatDateTimeCompact(store.current.createdAt) }) }}
            </UiText>
          </UiStack>
          <EmailCampaignStats :stats="store.current.stats" />
        </UiStack>
      </UiCard>

      <UiCard padding="md" :aria-busy="loading ? 'true' : undefined" data-testid="email-campaign-form">
        <UiStack>
          <UiField :label="t('shared.subject')" :error="store.fieldErrors.subject">
            <template #default="{ id, invalid, describedby }">
              <UiInput
                :id="id"
                v-model="form.subject"
                :invalid="invalid"
                :aria-describedby="describedby"
                :readonly="readOnly"
                :maxlength="CAMPAIGN_SUBJECT_MAX_LENGTH"
                :placeholder="t('emailCampaigns.form.subjectPlaceholder')"
                data-testid="email-campaign-subject"
              />
            </template>
          </UiField>

          <UiField :label="t('emailCampaigns.form.body')" :hint="t('emailCampaigns.form.bodyHint')" :error="store.fieldErrors.body">
            <template #default="{ id, invalid, describedby }">
              <UiTextarea
                :id="id"
                v-model="form.body"
                rows="16"
                :invalid="invalid"
                :aria-describedby="describedby"
                :readonly="readOnly"
                :maxlength="CAMPAIGN_BODY_MAX_LENGTH"
                :placeholder="t('emailCampaigns.form.bodyPlaceholder')"
                class="font-mono"
                data-testid="email-campaign-body"
              />
            </template>
          </UiField>

          <UiField :label="t('emailCampaigns.form.audience')" :hint="readOnly ? undefined : t('emailCampaigns.form.audienceHint')" :error="store.fieldErrors.segments">
            <UiStack v-if="readOnly && store.current">
              <UiText
                v-for="(segment, index) in store.current.segments"
                :key="segmentKey(segment)"
                tone="secondary"
                size="md"
                :data-testid="`email-campaign-segment-summary-${index}`"
              >
                {{ describeSegment(segment) }}
              </UiText>
            </UiStack>
            <UiStack v-else>
              <EmailCampaignSegmentRow
                v-for="(segment, index) in segments"
                :key="segment.key"
                :model-value="segment"
                :products="productOptions"
                :groups="groupOptions"
                :filter-types="filterTypes"
                @update:model-value="replaceSegment(index, $event)"
                @remove="removeSegment(index)"
              />
              <div>
                <UiButton type="button" :variant="UiButtonVariant.Secondary" data-testid="email-campaign-add-segment" @click="addSegment">
                  {{ t('emailCampaigns.form.addSegment') }}
                </UiButton>
              </div>
              <UiText v-if="!rulesComplete" tone="muted" size="sm" data-testid="email-campaign-audience">
                {{ t('emailCampaigns.form.audienceIncomplete') }}
              </UiText>
              <UiText v-else-if="store.audienceError" tone="danger" size="sm" data-testid="email-campaign-audience">
                {{ store.audienceError }}
              </UiText>
              <UiSkeleton v-else-if="store.audienceCount === null" :lines="1" class="w-40" aria-busy="true" data-testid="email-campaign-audience" />
              <UiText v-else tone="secondary" size="sm" data-testid="email-campaign-audience">
                {{ t('emailCampaigns.form.audienceCount', { count: store.audienceCount }) }}
              </UiText>
            </UiStack>
          </UiField>

          <UiStack direction="row" gap="sm" wrap>
            <template v-if="!readOnly">
              <UiButton
                :variant="UiButtonVariant.Primary"
                :loading="acting"
                :disabled="!canSave"
                data-testid="email-campaign-save"
                @click="saveDraft"
              >
                {{ t('emailCampaigns.form.saveDraft') }}
              </UiButton>
              <UiButton
                :variant="UiButtonVariant.Secondary"
                :disabled="!canSave || acting"
                data-testid="email-campaign-preview"
                @click="openPreview"
              >
                {{ t('emailCampaigns.form.preview') }}
              </UiButton>
              <UiButton
                :variant="UiButtonVariant.Secondary"
                :disabled="!canSave || acting"
                data-testid="email-campaign-schedule"
                @click="scheduleOpen = true"
              >
                {{ t('emailCampaigns.form.schedule') }}
              </UiButton>
              <UiButton
                :variant="UiButtonVariant.Secondary"
                :disabled="!canSave || acting"
                data-testid="email-campaign-send-now"
                @click="sendNowOpen = true"
              >
                {{ t('emailCampaigns.form.sendNow') }}
              </UiButton>
            </template>
            <template v-else>
              <UiButton
                :variant="UiButtonVariant.Secondary"
                :disabled="acting"
                data-testid="email-campaign-preview"
                @click="openPreview"
              >
                {{ t('emailCampaigns.form.preview') }}
              </UiButton>
              <UiButton
                v-if="store.current?.status === CampaignStatus.Scheduled"
                :variant="UiButtonVariant.Danger"
                :disabled="acting"
                data-testid="email-campaign-cancel-schedule"
                @click="cancelOpen = true"
              >
                {{ t('emailCampaigns.form.cancelSchedule') }}
              </UiButton>
            </template>
            <UiLink :to="AppRoute.SupportEmailCampaigns" :variant="UiButtonVariant.Secondary" data-testid="email-campaign-back">
              {{ readOnly ? t('shared.back') : t('shared.cancel') }}
            </UiLink>
          </UiStack>
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      variant="confirm"
      :open="sendNowOpen"
      :title="t('emailCampaigns.form.sendNowTitle')"
      :message="sendNowMessage"
      :confirm-label="t('emailCampaigns.form.sendNow')"
      @confirm="sendNow"
      @close="sendNowOpen = false"
    />

    <UiDialog
      variant="confirm"
      :open="cancelOpen"
      :title="t('emailCampaigns.form.cancelScheduleTitle')"
      :message="t('emailCampaigns.form.cancelScheduleMessage')"
      :confirm-label="t('emailCampaigns.form.cancelSchedule')"
      tone="danger"
      @confirm="cancelSchedule"
      @close="cancelOpen = false"
    />

    <EmailCampaignScheduleDialog
      v-if="scheduleOpen"
      :loading="acting"
      @confirm="scheduleAt"
      @close="scheduleOpen = false"
    />

    <EmailCampaignPreviewDialog v-if="previewOpen && store.preview" :preview="store.preview" @close="closePreview" />
  </UiStack>
</template>
