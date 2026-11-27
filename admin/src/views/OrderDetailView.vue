<script setup lang="ts">
/**
 * Order detail view: one order with its items, linked invoice and the admin actions
 * (accept, cancel, delete), the fraud panel with approve and reject for an order held for review, a retry on each line the API says may be retried (`canRetry`, decided
 * with its configured lease), and a button that closes a line as refunded by hand on each line the
 * API says may be closed (`canClose`: failed, stuck in refunding past the retry lease, or partly refunded).
 *
 * The page opens on a header (number, status badge, date, the admin actions) over three tabs: overview
 * (order info, invoice and the fraud panel), items and notes. While the order is read the page draws the cards of
 * `OrderDetailView.schema` as placeholders, with no tabs. Reads `stores/orders`; a refused load shows `store.error` with a retry. The outcome of an accept
 * or cancel (success, or the API's sentence) shows under the header; a delete returns to the list.
 * Cancelling, deleting and closing a refund are confirmed first in a danger prompt; a rejection asks for the reason the
 * client is mailed.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import OrderFact from '@/components/orders/OrderFact.vue'
import OrderFraudPanel from '@/components/orders/OrderFraudPanel.vue'
import OrderRejectReviewDialog from '@/components/orders/OrderRejectReviewDialog.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiTextTag, UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { BILLING_CYCLE_OPTIONS, INVOICE_STATUS_OPTIONS, ORDER_LINE_FULFILMENT_OPTIONS, ORDER_STATUS_OPTIONS } from '@/lib/constants'
import { formatDateTime, EMPTY_VALUE } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { invoiceStatusTone, orderItemStatusTone, orderLineFulfilmentTone, orderStatusTone, statusLabel } from '@/lib/tones'
import { orderLineErrorKey } from '@/lib/orderLines'
import { OrderItemFulfilmentState, OrderStatus } from '@/types/order'
import { useOrdersStore } from '@/stores/orders'
import { AppRoute, clientPath, invoicePath } from '@/lib/routes'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { usePageCrumbs } from '@/composables/usePageCrumbs'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'
import schema from '@/views/OrderDetailView.schema'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiCardPadding } from '@/utils/ui/card'
import { UiSize } from '@/utils/ui/size'

/** The irreversible order changes the danger confirmation guards. */
enum OrderDangerAction {
  /** Cancel the pending order. */
  Cancel = 'cancel',
  /** Delete the order. */
  Delete = 'delete',
  /** Close a partly refunded line as refunded. */
  CloseRefund = 'closeRefund',
}

/** The tabs of the page. */
enum OrderTab {
  /** Order info, invoice and the fraud panel. */
  Overview = 'overview',
  /** The order lines with their fulfilment. */
  Items = 'items',
  /** The admin notes. */
  Notes = 'notes',
}

const { t } = useI18n()

/** Builds the trail the header draws above the title: Orders, the order. */
const { crumbs } = usePageCrumbs()

const route = useRoute()
const router = useRouter()
const store = useOrdersStore()

/** The order on show; null while it is still being read, when the page draws its layout with placeholders. */
const order = computed(() => store.currentOrder)

/** The tab on show. */
const tab = ref<OrderTab>(OrderTab.Overview)

/** The tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: OrderTab.Overview, label: t('orders.orderDetail.overview'), testid: 'order-tab-overview' },
  { value: OrderTab.Items, label: t('orders.ordersList.items'), testid: 'order-tab-items' },
  { value: OrderTab.Notes, label: t('shared.adminNotes'), testid: 'order-tab-notes' },
])

/** The irreversible change waiting for confirmation, null when the prompt is closed. */
const dangerConfirm = ref<OrderDangerAction | null>(null)

