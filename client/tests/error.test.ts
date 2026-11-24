/**
 * Tests for the error page: wording and icon per status, the generic fallback, the way home, and
 * the way back that falls home when there is no history.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import ErrorPage from '~/error.vue'

/** Spy for `navigateTo`, to read where the back button sends a visitor with no history. */
const navigateTo = vi.fn()

describe('error.vue', () => {
  beforeEach(() => {
    navigateTo.mockReset()
    vi.stubGlobal('navigateTo', navigateTo)
    vi.stubGlobal('useLocalePath', () => (path: string) => `/en${path}`)
    // Echoes the key, but honours the fallback `t(key, fallback)` takes for an unknown status.
    vi.stubGlobal('useI18n', () => ({
      t: (key: string, fallback?: string) => (/^error\.(403|404|500)\./.test(key) || fallback === undefined ? key : fallback),
    }))
  })

  it('shows the status code as the h1', () => {
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 404 } } })

    expect(wrapper.find('h1').text()).toBe('404')
  })

  it.each([403, 404, 500])('words a %s with its own title and message', (statusCode) => {
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode } } })

    expect(wrapper.find('[data-testid="error-title"]').text()).toBe(`error.${statusCode}.title`)
    expect(wrapper.find('[data-testid="error-message"]').text()).toBe(`error.${statusCode}.message`)
  })

  it('falls back to the generic title and to the error\'s own message for another status', () => {
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 418, message: 'Short and stout' } } })

    expect(wrapper.find('[data-testid="error-title"]').text()).toBe('error.default.title')
    expect(wrapper.find('[data-testid="error-message"]').text()).toBe('Short and stout')
  })

  it('falls back to the generic message when the error has none', () => {
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 418 } } })

    expect(wrapper.find('[data-testid="error-message"]').text()).toBe('error.default.message')
  })

  it('links home and to the contact page in the current language', () => {
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 500 } } })

    expect(wrapper.find('[data-testid="error-go-home"]').attributes('href')).toBe('/en/')
    expect(wrapper.find('[data-testid="error-go-home"]').text()).toBe('common.home')
    expect(wrapper.find('[data-testid="error-go-back"]').text()).toBe('common.back')
    expect(wrapper.find('[data-testid="error-contact"]').attributes('href')).toBe('/en/contact')
  })

  it('goes back in the browser history when there is some', async () => {
    const back = vi.spyOn(window.history, 'back').mockImplementation(() => undefined)
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(3)
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 404 } } })

    await wrapper.find('[data-testid="error-go-back"]').trigger('click')

    expect(back).toHaveBeenCalledTimes(1)
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('goes home when there is no history to go back to', async () => {
    vi.spyOn(window.history, 'length', 'get').mockReturnValue(1)
    const wrapper = mountWithKit(ErrorPage, { props: { error: { statusCode: 404 } } })

    await wrapper.find('[data-testid="error-go-back"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith('/en/')
  })
})
