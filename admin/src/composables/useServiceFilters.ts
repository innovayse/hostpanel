/**
 * The service filters every service list shares: status, product and billing cycle.
 *
 * Both service lists (the admin list and the tab of a client's profile) keep these three filters in the address
 * through `useListQuery`. This reads them back as the typed values the API takes, and describes them as chips, so
 * the two screens do not each carry the same conversion.
 */
import { computed } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListChipSource, ListQuery } from '@/composables/useListQuery'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import { statusLabel } from '@/lib/tones'
import type { Product } from '@/types/product'
import { BillingCycle, ServiceStatus } from '@/types/service'
import type { ServiceListFilters } from '@/types/service'

/** The service filter fields every service list has; each is one query key and one `ServiceListFilters` member. */
export const ServiceFilterKey = {
  Status: 'status',
  ProductId: 'productId',
  BillingCycle: 'billingCycle',
} as const

/** One of the {@link ServiceFilterKey} values. */
export type ServiceFilterKey = (typeof ServiceFilterKey)[keyof typeof ServiceFilterKey]

/** What {@link useServiceFilters} hands the service list screen. */
export interface ServiceFilters {
  /** The applied status, product and billing cycle as the API takes them; a member is undefined when its filter is not set. */
  filters: ComputedRef<ServiceListFilters>
  /**
   * Describes the applied service filters for `chipsOf`.
   *
   * @returns One entry per service filter, with the value as the operator reads it, undefined when it is not set.
   */
  chipSources: () => ListChipSource<ServiceFilterKey>[]
}

/**
 * Reads the service filters from the address of a service list.
 *
 * Must run in a component's setup, because it reads the locale.
 *
 * @param query - The list's address-backed filters; it must hold at least the {@link ServiceFilterKey} fields.
 * @param products - The products, read to name the product of a chip.
 * @returns The applied filters and their chip descriptions.
 */
export const useServiceFilters = (
  query: Pick<ListQuery<ServiceFilterKey>, 'enumOf' | 'idOf'>,
  products: Readonly<Ref<readonly Product[]>>,
): ServiceFilters => {
  const { t } = useI18n()

  /** The status in the address as a service status, undefined when none is set or the value is not one. */
  const status = query.enumOf(ServiceFilterKey.Status, ServiceStatus)

  /** The product in the address as an id, undefined when none is set or the value is not an id. */
  const productId = query.idOf(ServiceFilterKey.ProductId)

  /** The billing cycle in the address as a billing cycle, undefined when none is set or the value is not one. */
  const billingCycle = query.enumOf(ServiceFilterKey.BillingCycle, BillingCycle)

  return {
    /** The service filters the address names, ready for the list request. */
    filters: computed(() => ({ status: status.value, productId: productId.value, billingCycle: billingCycle.value })),
    /**
     * The status, product and billing cycle chips.
     *
     * @returns Each filter with its key, label and the value the address holds.
     */
    chipSources: () => [
      {
        key: ServiceFilterKey.Status,
        label: t('shared.status'),
        value: status.value === undefined ? undefined : statusLabel(SERVICE_STATUS_OPTIONS, status.value),
      },
      {
        key: ServiceFilterKey.ProductId,
        label: t('shared.productService'),
        value: productId.value === undefined
          ? undefined
          : (products.value.find(product => product.id === productId.value)?.name ?? String(productId.value)),
      },
      {
        key: ServiceFilterKey.BillingCycle,
        label: t('shared.billingCycle'),
        value: billingCycle.value === undefined ? undefined : statusLabel(BILLING_CYCLE_OPTIONS, billingCycle.value),
      },
    ],
  }
}
