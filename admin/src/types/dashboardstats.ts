/** Stats shown on the admin dashboard. */
export interface DashboardStats {
  /** Number of active services. */
  activeServices: number
  /** Number of overdue invoices. */
  overdueInvoices: number
  /** Number of open support tickets. */
  openTickets: number
  /** Total registered clients. */
  totalClients: number
}

/** The fields of `DashboardStats` that hold a number a tile shows. */
export type DashboardFigure = keyof DashboardStats
