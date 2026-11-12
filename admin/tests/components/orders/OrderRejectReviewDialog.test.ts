/**
 * Mount tests for OrderRejectReviewDialog: an empty reason is refused with its sentence and nothing is sent, a typed reason is
 * emitted trimmed, and the prompt closes on cancel but not while the reject runs.
 */
import { describe, expect, it } from 'vitest'
import OrderRejectReviewDialog from '@/components/orders/OrderRejectReviewDialog.vue'
import { mountWithApp } from '../../helpers/mount'

describe('OrderRejectReviewDialog', () => {
  it('refuses an empty reason with its sentence and sends nothing', async () => {
    const wrapper = mountWithApp(OrderRejectReviewDialog, { props: { loading: false } })

    await wrapper.get('[data-testid="order-reject-reason"]').setValue('   ')
    await wrapper.get('[data-testid="order-reject-submit"]').trigger('click')

    expect(wrapper.get('[data-testid="order-reject-reason-error"]').text()).toContain('Enter a reason.')
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits the typed reason trimmed', async () => {
    const wrapper = mountWithApp(OrderRejectReviewDialog, { props: { loading: false } })

    await wrapper.get('[data-testid="order-reject-reason"]').setValue('  Stolen card  ')
    await wrapper.get('[data-testid="order-reject-submit"]').trigger('click')

    expect(wrapper.emitted('submit')).toEqual([['Stolen card']])
  })

  it('closes on cancel, and stays open while the reject runs', async () => {
    const idle = mountWithApp(OrderRejectReviewDialog, { props: { loading: false } })
    await idle.get('[data-testid="order-reject-cancel"]').trigger('click')
    expect(idle.emitted('close')).toHaveLength(1)

    const busy = mountWithApp(OrderRejectReviewDialog, { props: { loading: true } })
    await busy.get('[data-testid="order-reject-reason"]').setValue('Stolen card')
    await busy.get('[data-testid="order-reject-cancel"]').trigger('click')
    await busy.get('[data-testid="order-reject-submit"]').trigger('click')
    expect(busy.emitted('close')).toBeUndefined()
    expect(busy.emitted('submit')).toBeUndefined()
  })
})
