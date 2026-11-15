/**
 * Tests for `pages/client/profile.vue`: it only forwards to the account page's details tab.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import ProfilePage from '~/pages/client/profile.vue'
import { clientAccountTabPath } from '~/constants/clientRoutes'
import { AccountTab } from '~/types/accounttab'

/** Stands in for Nuxt's `navigateTo`. */
const navigate = vi.fn()

describe('profile entry page', () => {
  beforeEach(() => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('forwards to the details tab of the account page', async () => {
    await mountPage(ProfilePage)

    expect(navigate).toHaveBeenCalledWith(clientAccountTabPath(AccountTab.Details), { replace: true })
  })
})
