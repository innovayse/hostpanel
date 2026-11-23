/**
 * Tests for the list page state: the delete question is staged before anything is sent, a
 * confirmed delete clears it, and a saved order reports its flag. The outcome of a delete or a reorder is a toast.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { useStorefrontApi } from '@/composables/apis/useStorefrontApi'
import { ApiPath } from '@/lib/apiPaths'
import { useStorefrontCrud } from '@/composables/useStorefrontCrud'
import { useStorefrontList } from '@/composables/useStorefrontList'
import type { StorefrontList } from '@/composables/useStorefrontList'
import { json, problem } from '../helpers/fetch'
import { createTestI18n } from '../helpers/i18n'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'

/** An entry as the tests use it. */
interface Widget {
  /** Entry ID. */
  id: number
  /** Display order. */
  sortOrder: number
}

/** The entry the tests delete. */
const widget: Widget = { id: 4, sortOrder: 0 }

/**
 * Builds the CRUD state over the `/admin/faqs` endpoint set.
 *
 * @returns The CRUD state.
 */
const makeCrud = () => useStorefrontCrud<Widget, unknown>(useStorefrontApi<Widget, unknown>(ApiPath.AdminFaqs))

/**
 * Builds the list state over the `/admin/faqs` endpoint set inside a throwaway component, so the i18n and the toast store
 * are real. The CRUD state is handed over as a store hands it: its refusal sentence as a plain value.
 *
 * @param crud - The CRUD state to wrap.
 * @returns The list state.
 */
const makeList = (crud = makeCrud()): StorefrontList<Widget> => {
  let list: StorefrontList<Widget> | undefined
  mount(defineComponent({
    setup: () => {
      list = useStorefrontList<Widget>({
        remove: crud.remove,
        updateOrder: crud.updateOrder,
        get actionError() {
          return crud.actionError.value
        },
      })
      return () => h('div')
    },
  }), { global: { plugins: [createTestI18n(), createPinia()] } })
  return list as StorefrontList<Widget>
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useStorefrontList', () => {
  it('stages a delete without sending anything and cancels it', () => {
    const mock = stubRouteTable({})
    const list = makeList()

    list.stageDelete(widget)
    expect(list.deleteTarget.value).toEqual(widget)
    list.cancelDelete()

    expect(list.deleteTarget.value).toBeNull()
    expect(mock).not.toHaveBeenCalled()
  })

  it('confirmDelete deletes the staged entry and closes the question', async () => {
    const mock = stubRouteTable({ 'DELETE /admin/faqs/4': () => new Response(null, { status: 204 }), 'GET /admin/faqs': () => json([]) })
    const list = makeList()

    list.stageDelete(widget)
    await list.confirmDelete()

    expect(requestsOf(mock)).toContain('DELETE /admin/faqs/4')
    expect(list.deleteTarget.value).toBeNull()
    expect(list.deleting.value).toBe(false)
    expect(toastMessages()).toEqual(['Record deleted.'])
  })

  it('a refused confirmDelete closes the question, toasts the sentence and reloads the list', async () => {
    const mock = stubRouteTable({ 'DELETE /admin/faqs/4': () => problem('Pinned.', 409), 'GET /admin/faqs': () => json([widget]) })
    const crud = makeCrud()
    const list = makeList(crud)

    list.stageDelete(widget)
    await list.confirmDelete()

    expect(crud.actionError.value).toBe('Pinned.')
    expect(toastMessages()).toEqual(['Pinned.'])
    expect(crud.items.value).toEqual([widget])
    expect(requestsOf(mock)).toEqual(['DELETE /admin/faqs/4', 'GET /admin/faqs'])
    expect(list.deleteTarget.value).toBeNull()
  })

  it('a refused saveOrder toasts the sentence, reloads the list and lowers the saving flag', async () => {
    const mock = stubRouteTable({ 'PUT /admin/faqs/order': () => problem('Locked.', 409), 'GET /admin/faqs': () => json([widget]) })
    const crud = makeCrud()
    const list = makeList(crud)

    await list.saveOrder([{ id: 4, sortOrder: 3 }])

    expect(crud.actionError.value).toBe('Locked.')
    expect(toastMessages()).toEqual(['Locked.'])
    expect(requestsOf(mock)).toContain('GET /admin/faqs')
    expect(list.savingOrder.value).toBe(false)
  })

  it('confirmDelete with nothing staged sends nothing', async () => {
    const mock = stubRouteTable({})

    await makeList().confirmDelete()

    expect(mock).not.toHaveBeenCalled()
  })

  it('saveOrder sends the pairs and lowers the saving flag', async () => {
    const mock = stubRouteTable({ 'PUT /admin/faqs/order': () => new Response(null, { status: 204 }), 'GET /admin/faqs': () => json([]) })
    const list = makeList()

    await list.saveOrder([{ id: 4, sortOrder: 0 }])

    expect(requestsOf(mock)).toContain('PUT /admin/faqs/order')
    expect(list.savingOrder.value).toBe(false)
    expect(toastMessages()).toEqual(['Order saved.'])
  })
})
