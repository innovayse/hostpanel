/** Stats shown on the admin dashboard. */
export interface DashboardStats {
  /** Total lifetime revenue. */
  totalRevenue: number
  /** Revenue for the current month. */
  monthlyRevenue: number
  /** Number of active services. */
  activeServices: number
  /** Number of overdue invoices. */
  overdueInvoices: number
  /** Number of open support tickets. */
  openTickets: number
  /** Total registered clients. */
  totalClients: number
}
