/**
 * Endpoint functions for the admin dashboard: statistics, the chart series and the recent-activity feed.
 *
 * No state lives here: the dashboard and activity stores own it. Every function rejects
 * with an `ApiError`; turning that into words is `lib/errorHandler.ts`'s job.
 */

import { useApi } from '@/composables/useApi'
import type { ActivityEvent } from '@/types/activityevent'
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

  return { fetchStats, fetchCharts, fetchActivity }
}
