/**
 * Plan change of the hosting service on screen: the plans it can move to, and the request to move
 * with its in-flight flag, the API's refusal sentence and the result. The only caller of the
 * plan change endpoints in `useClientApi`.
 *
 * @module stores/serviceplanchange
 */
import { defineStore } from 'pinia'
import { useClientApi } from '~/composables/apis/useClientApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import { ActionOutcome } from '~/types/actionoutcome'
import type { ServicePlanChangeOption } from '~/types/serviceplanchangeoption'
import type { ServicePlanChangeResult } from '~/types/serviceplanchangeresult'

/** The plan change store. */
export const useServicePlanChangeStore = defineStore('servicePlanChange', () => {
  /** The client endpoints. */
  const api = useClientApi()

  /** The plans the service can move to; empty when there is nowhere to move. */
  const options = ref<ServicePlanChangeOption[]>([])
  /** Whether the options are loading. */
  const loading = ref(false)
  /** The API's sentence when loading the options failed. */
  const loadError = ref('')
  /** Whether a request is in flight. */
  const submitting = ref(false)
  /** The API's sentence when the last request was refused. */
  const submitError = ref('')
  /** What the accepted request came to; null until one was accepted. */
  const result = ref<ServicePlanChangeResult | null>(null)

  /**
   * Loads the plans the service can move to.
   *
   * @param serviceId - Service id.
   * @returns Resolves when settled; a failure lands in {@link loadError}.
   */
  const load = async (serviceId: string): Promise<void> => {
    loading.value = true
    loadError.value = ''
    try {
      options.value = await api.fetchPlanChangeOptions(serviceId)
    } catch (e: unknown) {
      options.value = []
      loadError.value = apiErrorMessage(e)
    } finally {
      loading.value = false
    }
  }

  /**
   * Requests the move to another plan.
   *
   * @param serviceId - Service id.
   * @param toProductId - The product of the plan to move to.
   * @returns How the attempt ended; on failure {@link submitError} holds the sentence to show.
   */
  const submit = async (serviceId: string, toProductId: number): Promise<ActionOutcome> => {
    submitError.value = ''
    submitting.value = true
    try {
      result.value = await api.requestPlanChange(serviceId, toProductId)
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      submitError.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      submitting.value = false
    }
  }

  /** Clears everything, for a newly opened service. */
  const reset = (): void => {
    options.value = []
    loading.value = false
    loadError.value = ''
    submitting.value = false
    submitError.value = ''
    result.value = null
  }

  return { options, loading, loadError, submitting, submitError, result, load, submit, reset }
})
