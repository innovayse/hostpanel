<template>
  <UiCard data-testid="service-cancel">
    <UiCardHeader :title="$t('client.services.cancelTitle')" :icon="XCircle" />

    <UiAlert v-if="cancelDone" variant="success" data-testid="service-cancel-success">
      {{ $t('client.services.cancelSuccess') }}
    </UiAlert>

    <template v-else>
      <UiText size="sm" tone="muted" class="mb-5">{{ $t('client.services.cancelDesc') }}</UiText>

      <UiForm data-testid="service-cancel-form" :error="cancelError" spacing="sm" @submit="showConfirm = true">
        <!--
          `value` is the backend `CancellationType` member name, never the wording beside it:
          the handler parses this string as an enum. The sentence the person reads comes from
          the locale files via `label` and `description`.
        -->
        <UiStack gap="sm">
          <UiText as="span" size="sm" weight="medium">{{ $t('client.services.cancelType') }}</UiText>
          <UiRadio
            name="cancel-type"
            :value="CancellationType.EndOfBillingPeriod"
            :model-value="cancelType"
            :label="$t('client.services.cancelEndOfPeriod')"
            :description="$t('client.services.cancelEndOfPeriodDesc')"
            testid="service-cancel-type-end"
            @update:model-value="cancelType = CancellationType.EndOfBillingPeriod"
          />
          <UiRadio
            name="cancel-type"
            :value="CancellationType.Immediate"
            :model-value="cancelType"
            :label="$t('client.services.cancelImmediate')"
            :description="$t('client.services.cancelImmediateDesc')"
            testid="service-cancel-type-immediate"
            @update:model-value="cancelType = CancellationType.Immediate"
          />
        </UiStack>

        <UiTextarea
          v-model="cancelReason"
          :label="$t('client.services.cancelReason')"
          :placeholder="$t('client.services.cancelReasonPlaceholder')"
          :rows="4"
          testid="service-cancel-reason"
        />

        <template #actions>
          <UiButton type="submit" variant="danger" :loading="cancelSubmitting" data-testid="service-cancel-submit">
            <XCircle v-if="!cancelSubmitting" :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.services.cancelBtn') }}
          </UiButton>
        </template>
      </UiForm>
    </template>
  </UiCard>

  <UiConfirmDialog
    :open="showConfirm"
    :title="$t('client.services.cancelConfirmTitle')"
    :description="$t('client.services.cancelConfirmDesc')"
    variant="danger"
    :loading="cancelSubmitting"
    @confirm="submitCancellation"
    @cancel="showConfirm = false"
  />
</template>

<script setup lang="ts">
/**
 * ServiceCancelForm. Requests the cancellation of one service: when it takes effect, an
 * optional reason and a confirmation dialog before anything is sent. The request, its in-flight
 * flag and the API's refusal belong to the service management store.
 */
import { ref } from 'vue'
import { XCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { CancellationType } from '~/types/cancellationtype'

/** Props of ServiceCancelForm. */
interface Props {
  /** The id of the service to cancel. */
  serviceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The service management store: the request flags and the API's refusal sentence. */
const store = useServiceManagementStore()
/** Reactive state of the store this component renders. */
const { cancelSubmitting, cancelError, cancelDone } = storeToRefs(store)

/** Which cancellation the form will request; the default matches the pre-selected radio. */
const cancelType = ref<CancellationType>(CancellationType.EndOfBillingPeriod)

/** The reason the client gives. */
const cancelReason = ref('')

/** Whether the confirmation dialog is open. */
const showConfirm = ref(false)

/**
 * Posts the cancellation request the confirmation dialog just approved.
 *
 * @returns Resolves when the attempt has ended; the outcome is rendered from the store.
 */
const submitCancellation = async (): Promise<void> => {
  showConfirm.value = false
  await store.cancelService(props.serviceId, cancelType.value, cancelReason.value)
}
</script>
