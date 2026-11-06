/**
 * Pinia store for the signed-in client's credit: the balance and its history (`pages/client/credit`), raising the top-up invoice that
 * adds funds (`pages/client/credit/add.vue`), and paying an invoice from the balance (the apply-credit card on the invoice page).
 *
 * Owns the loaded credit, the in-flight flags and the refusal sentences, so the pages render them and never wrap an API call in their
 * own try/catch. URLs and transport belong to {@link useBillingApi}; the amounts, the limits and the currency are the backend's.
 *
 * @module stores/credit
 */

import { defineStore } from 'pinia'
import { useBillingApi } from '~/composables/apis/useBillingApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import type { ClientCredit } from '~/types/clientcredit'

/** How many history lines one page of the table shows. */
export const CREDIT_PAGE_SIZE = 10

/**
 * Credit state for the client area.
 *
 * @returns The credit state and the actions that change it.
 */
export const useCreditStore = defineStore('credit', () => {
  const billing = useBillingApi()

  /** The credit on screen; null until {@link load} or after a failed read. */
  const credit = ref<ClientCredit | null>(null)

  /** Whether the credit is being read. */
  const loading = ref(false)

  /** The sentence for the last failed read; empty when there is none to show. */
  const error = ref('')

  /** The history page on screen, 1-based. */
  const page = ref(1)

  /** The history page the last read asked for, 1-based; it is the one to ask again after a failed read. */
  const requestedPage = ref(1)

  /** Whether a top-up invoice is being raised. */
  const topUpBusy = ref(false)

  /** The sentence for the last refused top-up; empty when there is none to show. */
  const topUpError = ref('')

  /** Whether credit is being applied to an invoice. */
  const applyBusy = ref(false)

  /** The sentence for the last refused application of credit; empty when there is none to show. */
  const applyError = ref('')

  /** The balance, zero before the credit is read. */
  const balance = computed<number>(() => credit.value?.balance ?? 0)

  /** Whether there is credit to spend. */
  const hasCredit = computed<boolean>(() => balance.value > 0)

  /**
   * Reads the credit and one page of its history.
   *
   * @param nextPage - 1-based history page; the page already on screen when absent.
   * @returns The credit; null when it could not be read ({@link error} holds the sentence).
   */
  const load = async (nextPage: number = page.value): Promise<ClientCredit | null> => {
    loading.value = true
    error.value = ''
    requestedPage.value = nextPage
    try {
      credit.value = await billing.fetchCredit(nextPage, CREDIT_PAGE_SIZE)
      page.value = nextPage
    } catch (e: unknown) {
      credit.value = null
      error.value = apiErrorMessage(e)
    } finally {
      loading.value = false
    }
    return credit.value
  }

  /**
   * Raises the top-up invoice for an amount. The balance does not move yet: the client pays the invoice, and the balance rises
   * once it is paid.
   *
   * @param amount - The amount to add, in the client's billing currency.
   * @returns The id of the new invoice; null when the backend refused ({@link topUpError} holds the sentence).
   */
  const createTopUp = async (amount: number): Promise<number | null> => {
    topUpBusy.value = true
    topUpError.value = ''
    try {
      return await billing.createCreditTopUp(amount)
    } catch (e: unknown) {
      topUpError.value = apiErrorMessage(e)
      return null
    } finally {
      topUpBusy.value = false
    }
  }

  /**
   * Pays part or all of an invoice from the balance, then reads the credit again so the balance on screen is the new one.
   *
   * @param invoiceId - Invoice to apply credit to.
   * @param amount - The credit to apply, in the invoice currency.
   * @returns Whether the backend applied it ({@link applyError} holds the sentence when not).
   */
  const applyToInvoice = async (invoiceId: string | number, amount: number): Promise<boolean> => {
    applyBusy.value = true
    applyError.value = ''
    try {
      await billing.applyInvoiceCredit(invoiceId, amount)
    } catch (e: unknown) {
      applyError.value = apiErrorMessage(e)
      return false
    } finally {
      applyBusy.value = false
    }
    await load(1)
    return true
  }

  return {
    credit,
    loading,
    error,
    page,
    requestedPage,
    topUpBusy,
    topUpError,
    applyBusy,
    applyError,
    balance,
    hasCredit,
    load,
    createTopUp,
    applyToInvoice
  }
})