/** The header actions of the order, each with its icon: accept, cancel and delete, as far as the order's status allows. */
const headerActions = computed<UiHeaderAction[]>(() => {
  const current = order.value
  if (current === null) {
    return []
  }
  const pending = current.status === OrderStatus.Pending
  return [
    ...(pending ? [headerAction(UiActionKind.Confirm, { label: t('orders.accept'), variant: UiButtonVariant.Secondary, testid: 'order-accept', onSelect: handleAccept })] : []),
    ...(pending ? [headerAction(UiActionKind.Cancel, { label: t('shared.cancel'), variant: UiButtonVariant.Danger, testid: 'order-cancel', onSelect: askCancel })] : []),
    ...(pending || current.status === OrderStatus.Cancelled ? [headerAction(UiActionKind.Delete, { label: t('shared.delete'), variant: UiButtonVariant.Ghost, testid: 'order-delete', onSelect: askDelete })] : []),
  ]
})

/** Asks the operator to confirm cancelling the order. */
const askCancel = (): void => {
  dangerConfirm.value = OrderDangerAction.Cancel
}

/** Asks the operator to confirm deleting the order. */
const askDelete = (): void => {
  dangerConfirm.value = OrderDangerAction.Delete
}

/** The partly refunded line whose refund is waiting to be closed, null when none. */
const closingRefundItemId = ref<number | null>(null)

/** Whether the reject prompt for an order held for review is open. */
const rejecting = ref(false)

/** True while an order command is in flight. */
const processing = ref(false)

/** The outcome line of the last accept or cancel. */
const feedback = useOutcomeToast()

/** Title of the danger confirmation. */
const dangerTitle = computed<string>(() => {
  switch (dangerConfirm.value) {
    case OrderDangerAction.Cancel:
      return t('orders.orderDetail.confirmCancelTitle')
    case OrderDangerAction.CloseRefund:
      return t('orders.orderDetail.confirmCloseRefundTitle')
    default:
      return t('orders.orderDetail.deleteOrder')
  }
})

/** Prompt of the danger confirmation. */
const dangerMessage = computed<string>(() => {
  switch (dangerConfirm.value) {
    case OrderDangerAction.Cancel:
      return t('orders.orderDetail.confirmCancelMessage')
    case OrderDangerAction.CloseRefund:
      return t('orders.orderDetail.confirmCloseRefundMessage')
    default:
      return t('orders.orderDetail.areYouSureYouWant')
  }
})

/** Confirm label of the danger confirmation. */
const dangerLabel = computed<string>(() => {
  switch (dangerConfirm.value) {
    case OrderDangerAction.Cancel:
      return t('orders.cancelOrder')
    case OrderDangerAction.CloseRefund:
      return t('orders.orderDetail.closeRefund')
    default:
      return t('shared.delete')
  }
})

/** Admin notes text area value. */
const notesText = ref('')

/**
 * Reads the order id from the route.
 *
 * @returns The order primary key.
 */
const orderId = (): number => Number(route.params.id)

/**
 * Runs one order command and shows its outcome above the order.
 *
 * @param command - The store call to run.
 * @param successKey - Locale key of the success sentence, or null when success navigates away.
 * @returns True when the API accepted the command.
 */
const runCommand = async (command: () => Promise<boolean>, successKey: string | null): Promise<boolean> => {
  processing.value = true
  const ok = await command()
  processing.value = false
  if (!ok) {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  } else if (successKey !== null) {
    feedback.show(t(successKey), false)
  }
  return ok
}

/**
 * Accepts the current order.
 *
 * @returns Resolves when the store has finished.
 */
const handleAccept = async (): Promise<void> => {
  const ok = await runCommand(() => store.acceptOrder(orderId()), 'orders.orderDetail.orderAccepted')
  if (ok && store.unfulfilledLines.length > 0) {
    feedback.show(t('orders.fulfillment.acceptedWithFailedLines', { lines: store.unfulfilledLineNames }), true)
  }
}

/**
 * Approves the order held for fraud review.
 *
 * @returns Resolves when the store has finished.
 */
const handleApproveReview = async (): Promise<void> => {
  await runCommand(() => store.approveReview(orderId()), 'orders.fraud.approved')
}

/**
 * Rejects the order held for fraud review with the reason the operator typed, then closes the prompt.
 *
 * @param reason - Why the order is refused; the client is mailed it.
 * @returns Resolves when the store has finished.
 */
