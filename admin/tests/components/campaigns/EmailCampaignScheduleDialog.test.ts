/**
 * Unit tests for EmailCampaignScheduleDialog: the date-time picker library is replaced by a stub,
 * so the test picks a moment the way the picker reports it. Scheduling needs a moment and reports
 * it; closing is reported.
 */
import { describe, expect, it, vi } from 'vitest'
import EmailCampaignScheduleDialog from '@/components/campaigns/EmailCampaignScheduleDialog.vue'
import { mountWithApp } from '../../helpers/mount'

vi.mock('@vuepic/vue-datepicker', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    VueDatePicker: defineComponent({
      name: 'VueDatePicker',
      props: ['modelValue', 'placeholder', 'minDate'],
      emits: ['update:modelValue'],
      setup: (props, { emit }) => () =>
        h('button', {
          'data-testid': 'email-campaign-schedule-at',
          'data-placeholder': props.placeholder,
          onClick: () => emit('update:modelValue', new Date('2030-05-06T07:08:00.000Z')),
        }),
    }),
  }
})

describe('EmailCampaignScheduleDialog', () => {
  it('cannot be confirmed until a moment is picked, then reports it', async () => {
    const wrapper = mountWithApp(EmailCampaignScheduleDialog, { props: { loading: false } })
    const confirm = wrapper.get('[data-testid="email-campaign-schedule-confirm"]')
    expect(confirm.attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="email-campaign-schedule-at"] button').attributes('data-placeholder')).toBe('Choose date and time')

    await wrapper.get('[data-testid="email-campaign-schedule-at"] button').trigger('click')
    expect(confirm.attributes('disabled')).toBeUndefined()
    await confirm.trigger('click')

    expect(wrapper.emitted('confirm')?.[0]).toEqual([new Date('2030-05-06T07:08:00.000Z')])
  })

  it('reports the cancel button and the close button', async () => {
    const wrapper = mountWithApp(EmailCampaignScheduleDialog, { props: { loading: false } })

    await wrapper.get('[data-testid="email-campaign-schedule-cancel"]').trigger('click')
    await wrapper.get('[data-testid="dialog-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(2)
  })
})
