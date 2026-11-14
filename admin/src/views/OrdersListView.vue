<script setup lang="ts">
/**
 * Orders list view: paged table of all customer orders with a status filter.
 *
 * The status filter and the page live in the address (`useFilterQuery`): the filter button opens the status field, an
 * applied status shows as a removable chip, and a reload or a shared link restores the same list. Shows order number, client, status (with the fraud score of an order held for review), IP address, payment method,
 * total, item count and date, with row actions to accept or cancel a pending order and to open its detail. Reads `stores/orders`;
 * a refused load shows `store.error` with a retry, and the outcome of an accept or cancel (success, or
 * the API's sentence) shows above the table. Cancelling is confirmed first. A button
 * in the header opens the queue of orders pending fraud review.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { ORDER_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { OrderStatus } from '@/types/order'
import type { OrderListItem } from '@/types/order'
import { orderStatusTone, statusLabel } from '@/lib/tones'
import { useOrdersStore } from '@/stores/orders'
import { clientPath, orderPath } from '@/lib/routes'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { useResultBanner } from '@/composables/useResultBanner'
import type { UiFilterChip } from '@/utils/ui/filter'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

const store = useOrdersStore()

/** The status filter and the page, kept in the address. */
const query = useFilterQuery(['status'] as const)

/** The status in the address as an order status, null when none is set or the value is not one. */
const statusOfAddress = computed<OrderStatus | null>(
  () => Object.values(OrderStatus).find((value) => value === query.applied.value.status) ?? null,
)

/** The applied status as a removable chip, none while the list is not filtered. */
const chips = computed<UiFilterChip<'status'>[]>(() =>
  statusOfAddress.value === null
    ? []
    : [{ key: 'status', label: t('shared.status'), value: statusLabel(ORDER_STATUS_OPTIONS, statusOfAddress.value) }],
)

/** The outcome line of the last accept or cancel. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** The order waiting for the cancel confirmation, null when the prompt is closed. */
const cancelling = ref<number | null>(null)

/** True while an accept or cancel is in flight. */
const processing = ref(false)

/**
 * Runs one order command, shows its outcome and reloads the list on success.
 *
 * @param command - The store call to run.
 * @param successKey - Locale key of the success sentence.
 * @returns True when the API accepted the command, once the reload has settled.
 */