const handleRejectReview = async (reason: string): Promise<void> => {
  await runCommand(() => store.rejectReview(orderId(), reason), 'orders.fraud.rejected')
  rejecting.value = false
}

/**
 * Retries one order line and shows the outcome above the order. The success sentence is chosen
 * after the result: a retry that ran but failed again says so instead of claiming success.
 *
 * @param itemId - The failed or stale line.
 * @returns Resolves when the store has finished.
 */
const handleRetryLine = async (itemId: number): Promise<void> => {
  const ok = await runCommand(() => store.retryOrderLine(orderId(), itemId), null)
  if (!ok) {
    return
  }
  if (store.unfulfilledLines.length > 0) {
    feedback.show(t('orders.fulfillment.retryStillFailed', { lines: store.unfulfilledLineNames }), true)
  } else {
    feedback.show(t('orders.orderDetail.lineRetried'), false)
  }
}

/**
 * Asks the operator to confirm that the cost of a line was given back by hand, before it is closed as refunded.
 *
 * @param itemId - The failed, refunding or partly refunded line.
 */
const askCloseRefund = (itemId: number): void => {
  closingRefundItemId.value = itemId
  dangerConfirm.value = OrderDangerAction.CloseRefund
}

/**
 * Runs the change the operator confirmed: a cancel and a closed refund stay on the order, a delete
 * returns to the list; a refusal stays on the page with the API's sentence.
 *
 * @returns Resolves when the change has settled.
 */
const confirmDanger = async (): Promise<void> => {
  if (dangerConfirm.value === OrderDangerAction.CloseRefund && closingRefundItemId.value !== null) {
    const itemId = closingRefundItemId.value
    await runCommand(() => store.closeLineRefund(orderId(), itemId), 'orders.orderDetail.refundClosed')
    dangerConfirm.value = null
    closingRefundItemId.value = null
    return
  }
  if (dangerConfirm.value === OrderDangerAction.Cancel) {
    await runCommand(() => store.cancelOrder(orderId()), 'orders.orderDetail.orderCancelled')
    dangerConfirm.value = null
    return
  }
  const deleted = await runCommand(() => store.deleteOrder(orderId()), null)
  dangerConfirm.value = null
  if (deleted) {
    await router.push(AppRoute.Orders)
  }
}

/** Closes the danger confirmation; ignored while the confirmed change runs. */
const dismissDanger = (): void => {
  if (!processing.value) {
    dangerConfirm.value = null
    closingRefundItemId.value = null
  }
}

onMounted(async () => {
  // A refusal from an earlier visit must not greet the reader.
  store.actionError = null
  await store.fetchOne(orderId())
  if (store.currentOrder) {
    notesText.value = store.currentOrder.notes ?? ''
  }
})
</script>

