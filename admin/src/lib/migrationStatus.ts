/**
 * Presentation helpers shared by the migration screens: which caption a job state or an
 * import-log outcome, skip reason or job failure gets, and whether a job can still change. Kept in one place so the history
 * list, the progress card and the report table cannot disagree. Badge tones live in `tones.ts`.
 */
import { MigrationJobFailure, MigrationLogAction, MigrationSkipReason, MigrationStatus } from '@/types/migration'
import type { MigrationJob, MigrationLogEntry } from '@/types/migration'

/** Locale key of the caption per job state. */
export const migrationStatusLabelKey: Record<MigrationJob['status'], string> = {
  [MigrationStatus.Pending]: 'migration.status.pending',
  [MigrationStatus.InProgress]: 'migration.status.inProgress',
  [MigrationStatus.Completed]: 'migration.status.completed',
  [MigrationStatus.Failed]: 'migration.status.failed',
  [MigrationStatus.Queued]: 'migration.status.queued',
}

/** Locale key of the caption per import-log outcome. */
export const migrationActionLabelKey: Record<MigrationLogEntry['action'], string> = {
  [MigrationLogAction.Imported]: 'migration.migrationReportTable.imported',
  [MigrationLogAction.Skipped]: 'migration.migrationProgressCard.skipped',
  [MigrationLogAction.Failed]: 'migration.migrationProgressCard.failed',
}

/** Locale key of the caption per reason a log row was skipped. */
export const migrationSkipReasonLabelKey: Record<MigrationSkipReason, string> = {
  [MigrationSkipReason.AlreadyExists]: 'migration.skipReason.alreadyExists',
  [MigrationSkipReason.ClientNotFound]: 'migration.skipReason.clientNotFound',
  [MigrationSkipReason.AccountNotProvisioned]: 'migration.skipReason.accountNotProvisioned',
  [MigrationSkipReason.ProductGroupNotFound]: 'migration.skipReason.productGroupNotFound',
  [MigrationSkipReason.NoProducts]: 'migration.skipReason.noProducts',
  [MigrationSkipReason.TicketNotFound]: 'migration.skipReason.ticketNotFound',
  [MigrationSkipReason.TicketClosed]: 'migration.skipReason.ticketClosed',
  [MigrationSkipReason.ImportFailed]: 'migration.skipReason.importFailed',
  [MigrationSkipReason.ProductNotFound]: 'migration.skipReason.productNotFound',
  [MigrationSkipReason.InvoiceNotFound]: 'migration.skipReason.invoiceNotFound',
  [MigrationSkipReason.DateMissing]: 'migration.skipReason.dateMissing',
  [MigrationSkipReason.DepartmentNotFound]: 'migration.skipReason.departmentNotFound',
  [MigrationSkipReason.DownloadCategoryNotFound]: 'migration.skipReason.downloadCategoryNotFound',
  [MigrationSkipReason.NameMissing]: 'migration.skipReason.nameMissing',
  [MigrationSkipReason.BillingCycleUnsupported]: 'migration.skipReason.billingCycleUnsupported',
  [MigrationSkipReason.NoProductLines]: 'migration.skipReason.noProductLines',
  [MigrationSkipReason.CurrencyInvalid]: 'migration.skipReason.currencyInvalid',
}

/** Locale key of the caption per reason a job failed. */
export const migrationJobFailureLabelKey: Record<MigrationJobFailure, string> = {
  [MigrationJobFailure.Unknown]: 'migration.jobFailure.unknown',
  [MigrationJobFailure.SourceUnreachable]: 'migration.jobFailure.sourceUnreachable',
  [MigrationJobFailure.SourceRefused]: 'migration.jobFailure.sourceRefused',
  [MigrationJobFailure.ImportFailed]: 'migration.jobFailure.importFailed',
  [MigrationJobFailure.Cancelled]: 'migration.jobFailure.cancelled',
  [MigrationJobFailure.Interrupted]: 'migration.jobFailure.interrupted',
  [MigrationJobFailure.SourceAddressNotAllowed]: 'migration.jobFailure.sourceAddressNotAllowed',
}

/**
 * Tells whether a job can still change on the server (so the screen polls it).
 *
 * @param status - The job's state.
 * @returns True for Pending, Queued and InProgress.
 */
export const isJobActive = (status: MigrationJob['status']): boolean =>
  status === MigrationStatus.Pending || status === MigrationStatus.Queued || status === MigrationStatus.InProgress
