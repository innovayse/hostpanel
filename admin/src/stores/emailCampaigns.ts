/**
 * Pinia store for email campaigns in the admin support module.
 *
 * Owns every request the campaign pages make and the sentence for a refusal, so the pages hold
 * no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useEmailCampaignsApi } from '@/composables/apis/useEmailCampaignsApi'
import { runAction } from '@/lib/runAction'
import type {
  EmailCampaign,
  EmailCampaignListItem,
  EmailCampaignPayload,
  EmailCampaignPreview,
  EmailCampaignSegment,
} from '@/types/emailcampaign'

/** The campaign list, the campaign being edited or watched, its preview and its audience size. */
export const useEmailCampaignsStore = defineStore('emailCampaigns', () => {
  const api = useEmailCampaignsApi()

  /** Every campaign with its delivery counters. */
  const campaigns = ref<EmailCampaignListItem[]>([])

  /** The campaign loaded into the form, null while none is. */
  const current = ref<EmailCampaign | null>(null)

  /** The rendered mail of the last preview, null while none was asked for. */
  const preview = ref<EmailCampaignPreview | null>(null)

  /** Clients the audience rules of the form match, null while not counted. */
  const audienceCount = ref<number | null>(null)

  /** True while a load or save request is in flight. */
  const loading = ref(false)

  /** True while the audience is being counted. */
  const counting = ref(false)

  /** Sentence for the last failed call, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed audience count, null when none. */
  const audienceError = ref<string | null>(null)

  /** Per-field messages of the last refused save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Number of the newest audience count; an older answer arriving later is dropped. */
  let latestCount = 0

  /**
   * Loads the campaign list.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, api.listCampaigns)
    loading.value = false
    if (result.ok) {
      campaigns.value = result.value
    }
    return result.ok
  }

  /**
   * Loads one campaign into `current`.
   *
   * @param id - The campaign identifier.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchById = async (id: number | string): Promise<boolean> => {
    loading.value = true
    current.value = null
    const result = await runAction(error, () => api.getCampaign(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
    return result.ok
  }

  /** Forgets the loaded campaign, so a new campaign's form does not inherit the stage of the last one opened. */
  const clearCurrent = (): void => {
    current.value = null
  }

  /**
   * Creates a draft campaign.
   *
   * @param payload - Campaign data.
   * @returns The new campaign's id, or null when refused (see `error` and `fieldErrors`).
   */
  const create = async (payload: EmailCampaignPayload): Promise<number | null> => {
    loading.value = true
    const result = await runAction(error, () => api.createCampaign(payload), fieldErrors)
    loading.value = false
    return result.ok ? result.value : null
  }

  /**
   * Replaces a draft campaign.
   *
   * @param id - The campaign identifier.
   * @param payload - Updated data.
   * @returns True when saved; false when refused (see `error` and `fieldErrors`).
   */
  const update = async (id: number | string, payload: EmailCampaignPayload): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.updateCampaign(id, payload), fieldErrors)
    loading.value = false
    return result.ok
  }

  /**
   * Deletes a draft campaign.
   *
   * @param id - The campaign identifier.
   * @returns True when deleted; false when refused (see `error`).
   */
  const remove = async (id: number | string): Promise<boolean> =>
    (await runAction(error, () => api.deleteCampaign(id))).ok

  /**
   * Schedules a campaign, or starts sending it at once.
   *
   * @param id - The campaign identifier.
   * @param at - Send time; null sends now.
   * @returns True when accepted; false when refused (see `error`).
   */
  const schedule = async (id: number | string, at: Date | null): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.scheduleCampaign(id, at === null ? null : at.toISOString()))
    loading.value = false
    return result.ok
  }

  /**
   * Cancels the schedule of a campaign, which becomes a draft again.
   *
   * @param id - The campaign identifier.
   * @returns True when cancelled; false when refused (see `error`).
   */
  const cancel = async (id: number | string): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.cancelCampaign(id))
    loading.value = false
    return result.ok
  }

  /**
   * Renders the mail of a saved campaign into `preview`.
   *
   * @param id - The campaign identifier.
   * @returns True when rendered; false when refused (see `error`).
   */
  const fetchPreview = async (id: number | string): Promise<boolean> => {
    loading.value = true
    preview.value = null
    const result = await runAction(error, () => api.previewCampaign(id))
    loading.value = false
    if (result.ok) {
      preview.value = result.value
    }
    return result.ok
  }

  /** Forgets the last preview, so a closed dialog does not show it again. */
  const clearPreview = (): void => {
    preview.value = null
  }

  /**
   * Counts the clients the audience rules match into `audienceCount`. Only the newest call
   * writes its answer, so a slow reply to an earlier edit cannot overwrite a later one.
   *
   * @param segments - The audience rules.
   * @returns True when counted; false when refused (see `audienceError`) or superseded.
   */
  const fetchAudienceCount = async (segments: EmailCampaignSegment[]): Promise<boolean> => {
    latestCount += 1
    const ticket = latestCount
    counting.value = true
    const result = await runAction(audienceError, () => api.countAudience(segments))
    if (ticket !== latestCount) {
      return false
    }
    counting.value = false
    audienceCount.value = result.ok ? result.value.count : null
    return result.ok
  }

  /** Forgets the audience size, e.g. when the rules are incomplete and cannot be counted. */
  const clearAudience = (): void => {
    latestCount += 1
    counting.value = false
    audienceCount.value = null
    audienceError.value = null
  }

  return {
    campaigns, current, preview, audienceCount, loading, counting, error, audienceError, fieldErrors,
    fetchAll, fetchById, clearCurrent, create, update, remove, schedule, cancel, fetchPreview, clearPreview,
    fetchAudienceCount, clearAudience,
  }
})
