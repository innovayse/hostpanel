<script setup lang="ts">
/**
 * Plan changes: paged list of the upgrade and downgrade requests of client services.
 *
 * Filters by status and, when opened from a service page (`?serviceId=`), shows that service's plan
 * change history. A pending or approved request can be approved or rejected after a confirmation
 * that carries an optional note. Reads and writes `stores/serviceChangeRequests`; the outcome of a
 * decision shows above the table, a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useResultBanner } from '@/composables/useResultBanner'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
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
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatPrice } from '@/lib/money'
import { AppRoute, ClientSection, clientItemPath, clientPath, invoicePath } from '@/lib/routes'
import { serviceChangeStatusLabel, serviceChangeStatusTone, serviceChangeTypeLabel, serviceChangeTypeTone } from '@/lib/tones'
import { useServiceChangeRequestsStore } from '@/stores/serviceChangeRequests'
import { ServiceChangeDecision, ServiceChangeStatus } from '@/types/servicechangerequest'
import type { ServiceChangeRequestItem } from '@/types/servicechangerequest'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** The route, whose `serviceId` query narrows the list to one service. */
const route = useRoute()

/** Plan change requests store: the page, its filters and the load state. */
const store = useServiceChangeRequestsStore()

/** The request awaiting a decision and which decision, null when no prompt is open. */
const staged = ref<{ id: number; decision: ServiceChangeDecision } | null>(null)

/** The optional staff note typed in the prompt. */
const note = ref('')

/** True while the staged decision is being sent. */
const deciding = ref(false)

/** The outcome line of the last decision. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Whether the current page has any request to list. */
const hasRows = computed<boolean>(() => store.requests.length > 0)

/** Options of the status filter: every status, then each lifecycle status. */
const statusOptions = computed(() => [
  { value: '' as string, label: t('serviceChangeRequests.allStatuses') },
  ...Object.values(ServiceChangeStatus).map((status) => ({ value: status as string, label: serviceChangeStatusLabel(status) })),
])

/** The service whose history is shown, taken from the route query; null for every service. */
const routeServiceId = computed<number | null>(() => {
  const raw = Number(route.query.serviceId)
  return Number.isInteger(raw) && raw > 0 ? raw : null
})

/** True when the prompt stages a rejection. */
const rejecting = computed<boolean>(() => staged.value?.decision === ServiceChangeDecision.Reject)

/**
 * Loads the current page; a refusal is left in `store.error`, shown with Retry.
 *
 * @returns Resolves when the list has settled.
 */
const load = (): Promise<void> => store.fetchAll()

/**
 * Updates the status filter and loads the first page.
 *
 * @param value - The chosen status, empty for every status.
 */
const setStatus = (value: string): void => {
  store.status = value as ServiceChangeStatus | ''
  store.page = 1
  void load()
}

/**
 * Moves to a page of the list and loads it.
 *
 * @param page - Target page, 1-based.
 */
const goToPage = (page: number): void => {
  store.page = page
  void load()
}

/**
 * Whether staff can still decide a request: only open requests (pending, or approved but not yet applied).
 *
 * @param item - The row.
 * @returns True for a pending or approved request.
 */
const isDecidable = (item: ServiceChangeRequestItem): boolean =>
  item.status === ServiceChangeStatus.Pending || item.status === ServiceChangeStatus.Approved

/**
 * Opens the prompt for a decision on a request with an empty note.
 *
 * @param id - The request id.
 * @param decision - The decision to confirm.
 */
const stage = (id: number, decision: ServiceChangeDecision): void => {
  note.value = ''
  staged.value = { id, decision }
}

/**
 * Sends the staged decision and shows its outcome.
 *
 * @returns Promise that resolves when the attempt has finished.
 */
