/**
 * The ticket filters every ticket list shares: status, priority and department.
 *
 * Both ticket lists (the staff list and the tab of a client's profile) keep these three filters in the address through
 * `useListQuery`. This reads them back as the typed values the API takes, and describes them as chips, so the two
 * screens do not each carry the same conversion. A screen adds its own filters (the staff list adds the flag) next to them.
 */
import { computed } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { TICKET_PRIORITY_OPTIONS, TICKET_STATUS_OPTIONS } from '@/lib/constants'
import { statusLabel } from '@/lib/tones'
import type { Department } from '@/types/department'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import type { ClientTicketListFilters } from '@/types/ticket'

/** The ticket filter fields every ticket list has; each is one query key and one `ClientTicketListFilters` member. */
export const TicketFilterKey = {
  Status: 'status',
  Priority: 'priority',
  DepartmentId: 'departmentId',
} as const

/** One of the {@link TicketFilterKey} values. */
export type TicketFilterKey = (typeof TicketFilterKey)[keyof typeof TicketFilterKey]

/** What {@link useTicketFilters} hands the ticket list screen. */
export interface TicketFilters {
  /** The applied status, priority and department as the API takes them; a member is undefined when its filter is not set. */
  filters: ComputedRef<ClientTicketListFilters>
  /**
   * Describes the applied ticket filters for `chipsOf`.
   *
   * @returns One entry per ticket filter, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<TicketFilterKey>[]
}

/**
 * Reads the ticket filters from the address of a ticket list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters; it must hold at least the {@link TicketFilterKey} fields.
 * @param departments - The support departments, read to name the department of a chip.
 * @returns The applied filters and their chip descriptions.
 */
export const useTicketFilters = (
  query: Pick<ListQuery<TicketFilterKey>, 'enumOf' | 'idOf'>,
  departments: Readonly<Ref<readonly Department[]>>,
): TicketFilters => {
  const { t } = useI18n()

  /** The status in the address as a ticket status, undefined when none is set or the value is not one. */
  const status = query.enumOf(TicketFilterKey.Status, TicketStatus)

  /** The priority in the address as a ticket priority, undefined when none is set or the value is not one. */
  const priority = query.enumOf(TicketFilterKey.Priority, TicketPriority)

  /** The department in the address as an id, undefined when none is set or the value is not an id. */
  const departmentId = query.idOf(TicketFilterKey.DepartmentId)

  return {
    filters: computed(() => ({ status: status.value, priority: priority.value, departmentId: departmentId.value })),
    chipSources: () => [
      {
        key: TicketFilterKey.Status,
        label: t('shared.status'),
        value: status.value === undefined ? undefined : statusLabel(TICKET_STATUS_OPTIONS, status.value),
      },
      {
        key: TicketFilterKey.Priority,
        label: t('shared.priority'),
        value: priority.value === undefined ? undefined : statusLabel(TICKET_PRIORITY_OPTIONS, priority.value),
      },
      {
        key: TicketFilterKey.DepartmentId,
        label: t('shared.department'),
        value: departmentId.value === undefined
          ? undefined
          : (departments.value.find(department => department.id === departmentId.value)?.name ?? String(departmentId.value)),
      },
    ],
  }
}
