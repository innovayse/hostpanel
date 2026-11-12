/**
 * Mount tests for OrderFraudPanel: the IP address, the score and the translated risk reasons render, the actions are
 * offered only while the order is held for review, and each action is reported to the screen.
 */
import { describe, expect, it } from 'vitest'
import OrderFraudPanel from '@/components/orders/OrderFraudPanel.vue'
import { FraudReasonCode } from '@/types/fraud'
import { OrderStatus } from '@/types/order'
import type { OrderDetail } from '@/types/order'
import { mountWithApp } from '../../helpers/mount'

/**
 * Builds a scored order.
 *
 * @param status - The order status.
 * @param reasons - The risk reasons.
 * @returns The order as the API sends it.
 */
const scored = (status: OrderStatus, reasons: FraudReasonCode[] = [FraudReasonCode.BlockedIp, FraudReasonCode.OrderVelocity]): OrderDetail =>
  ({ id: 8, status, ipAddress: '203.0.113.9', fraudScore: 82, fraudReasons: reasons }) as OrderDetail

describe('OrderFraudPanel', () => {
  it('shows the IP address, the score and each reason as a sentence', () => {
    const wrapper = mountWithApp(OrderFraudPanel, { props: { order: scored(OrderStatus.UnderReview), busy: false } })

    expect(wrapper.get('[data-testid="order-fraud-ip"]').text()).toBe('203.0.113.9')
    expect(wrapper.get('[data-testid="order-fraud-score"]').text()).toBe('82')
    expect(wrapper.get('[data-testid="order-fraud-reason-BlockedIp"]').text()).toBe('IP address is blocked')
    expect(wrapper.get('[data-testid="order-fraud-reason-OrderVelocity"]').text()).toBe('Too many orders from one IP address')
    expect(wrapper.find('[data-testid="order-fraud-held"]').exists()).toBe(true)
  })

  it('says so when no reason is recorded', () => {
    const wrapper = mountWithApp(OrderFraudPanel, { props: { order: scored(OrderStatus.UnderReview, []), busy: false } })

    expect(wrapper.get('[data-testid="order-fraud-no-reasons"]').text()).toBe('No reasons recorded.')
  })

  it('offers approve and reject only while the order is held for review', () => {
    const held = mountWithApp(OrderFraudPanel, { props: { order: scored(OrderStatus.UnderReview), busy: false } })
    const decided = mountWithApp(OrderFraudPanel, { props: { order: scored(OrderStatus.Fraud), busy: false } })

    expect(held.find('[data-testid="order-fraud-approve"]').exists()).toBe(true)
    expect(held.find('[data-testid="order-fraud-reject"]').exists()).toBe(true)
    expect(decided.find('[data-testid="order-fraud-approve"]').exists()).toBe(false)
    expect(decided.find('[data-testid="order-fraud-reject"]').exists()).toBe(false)
    expect(decided.find('[data-testid="order-fraud-held"]').exists()).toBe(false)
  })

  it('reports the chosen action and disables both while busy', async () => {
    const wrapper = mountWithApp(OrderFraudPanel, { props: { order: scored(OrderStatus.UnderReview), busy: false } })

    await wrapper.get('[data-testid="order-fraud-approve"]').trigger('click')
    await wrapper.get('[data-testid="order-fraud-reject"]').trigger('click')
    expect(wrapper.emitted('approve')).toHaveLength(1)
    expect(wrapper.emitted('reject')).toHaveLength(1)

    await wrapper.setProps({ busy: true })
    expect(wrapper.get('[data-testid="order-fraud-approve"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="order-fraud-reject"]').attributes('disabled')).toBeDefined()
  })
})