const handleDecision = async (): Promise<void> => {
  const target = staged.value
  if (target === null) {
    return
  }
  feedback.clear()
  deciding.value = true
  const text = note.value.trim() === '' ? null : note.value.trim()
  const approving = target.decision === ServiceChangeDecision.Approve
  const done = approving ? await store.approve(target.id, text) : await store.reject(target.id, text)
  deciding.value = false
  staged.value = null
  if (done) {
    feedback.show(t(approving ? 'serviceChangeRequests.approved' : 'serviceChangeRequests.rejected'), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Closes the prompt; ignored while the decision is being sent. */
const dismiss = (): void => {
  if (!deciding.value) {
    staged.value = null
  }
}

/**
 * The row actions of a request: approve and reject while it is open, none once it is resolved.
 *
 * @param item - The row.
 * @returns The action list passed to UiRowActions.
 */
const requestActions = (item: ServiceChangeRequestItem): UiMenuAction[] =>
  isDecidable(item)
    ? [
        {
          label: t('serviceChangeRequests.approve'),
          testid: `plan-changes-approve-${item.id}`,
          /**
           * Opens the approval prompt for this row.
           *
           * @returns Nothing.
           */
          onSelect: (): void => {
            stage(item.id, ServiceChangeDecision.Approve)
          },
        },
        {
          label: t('serviceChangeRequests.reject'),
          testid: `plan-changes-reject-${item.id}`,
          /**
           * Opens the rejection prompt for this row.
           *
           * @returns Nothing.
           */
          onSelect: (): void => {
            stage(item.id, ServiceChangeDecision.Reject)
          },
          danger: true,
        },
      ]
    : []

/** Applies the service filter from the route and reloads from the first page whenever it changes. */
watch(routeServiceId, (serviceId) => {
  store.serviceId = serviceId
  store.page = 1
  void load()
})

/** Loads the first page with the service filter the route carries. */
onMounted(() => {
  store.serviceId = routeServiceId.value
  store.page = 1
  return load()
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="store.serviceId === null ? t('serviceChangeRequests.title') : t('serviceChangeRequests.history')"
      :subtitle="t('serviceChangeRequests.subtitle')"
      data-testid="plan-changes-header"
    />

    <UiStack direction="row" align="center">
      <UiSelect
        :model-value="store.status"
        :options="statusOptions"
        :placeholder="t('serviceChangeRequests.filterStatus')"
        data-testid="plan-changes-status-filter"
        class="w-48"
        @update:model-value="setStatus"
      />
      <template v-if="store.serviceId !== null">
        <UiBadge small>{{ t('serviceChangeRequests.serviceFilter', { id: store.serviceId }) }}</UiBadge>
        <UiLink variant="link" :to="AppRoute.ClientsPlanChanges" data-testid="plan-changes-clear-service">
          {{ t('serviceChangeRequests.clearServiceFilter') }}
        </UiLink>
      </template>
    </UiStack>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="plan-changes-feedback" />

    <UiErrorState
      v-if="store.error !== null"
      :message="store.error"
      :loading="store.loading"
      testid="plan-changes-error"
      @retry="load"
    />

    <UiCard v-if="hasRows || store.loading">
      <UiTable :density="UiTableDensity.Comfortable" :loading="store.loading && !hasRows" data-testid="plan-changes-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('serviceChangeRequests.service') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('serviceChangeRequests.plans') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('serviceChangeRequests.proration') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="item in store.requests"
            :key="item.id"
            hoverable
            bordered
            :data-testid="`plan-changes-row-${item.id}`"
          >
            <UiTableCell muted>{{ formatDate(item.createdAt) }}</UiTableCell>
            <UiTableCell>
              <UiLink
                variant="link"
                :to="clientItemPath(item.clientId, ClientSection.Services, item.serviceId)"
                :data-testid="`plan-changes-service-${item.id}`"
              >
                #{{ item.serviceId }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink variant="link" :to="clientPath(item.clientId)" :data-testid="`plan-changes-client-${item.id}`">
                {{ item.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiText>{{ item.fromProductName }} → {{ item.toProductName }}</UiText>
              <UiText v-if="item.note" tone="muted" size="xs">{{ t('serviceChangeRequests.note') }}: {{ item.note }}</UiText>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="serviceChangeTypeTone(item.type)" small>{{ serviceChangeTypeLabel(item.type) }}</UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiText>{{ formatPrice(item.prorationAmount, item.currencyCode) }}</UiText>
              <UiLink
                v-if="item.invoiceId !== null"
                variant="link"
                :to="invoicePath(item.invoiceId)"
                :data-testid="`plan-changes-invoice-${item.id}`"
              >
                {{ t('serviceChangeRequests.invoice', { id: item.invoiceId }) }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="serviceChangeStatusTone(item.status)" small>{{ serviceChangeStatusLabel(item.status) }}</UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="requestActions(item)" :testid="`plan-changes-actions-${item.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiStack direction="row" justify="between" align="center" class="p-3">
        <UiText tone="muted" size="xs">
          {{ t('serviceChangeRequests.totalCount', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="store.page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          testid="plan-changes-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else-if="!store.error">
      <UiEmptyState :title="t('serviceChangeRequests.empty')" data-testid="plan-changes-empty">
        <template #icon>
          <UiIcon :markup="Icon.Remove" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>

    <UiDialog
      v-if="staged !== null"
      variant="confirm"
      :title="rejecting ? t('serviceChangeRequests.rejectTitle') : t('serviceChangeRequests.approveTitle')"
      :message="rejecting ? t('serviceChangeRequests.rejectMessage') : t('serviceChangeRequests.approveMessage')"
      :confirm-label="rejecting ? t('serviceChangeRequests.reject') : t('serviceChangeRequests.approve')"
      :loading="deciding"
      :tone="rejecting ? UiTone.Danger : UiTone.Primary"
      data-testid="plan-changes-dialog"
      @confirm="handleDecision"
      @close="dismiss"
    >
      <UiTextarea
        v-model="note"
        rows="3"
        :placeholder="t('serviceChangeRequests.notePlaceholder')"
        data-testid="plan-changes-note"
      />
    </UiDialog>
  </UiStack>
</template>
