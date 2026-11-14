/**
 * Users list flow: the search box is sent to the API as it is typed (after a pause) and kept in the address, the page
 * is kept in the address and sent with the same search, a link with a search opens the narrowed list, and the list has
 * no search button of its own.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { UserListItem } from '../src/types/useraccount'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/**
 * Builds a user of the list.
 *
 * @param id - The user id.
 * @param firstName - Given name.
 * @param email - E-mail address.
 * @returns The list row.
 */
const userOf = (id: string, firstName: string, email: string): UserListItem => ({
  id,
  clientId: null,
  firstName,
  lastName: 'Tester',
  email,
  language: null,
  lastLoginAt: null,
  createdAt: '2026-01-01T00:00:00Z',
})

/** Every user the stubbed list holds. */
const allUsers: UserListItem[] = [userOf('u1', 'Ada', 'ada@example.test'), userOf('u2', 'Grace', 'grace@example.test')]

/**
 * Stubs the user list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubUserList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/admin/users',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allUsers.filter((user) => user.email.includes(term))
          const body: PaginatedList<UserListItem> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
    ],
  })

test('searching sends the search with the page and shows only the matching user', async ({ page }) => {
  const api = await stubUserList(page)

  await page.goto('/clients/users')
  await expect(page.getByTestId('users-row-u1')).toBeVisible()
  await expect(page.getByTestId('users-row-u2')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/admin/users')[0]?.search).toBe('?page=1&pageSize=20')

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'users-search').fill('grace')
  await expect(page).toHaveURL(/search=grace/)

  await expect(page.getByTestId('users-row-u2')).toBeVisible()
  await expect(page.getByTestId('users-row-u1')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/users').at(-1)?.search).toBe('?page=1&pageSize=20&search=grace')
  await expect(page.getByTestId('users-search-submit')).toHaveCount(0)
})

test('a list link with a search opens the narrowed list and keeps it in the box', async ({ page }) => {
  const api = await stubUserList(page)

  await page.goto('/clients/users?search=ada')

  await expect(page.getByTestId('users-row-u1')).toBeVisible()
  await expect(page.getByTestId('users-row-u2')).toHaveCount(0)
  await expect(fieldOf(page, 'users-search')).toHaveValue('ada')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/users')[0]?.search).toBe('?page=1&pageSize=20&search=ada')
})

test('a list link with a page opens that page', async ({ page }) => {
  const api = await stubUserList(page)

  await page.goto('/clients/users?page=2')

  await expect(page.getByTestId('users-row-u1')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/admin/users')[0]?.search).toBe('?page=2&pageSize=20')
})
