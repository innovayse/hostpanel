/**
 * Endpoint functions for the admin dashboard: statistics, the chart series and the recent-activity feed.
 *
 * No state lives here: the dashboard and activity stores own it. Every function rejects
 * with an `ApiError`; turning that into words is `lib/errorHandler.ts`'s job.
 */

import { useApi } from '@/composables/useApi'
import type { ActivityEvent } from '@/types/activityevent'
import { HttpMethod } from '@/types/http'
import type { DashboardCharts, DashboardPeriod } from '@/types/dashboardcharts'
import type { DashboardStats } from '@/types/dashboardstats'
import { ApiPath } from '@/lib/apiPaths'

/**
 * Endpoint functions for `/api/admin/dashboard/*`.
 *
 * @returns One typed function per dashboard endpoint the admin panel uses.
 */
export const useDashboardApi = () => {
  const { request } = useApi()

  /**
   * Reads the headline statistics.
   *
   * @returns Revenue, service, invoice, ticket and client totals.
   */
  const fetchStats = (): Promise<DashboardStats> =>
    request<DashboardStats>(`${ApiPath.AdminDashboard}/stats`)

  /**
   * Reads the series the dashboard charts draw.
   *
   * @param period - How far back the series reach.
   * @returns Income per day, new orders and clients per day, invoices by status and open tickets by priority.
   */
  const fetchCharts = (period: DashboardPeriod): Promise<DashboardCharts> =>
    request<DashboardCharts>(`${ApiPath.AdminDashboard}/charts?period=${period}`)

  /**
   * Reads the recent activity feed.
   *
   * @param limit - Maximum number of events to return.
   * @returns Events, newest first.
   */
  const fetchActivity = (limit: number): Promise<ActivityEvent[]> =>
    request<ActivityEvent[]>(`${ApiPath.AdminDashboard}/activity?limit=${limit}`)

  /**
   * Marks one event of the feed as read for the signed-in admin.
   *
   * @param eventKey - The key of the event, as the feed returned it.
   * @returns Resolves once the server has stored the mark.
   */
  const markActivityRead = (eventKey: string): Promise<void> =>
    request<void>(`${ApiPath.AdminDashboard}/activity/read`, { method: HttpMethod.Post, body: JSON.stringify({ eventKey }) })

  /**
   * Marks every event of the feed as read for the signed-in admin.
   *
   * @returns Resolves once the server has stored the marks.
   */
  const markAllActivityRead = (): Promise<void> =>
    request<void>(`${ApiPath.AdminDashboard}/activity/read-all`, { method: HttpMethod.Post })

  return { fetchStats, fetchCharts, fetchActivity, markActivityRead, markAllActivityRead }
}
