/**
 * Endpoints behind the email campaigns store.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type {
  EmailCampaign,
  EmailCampaignAudience,
  EmailCampaignListItem,
  EmailCampaignPayload,
  EmailCampaignPreview,
  EmailCampaignSegment,
} from '@/types/emailcampaign'

/**
 * Endpoint functions for `/admin/email-campaigns`.
 *
 * @returns One typed function per endpoint.
 */
export const useEmailCampaignsApi = () => {
  const { request } = useApi()

  /**
   * Lists every campaign with its delivery counters.
   *
   * @returns The campaigns.
   */
  const listCampaigns = (): Promise<EmailCampaignListItem[]> =>
    request<EmailCampaignListItem[]>(ApiPath.AdminEmailCampaigns)

  /**
   * Reads one campaign with its body and audience.
   *
   * @param id - Campaign id.
   * @returns The campaign.
   */
  const getCampaign = (id: number | string): Promise<EmailCampaign> =>
    request<EmailCampaign>(`${ApiPath.AdminEmailCampaigns}/${id}`)

  /**
   * Creates a draft campaign.
   *
   * @param payload - Subject, body and audience rules.
   * @returns The new campaign's id.
   */
  const createCampaign = (payload: EmailCampaignPayload): Promise<number> =>
    request<number>(ApiPath.AdminEmailCampaigns, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a draft campaign.
   *
   * @param id - Campaign id.
   * @param payload - Subject, body and audience rules.
   * @returns Nothing; the API answers 204.
   */
  const updateCampaign = (id: number | string, payload: EmailCampaignPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminEmailCampaigns}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a draft campaign.
   *
   * @param id - Campaign id.
   * @returns Nothing; the API answers 204.
   */
  const deleteCampaign = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.AdminEmailCampaigns}/${id}`, { method: HttpMethod.Delete })

  /**
   * Schedules a campaign, or starts sending it at once.
   *
   * @param id - Campaign id.
   * @param at - ISO 8601 send time; null sends now.
   * @returns Nothing; the API answers 204.
   */
  const scheduleCampaign = (id: number | string, at: string | null): Promise<void> =>
    request<void>(`${ApiPath.AdminEmailCampaigns}/${id}/schedule`, { method: HttpMethod.Post, body: JSON.stringify({ at }) })

  /**
   * Cancels the schedule of a campaign, which becomes a draft again.
   *
   * @param id - Campaign id.
   * @returns Nothing; the API answers 204.
   */
  const cancelCampaign = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.AdminEmailCampaigns}/${id}/cancel`, { method: HttpMethod.Post })

  /**
   * Renders the mail of a saved campaign with a sample unsubscribe link.
   *
   * @param id - Campaign id.
   * @returns The rendered subject and HTML.
   */
  const previewCampaign = (id: number | string): Promise<EmailCampaignPreview> =>
    request<EmailCampaignPreview>(`${ApiPath.AdminEmailCampaigns}/${id}/preview`)

  /**
   * Counts the clients the audience rules match.
   *
   * @param segments - The audience rules.
   * @returns The count.
   */
  const countAudience = (segments: EmailCampaignSegment[]): Promise<EmailCampaignAudience> =>
    request<EmailCampaignAudience>(`${ApiPath.AdminEmailCampaigns}/audience-count`, {
      method: HttpMethod.Post,
      body: JSON.stringify({ segments }),
    })

  return {
    listCampaigns,
    getCampaign,
    createCampaign,
    updateCampaign,
    deleteCampaign,
    scheduleCampaign,
    cancelCampaign,
    previewCampaign,
    countAudience,
  }
}
