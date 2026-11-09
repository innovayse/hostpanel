<script setup lang="ts">
/**
 * Orders list view: paged table of all customer orders with a status filter.
 *
 * Shows order number, client, status, payment method, total, item count and date, with row
 * actions to accept or cancel a pending order and to open its detail. Reads `stores/orders`;
 * a refused load shows `store.error` with a retry, and the outcome of an accept or cancel (success, or
 * the API's sentence) shows above the table. Cancelling is confirmed first.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
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
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const store = useOrdersStore()

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
    await store.fetchAll()
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
  return ok
}

/**
 * Moves to a page of the list and loads it.
 *
 * @param page - Target page, 1-based.
 */
const goToPage = (page: number): void => {
  store.page = page
  void store.fetchAll()
}

/**
 * Applies the status filter and reloads from the first page.
 *
 * @param status - Chosen status, empty for all.
 */
const onStatusChange = (status: string): void => {
  store.statusFilter = Object.values(OrderStatus).find((value) => value === status) ?? null
  store.page = 1
  void store.fetchAll()
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
const orderActions = (order: OrderListItem): UiRowAction[] => {
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

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('orders.ordersList.orders')"
      :subtitle="t('orders.ordersList.manageCustomerOrders')"
      data-testid="orders-header"
    >
      <template #actions>
        <UiSelect
          class="w-44"
          :model-value="store.statusFilter ?? ''"
          :options="ORDER_STATUS_OPTIONS"
          :placeholder="t('orders.ordersList.filterByStatus')"
          data-testid="orders-filter-status"
          @update:model-value="onStatusChange"
        />
      </template>
    </UiPageHeader>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="orders-action-feedback" />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="orders-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else-if="store.orders.length > 0 || store.loading">
      <UiTable density="comfortable" :loading="store.loading && store.orders.length === 0" :colspan="8" data-testid="orders-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('orders.ordersList.order') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
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

      <UiStack direction="row" justify="between" align="center" class="p-3">
        <UiText tone="muted" size="xs">
          {{ t('orders.ordersList.totalcountOrderValue', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="store.page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          testid="orders-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('orders.ordersList.noOrdersFound')" data-testid="orders-empty">
        <template #icon>
          <UiIcon :markup="Icon.Clipboard" size="lg" />
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
