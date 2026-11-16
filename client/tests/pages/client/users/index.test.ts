/**
 * Tests for `pages/client/users/index.vue`: it only forwards to the account page's users tab.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import UsersPage from '~/pages/client/users/index.vue'
import { clientAccountTabPath } from '~/constants/clientRoutes'
import { AccountTab } from '~/types/accounttab'

/** Stands in for Nuxt's `navigateTo`. */
const navigate = vi.fn()

describe('users entry page', () => {
  beforeEach(() => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
  })

  it('forwards to the users tab of the account page', async () => {
    await mountPage(UsersPage)

    expect(navigate).toHaveBeenCalledWith(clientAccountTabPath(AccountTab.Users), { replace: true })
  })
})
