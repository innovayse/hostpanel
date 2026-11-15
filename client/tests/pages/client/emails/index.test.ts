/**
 * Tests for `pages/client/emails/index.vue`: it only forwards to the account page's emails tab.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import EmailsPage from '~/pages/client/emails/index.vue'
import { clientAccountTabPath } from '~/constants/clientRoutes'
import { AccountTab } from '~/types/accounttab'

/** Stands in for Nuxt's `navigateTo`. */
const navigate = vi.fn()

describe('emails entry page', () => {
  beforeEach(() => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('forwards to the emails tab of the account page', async () => {
    await mountPage(EmailsPage)

    expect(navigate).toHaveBeenCalledWith(clientAccountTabPath(AccountTab.Emails), { replace: true })
  })
})
