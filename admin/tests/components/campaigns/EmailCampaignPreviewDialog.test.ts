/**
 * Unit tests for EmailCampaignPreviewDialog: the rendered subject is the title, the HTML goes into
 * the sandboxed frame, and closing is reported.
 */
import { describe, expect, it } from 'vitest'
import EmailCampaignPreviewDialog from '@/components/campaigns/EmailCampaignPreviewDialog.vue'
import { mountWithApp } from '../../helpers/mount'

/** A rendered mail. */
const preview = { subject: 'Welcome back', html: '<p>Hello</p>' }

describe('EmailCampaignPreviewDialog', () => {
  it('shows the subject as the title and the HTML in the frame', () => {
    const wrapper = mountWithApp(EmailCampaignPreviewDialog, { props: { preview } })
    const frame = wrapper.get('[data-testid="email-campaign-preview-frame"]')

    expect(wrapper.get('[data-testid="email-campaign-preview"]').text()).toContain('Welcome back')
    expect(frame.attributes('srcdoc')).toBe('<p>Hello</p>')
    expect(frame.attributes('title')).toBe('Preview of the mail')
  })

  it('reports the close button', async () => {
    const wrapper = mountWithApp(EmailCampaignPreviewDialog, { props: { preview } })

    await wrapper.get('[data-testid="dialog-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
