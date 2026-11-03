<script setup lang="ts">
/**
 * Order detail view: one order with its items, linked invoice and the admin actions
 * (accept, cancel, delete).
 *
 * Reads `stores/orders`; a refused load shows `store.error` with a retry. The outcome of an accept
 * or cancel (success, or the API's sentence) shows above the order; a delete returns to the list.
 * Cancelling and deleting are confirmed first in a danger prompt.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import OrderFact from '@/components/orders/OrderFact.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { Icon } from '@/lib/icons'
import { BILLING_CYCLE_OPTIONS, INVOICE_STATUS_OPTIONS, ORDER_STATUS_OPTIONS } from '@/lib/constants'
import { formatDateTime } from '@/lib/format'
import { formatPlainAmount } from '@/lib/money'
import { invoiceStatusTone, orderItemStatusTone, orderStatusTone, statusLabel } from '@/lib/tones'
import { OrderStatus } from '@/types/order'
import { useOrdersStore } from '@/stores/orders'
import { AppRoute, clientPath, invoicePath } from '@/lib/routes'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'

/** The irreversible order changes the danger confirmation guards. */
enum OrderDangerAction {
  /** Cancel the pending order. */
  Cancel = 'cancel',
  /** Delete the order. */
  Delete = 'delete',
}

const { t } = useI18n()

const route = useRoute()
const router = useRouter()
const store = useOrdersStore()

/** The irreversible change waiting for confirmation, null when the prompt is closed. */
const dangerConfirm = ref<OrderDangerAction | null>(null)

/** True while an order command is in flight. */
const processing = ref(false)

/** The outcome line of the last accept or cancel. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Title of the danger confirmation. */
const dangerTitle = computed<string>(() =>
  dangerConfirm.value === OrderDangerAction.Cancel ? t('orders.orderDetail.confirmCancelTitle') : t('orders.orderDetail.deleteOrder'),
)

/** Prompt of the danger confirmation. */
const dangerMessage = computed<string>(() =>
  dangerConfirm.value === OrderDangerAction.Cancel ? t('orders.orderDetail.confirmCancelMessage') : t('orders.orderDetail.areYouSureYouWant'),
)

/** Confirm label of the danger confirmation. */
const dangerLabel = computed<string>(() =>
  dangerConfirm.value === OrderDangerAction.Cancel ? t('orders.orderDetail.cancelOrder') : t('shared.delete'),
)

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
  feedback.clear()
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
  if (ok && store.acceptFailures.length > 0) {
    feedback.show(t('orders.fulfillment.acceptedWithFailedLines', { lines: store.acceptFailureNames }), true)
  }
}

/**
 * Runs the change the operator confirmed: a cancel stays on the order, a delete returns to the
 * list; a refusal stays on the page with the API's sentence.
 *
 * @returns Resolves when the change has settled.
 */