const runCommand = async (command: () => Promise<boolean>, successKey: string): Promise<boolean> => {
  feedback.clear()
  processing.value = true
  const ok = await command()
  processing.value = false
  if (ok) {
    feedback.show(t(successKey), false)
    await load()
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
  return ok
}

/**
 * Loads the page and the status the address holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(query.page.value, statusOfAddress.value)

/**
 * Shows the queue of orders held for fraud review: puts that status in the address.
 *
 * @returns Resolves when the address has changed.
 */
const showReviewQueue = async (): Promise<void> => {
  query.draft.value.status = OrderStatus.UnderReview
  await query.apply()
}

/**
 * Accepts a pending order, then reloads the list.
 *
 * @param id - The order primary key.
 * @returns Resolves when the list has been refreshed.
 */
const accept = async (id: number): Promise<void> => {
  const ok = await runCommand(() => store.acceptOrder(id), 'orders.ordersList.orderAccepted')
  if (ok && store.unfulfilledLines.length > 0) {
    feedback.show(t('orders.fulfillment.acceptedWithFailedLines', { lines: store.unfulfilledLineNames }), true)
  }
}

/**
 * Cancels the order the operator confirmed, then reloads the list.
 *
 * @returns Resolves when the list has been refreshed.
 */
const confirmCancel = async (): Promise<void> => {
  const id = cancelling.value
  if (id === null) {
    return
  }
  await runCommand(() => store.cancelOrder(id), 'orders.ordersList.orderCancelled')
  cancelling.value = null
}

/** Closes the cancel confirmation; ignored while the cancel runs. */
const dismissCancel = (): void => {
  if (!processing.value) {
    cancelling.value = null
  }
}

/**
 * The row actions of an order: accept a pending one, open its detail as a real link, and cancel a
 * pending one last (confirmed first). A settled order only offers the detail link.
 *
 * @param order - The order of the row.
 * @returns The action list passed to UiRowActions.
 */
const orderActions = (order: OrderListItem): UiMenuAction[] => {
  const pending = order.status === OrderStatus.Pending
  /**
   * Accepts this pending order.
   *
   * @returns Nothing.
   */
  const onAccept = (): void => {
    void accept(order.id)
  }
  /**
   * Asks for confirmation before this order is cancelled.
   *
   * @returns Nothing.
   */
  const onCancel = (): void => {
    cancelling.value = order.id
  }
  return [
    ...(pending ? [{ label: t('orders.ordersList.accept'), testid: `orders-accept-${order.id}`, onSelect: onAccept }] : []),
    { label: t('shared.view'), testid: `orders-view-${order.id}`, to: orderPath(order.id) },
    ...(pending ? [{ label: t('shared.cancel'), testid: `orders-cancel-${order.id}`, danger: true, onSelect: onCancel }] : []),
  ]
}

// The address is the one source of the list: a filter, a page or the back button reloads it.
watch([statusOfAddress, query.page], () => {
  void load()
})

onMounted(() => load())
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('orders.ordersList.orders')"
      :subtitle="t('orders.ordersList.manageCustomerOrders')"
      data-testid="orders-header"
    >
      <template #actions>
        <UiButton
          :variant="UiButtonVariant.Secondary"
          size="sm"
          data-testid="orders-review-queue"
          @click="showReviewQueue"
        >
          {{ t('orders.ordersList.reviewQueue') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiListToolbar
      :active-count="query.activeCount.value"
      :chips="chips"
      @apply="query.apply"
      @reset="query.reset"
      @cancel="query.cancel"
      @remove="query.remove"
      @clear="query.clearAll"
    >
      <UiField :label="t('shared.status')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="query.draft.value.status"
            :options="ORDER_STATUS_OPTIONS"
            :placeholder="t('orders.ordersList.filterByStatus')"
            data-testid="orders-filter-status"
          />
        </template>
      </UiField>
    </UiListToolbar>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="orders-action-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="orders-error"
      @retry="load"
    />

    <UiCard v-else-if="store.orders.length > 0 || store.loading">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && store.orders.length === 0"
        data-testid="orders-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader card-role="title">{{ t('orders.ordersList.order') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader card-role="badge">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.ipAddress') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.paymentMethod') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.total') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('orders.ordersList.items') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="order in store.orders"
            :key="order.id"
            hoverable
            bordered
            :data-testid="`orders-row-${order.id}`"
          >
            <UiTableCell>{{ order.orderNumber }}</UiTableCell>
            <UiTableCell>
              <UiLink variant="link" :to="clientPath(order.clientId)" :data-testid="`orders-client-${order.id}`">
                {{ order.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="xs" align="center" wrap>
                <UiBadge :tone="orderStatusTone(order.status)" small>
                  {{ statusLabel(ORDER_STATUS_OPTIONS, order.status) }}
                </UiBadge>
                <UiBadge
                  v-if="order.fraudScore !== null"
                  :tone="UiTone.Danger"
                  small
                  :data-testid="`orders-fraud-score-${order.id}`"
                >
                  {{ order.fraudScore }}
                </UiBadge>
                <UiText v-if="order.failedLineCount > 0" tone="muted" size="xs" :data-testid="`orders-failed-lines-${order.id}`">
                  {{ t('orders.ordersList.failedLines', { count: order.failedLineCount }) }}
                </UiText>
                <UiText
                  v-else-if="order.status === OrderStatus.Incomplete"
                  tone="muted"
                  size="xs"
                  :data-testid="`orders-needs-attention-${order.id}`"
                >
                  {{ t('orders.ordersList.needsAttention') }}
                </UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ order.ipAddress ?? '-' }}</UiTableCell>
            <UiTableCell>{{ order.paymentMethod }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(order.total, order.currency) }}</UiTableCell>
            <UiTableCell numeric muted>{{ order.itemCount }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(order.createdAt) }}</UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="orderActions(order)" :testid="`orders-actions-${order.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiText tone="muted" size="xs">
            {{ t('orders.ordersList.totalcountOrderValue', { totalCount: store.totalCount }) }}
          </UiText>
          <UiPagination
            :page="query.page.value"
            :total="store.totalCount"
            :page-size="store.pageSize"
            testid="orders-pagination"
            @update:page="query.setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="statusOfAddress === null ? t('orders.ordersList.noOrdersFound') : t('orders.ordersList.noOrdersMatchYourFilters')"
        data-testid="orders-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Clipboard" size="lg" />
        </template>
        <template v-if="statusOfAddress !== null" #actions>
          <UiButton :variant="UiButtonVariant.Link" data-testid="orders-clear-filters" @click="query.clearAll">
            {{ t('orders.ordersList.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>

    <UiDialog
      :open="cancelling !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('orders.ordersList.confirmCancelTitle')"
      :message="t('orders.ordersList.confirmCancelMessage')"
      :confirm-label="t('orders.ordersList.cancelOrder')"
      :loading="processing"
      :tone="UiTone.Danger"
      @confirm="confirmCancel"
      @close="dismissCancel"
    />
  </UiStack>
</template>