<template>
  <UiStack>
    <template v-if="store.error">
      <UiErrorState
        :message="store.error"
        :loading="store.loading"
        testid="order-error"
        @retry="store.fetchOne(orderId())"
      />
    </template>

    <!-- The order, or while it is read the cards of its schema with bars where the values go -->
    <template v-else>
      <UiPageHeader
        :actions="headerActions"
        :title="order?.orderNumber"
        :loading="order === null"
        :crumbs="crumbs(order?.orderNumber)"
        data-testid="order-header"
      >
        <template #badge>
          <UiBadge v-if="order !== null" :tone="orderStatusTone(order.status)" data-testid="order-status">
            {{ statusLabel(ORDER_STATUS_OPTIONS, order.status) }}
          </UiBadge>
        </template>
        <UiText v-if="order !== null" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="mt-1.5">
          {{ formatDateTime(order.createdAt) }}
        </UiText>
      </UiPageHeader>


      <UiTabs v-if="order !== null" v-model="tab" :items="tabItems" :aria-label="order.orderNumber" />

      <template v-if="order === null || tab === OrderTab.Overview">
        <UiDetailCards :columns="schema.columns" :loading="order === null">
          <template #card-info>
            <UiStack v-if="order !== null" data-testid="order-info">
              <OrderFact :label="t('shared.client')">
                <UiLink :variant="UiButtonVariant.Link" :to="clientPath(order.clientId)" data-testid="order-client">
                  {{ order.clientName }}
                </UiLink>
              </OrderFact>
              <OrderFact :label="t('shared.paymentMethod')">
                {{ order.paymentMethod }}
              </OrderFact>
              <OrderFact :label="t('shared.ipAddress')">
                {{ order.ipAddress ?? EMPTY_VALUE }}
              </OrderFact>
              <OrderFact :label="t('orders.orderDetail.created')">
                {{ formatDateTime(order.createdAt) }}
              </OrderFact>
              <OrderFact v-if="order.bundleName" :label="t('orders.orderDetail.bundle')">
                <UiText :as="UiTextTag.Span" data-testid="order-bundle">{{ order.bundleName }}</UiText>
              </OrderFact>
              <OrderFact :label="t('orders.orderDetail.linesSubtotal')">
                <UiText :as="UiTextTag.Span" data-testid="order-lines-subtotal">
                  {{ formatPlainAmount(order.total, order.currency) }}
                </UiText>
              </OrderFact>
            </UiStack>
          </template>

          <template #card-invoice>
            <UiStack v-if="order !== null" data-testid="order-invoice">
              <template v-if="order.invoiceId">
                <OrderFact :label="t('shared.invoiceId')">
                  <UiLink :variant="UiButtonVariant.Link" :to="invoicePath(order.invoiceId)" data-testid="order-invoice-link">
                    #{{ order.invoiceId }}
                  </UiLink>
                </OrderFact>
                <OrderFact :label="t('shared.status')">
                  <UiBadge
                    v-if="order.invoiceStatus"
                    :tone="invoiceStatusTone(order.invoiceStatus)"
                    small
                  >
                    {{ statusLabel(INVOICE_STATUS_OPTIONS, order.invoiceStatus) }}
                  </UiBadge>
                  <template v-else>
                    {{ EMPTY_VALUE }}
                  </template>
                </OrderFact>
                <OrderFact :label="t('orders.orderDetail.invoiceTotal')">
                  <UiText :as="UiTextTag.Span" data-testid="order-invoice-total">
                    {{
                      order.invoiceTotal === null
                        ? EMPTY_VALUE
                        : formatPlainAmount(order.invoiceTotal, order.currency)
                    }}
                  </UiText>
                </OrderFact>
              </template>
              <UiText v-else :tone="UiTextTone.Muted" :size="UiTextSize.Md">
                {{ t('orders.orderDetail.noInvoiceLinkedToThis') }}
              </UiText>
            </UiStack>
          </template>
        </UiDetailCards>

        <OrderFraudPanel
          v-if="order !== null && order.fraudScore !== null"
          :order="order"
          :busy="processing"
          @approve="handleApproveReview"
          @reject="rejecting = true"
        />
      </template>

      <UiDataTable v-else-if="tab === OrderTab.Items" data-testid="order-items">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader :size="UiColumnSize.Grow">{{ t('orders.orderDetail.product') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('orders.orderDetail.firstPayment') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('orders.orderDetail.recurring') }}</UiTableHeader>
            <UiTableHeader :card-role="UiCardRole.Badge">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('orders.orderDetail.fulfilment') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="item in order.items"
            :key="item.id"
            hoverable
            :data-testid="`order-item-${item.id}`"
          >
            <UiTableCell>{{ item.productName }}</UiTableCell>
            <UiTableCell>{{ statusLabel(BILLING_CYCLE_OPTIONS, item.billingCycle) }}</UiTableCell>
            <UiTableCell>{{ item.domain ?? item.hostname ?? EMPTY_VALUE }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(item.firstPaymentAmount, order.currency) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPlainAmount(item.recurringAmount, order.currency) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="orderItemStatusTone(item.status)" small>
                {{ statusLabel(ORDER_STATUS_OPTIONS, item.status) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiStack :gap="UiGap.Xs">
                <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center" wrap>
                  <UiBadge
                    :tone="orderLineFulfilmentTone(item.fulfilmentState)"
                    small
                    :data-testid="`order-item-fulfilment-${item.id}`"
                  >
                    {{ statusLabel(ORDER_LINE_FULFILMENT_OPTIONS, item.fulfilmentState) }}
                  </UiBadge>
                  <UiButton
                    v-if="item.canRetry"
                    :action="UiActionKind.Refresh"
                    :variant="UiButtonVariant.Secondary"
                    :size="UiSize.Sm"
                    :loading="store.retryingItemId === item.id"
                    :disabled="store.retryingItemId !== null && store.retryingItemId !== item.id"
                    :data-testid="`order-item-retry-${item.id}`"
                    @click="handleRetryLine(item.id)"
                  >
                    {{ t('shared.retry') }}
                  </UiButton>
                </UiStack>
                <UiStack
                  v-if="item.canClose"
                  :direction="UiDirection.Row"
                  :gap="UiGap.Sm"
                  :align="UiAlignItems.Center"
                  wrap
                >
                  <UiText
                    v-if="item.fulfilmentState === OrderItemFulfilmentState.PartiallyRefunded"
                    :tone="UiTextTone.Warning"
                    :size="UiTextSize.Xs"
                    :data-testid="`order-item-finish-refund-${item.id}`"
                  >
                    {{ t('orders.orderDetail.finishRefund') }}
                  </UiText>
                  <UiButton
                    :action="UiActionKind.Confirm"
                    :variant="UiButtonVariant.Secondary"
                    :size="UiSize.Sm"
                    :data-testid="`order-item-close-refund-${item.id}`"
                    @click="askCloseRefund(item.id)"
                  >
                    {{ t('orders.orderDetail.closeRefund') }}
                  </UiButton>
                </UiStack>
                <UiText
                  v-if="item.fulfilmentErrorCode"
                  :tone="UiTextTone.Muted"
                  :size="UiTextSize.Xs"
                  :data-testid="`order-item-error-${item.id}`"
                >
                  {{ t(orderLineErrorKey(item.fulfilmentErrorCode)) }}
                </UiText>
                <UiText
                  v-if="store.lineErrors[item.id]"
                  :tone="UiTextTone.Danger"
                  :size="UiTextSize.Xs"
                  :data-testid="`order-item-retry-error-${item.id}`"
                >
                  {{ store.lineErrors[item.id] }}
                </UiText>
                <UiText
                  v-if="item.fulfilmentAttempts > 0 && item.lastFulfilmentAttemptAt"
                  :tone="UiTextTone.Muted"
                  :size="UiTextSize.Xs"
                  :data-testid="`order-item-attempts-${item.id}`"
                >
                  {{ t('orders.orderDetail.attempts', { count: item.fulfilmentAttempts, at: formatDateTime(item.lastFulfilmentAttemptAt) }) }}
                </UiText>
              </UiStack>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiDataTable>

      <UiCard v-else :padding="UiCardPadding.Md" data-testid="order-notes">
        <UiStack :gap="UiGap.Sm">
          <UiHeading :level="2" :variant="UiHeadingVariant.Section">
            {{ t('shared.adminNotes') }}
          </UiHeading>
          <UiTextarea
            v-model="notesText"
            :rows="4"
            :placeholder="t('orders.orderDetail.addNotesAboutThisOrder')"
            data-testid="order-notes-input"
          />
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
            {{ t('orders.orderDetail.notesAreOnlyVisibleTo') }}
          </UiText>
        </UiStack>
      </UiCard>
    </template>

    <OrderRejectReviewDialog
      v-if="rejecting"
      :loading="processing"
      @submit="handleRejectReview"
      @close="rejecting = false"
    />

    <UiDialog
      :open="dangerConfirm !== null"
      :variant="UiDialogVariant.Confirm"
      :title="dangerTitle"
      :message="dangerMessage"
      :confirm-label="dangerLabel"
      :loading="processing"
      :tone="UiTone.Danger"
      @confirm="confirmDanger"
      @close="dismissDanger"
    />
  </UiStack>
</template>