const confirmDanger = async (): Promise<void> => {
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
    <UiLink variant="link" :to="AppRoute.Orders" class="self-start" data-testid="order-back">
      <UiIcon :markup="Icon.Back" size="sm" />
      {{ t('orders.orderDetail.backToOrders') }}
    </UiLink>

    <UiStack v-if="store.loading && !store.currentOrder" aria-busy="true" data-testid="order-loading">
      <UiText tone="secondary" size="md">
        {{ t('orders.orderDetail.loadingOrder') }}
      </UiText>
      <UiSkeleton :lines="3" />
    </UiStack>

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="order-error"
      @retry="store.fetchOne(orderId())"
    />

    <template v-else-if="store.currentOrder">
      <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="order-action-feedback" />

      <UiPageHeader :title="store.currentOrder.orderNumber" data-testid="order-header">
        <UiStack direction="row" gap="sm" align="center" wrap>
          <UiBadge :tone="orderStatusTone(store.currentOrder.status)" data-testid="order-status">
            {{ statusLabel(ORDER_STATUS_OPTIONS, store.currentOrder.status) }}
          </UiBadge>
          <UiText tone="muted" size="sm">
            {{ formatDateTime(store.currentOrder.createdAt) }}
          </UiText>
        </UiStack>
        <template #actions>
          <UiButton
            v-if="store.currentOrder.status === OrderStatus.Pending"
            variant="secondary"
            size="sm"
            data-testid="order-accept"
            @click="handleAccept"
          >
            <UiIcon :markup="Icon.Check" size="sm" />
            {{ t('orders.orderDetail.accept') }}
          </UiButton>
          <UiButton
            v-if="store.currentOrder.status === OrderStatus.Pending"
            variant="danger"
            size="sm"
            data-testid="order-cancel"
            @click="dangerConfirm = OrderDangerAction.Cancel"
          >
            <UiIcon :markup="Icon.Close" size="sm" />
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            v-if="store.currentOrder.status === OrderStatus.Pending || store.currentOrder.status === OrderStatus.Cancelled"
            variant="ghost"
            size="sm"
            data-testid="order-delete"
            @click="dangerConfirm = OrderDangerAction.Delete"
          >
            <UiIcon :markup="Icon.Trash" size="sm" />
            {{ t('shared.delete') }}
          </UiButton>
        </template>
      </UiPageHeader>

      <UiGrid :columns="2">
        <UiCard padding="md" data-testid="order-info">
          <UiStack>
            <UiHeading :level="2" variant="section">
              {{ t('orders.orderDetail.orderInfo') }}
            </UiHeading>
            <OrderFact :label="t('shared.client')">
              <UiLink variant="link" :to="clientPath(store.currentOrder.clientId)" data-testid="order-client">
                {{ store.currentOrder.clientName }}
              </UiLink>
            </OrderFact>
            <OrderFact :label="t('shared.paymentMethod')">
              {{ store.currentOrder.paymentMethod }}
            </OrderFact>
            <OrderFact :label="t('shared.ipAddress')">
              {{ store.currentOrder.ipAddress ?? '—' }}
            </OrderFact>
            <OrderFact :label="t('orders.orderDetail.created')">
              {{ formatDateTime(store.currentOrder.createdAt) }}
            </OrderFact>
            <OrderFact :label="t('orders.orderDetail.linesSubtotal')">
              <span data-testid="order-lines-subtotal">
                {{ formatPlainAmount(store.currentOrder.total, store.currentOrder.currency) }}
              </span>
            </OrderFact>
          </UiStack>
        </UiCard>

        <UiCard padding="md" data-testid="order-invoice">
          <UiStack>
            <UiHeading :level="2" variant="section">
              {{ t('orders.orderDetail.invoice') }}
            </UiHeading>
            <template v-if="store.currentOrder.invoiceId">
              <OrderFact :label="t('shared.invoiceId')">
                <UiLink variant="link" :to="invoicePath(store.currentOrder.invoiceId)" data-testid="order-invoice-link">
                  #{{ store.currentOrder.invoiceId }}
                </UiLink>
              </OrderFact>
              <OrderFact :label="t('shared.status')">
                <UiBadge
                  v-if="store.currentOrder.invoiceStatus"
                  :tone="invoiceStatusTone(store.currentOrder.invoiceStatus)"
                  small
                >
                  {{ statusLabel(INVOICE_STATUS_OPTIONS, store.currentOrder.invoiceStatus) }}
                </UiBadge>
                <template v-else>
                  &mdash;
                </template>
              </OrderFact>
              <OrderFact :label="t('orders.orderDetail.invoiceTotal')">
                <span data-testid="order-invoice-total">
                  {{
                    store.currentOrder.invoiceTotal === null
                      ? '—'
                      : formatPlainAmount(store.currentOrder.invoiceTotal, store.currentOrder.currency)
                  }}
                </span>
              </OrderFact>
            </template>
            <UiText v-else tone="muted" size="md">
              {{ t('orders.orderDetail.noInvoiceLinkedToThis') }}
            </UiText>
          </UiStack>
        </UiCard>
      </UiGrid>

      <UiCard data-testid="order-items">
        <UiTable density="comfortable">
          <UiTableHead>
            <UiTableRow bordered>
              <UiTableHeader>{{ t('orders.orderDetail.product') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.billingCycle') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
              <UiTableHeader>{{ t('orders.orderDetail.firstPayment') }}</UiTableHeader>
              <UiTableHeader>{{ t('orders.orderDetail.recurring') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody>
            <UiTableRow
              v-for="item in store.currentOrder.items"
              :key="item.id"
              hoverable
              bordered
              :data-testid="`order-item-${item.id}`"
            >
              <UiTableCell>{{ item.productName }}</UiTableCell>
              <UiTableCell>{{ statusLabel(BILLING_CYCLE_OPTIONS, item.billingCycle) }}</UiTableCell>
              <UiTableCell>{{ item.domain ?? item.hostname ?? '—' }}</UiTableCell>
              <UiTableCell>{{ formatPlainAmount(item.firstPaymentAmount, store.currentOrder.currency) }}</UiTableCell>
              <UiTableCell>{{ formatPlainAmount(item.recurringAmount, store.currentOrder.currency) }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="orderItemStatusTone(item.status)" small>
                  {{ statusLabel(ORDER_STATUS_OPTIONS, item.status) }}
                </UiBadge>
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard padding="md" data-testid="order-notes">
        <UiStack gap="sm">
          <UiHeading :level="2" variant="section">
            {{ t('shared.adminNotes') }}
          </UiHeading>
          <UiTextarea
            v-model="notesText"
            :rows="4"
            :placeholder="t('orders.orderDetail.addNotesAboutThisOrder')"
            data-testid="order-notes-input"
          />
          <UiText tone="muted" size="xs">
            {{ t('orders.orderDetail.notesAreOnlyVisibleTo') }}
          </UiText>
        </UiStack>
      </UiCard>
    </template>

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
