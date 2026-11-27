/**
 * Unit tests for the admin i18n setup: the `lang` attribute of `<html>` follows the active locale, from startup and
 * on every change.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { MessageLanguage } from '@/locales/messages'

/** Loads a fresh copy of the i18n module, so its startup runs again against the current storage. */
const loadI18n = async () => {
  vi.resetModules()
  return import('@/i18n')
}

beforeEach(() => {
  localStorage.clear()
  document.documentElement.lang = ''
})

describe('i18n document language', () => {
  it('sets the html lang to the default locale at startup', async () => {
    await loadI18n()

    expect(document.documentElement.lang).toBe('en')
  })

  it('sets the html lang to the saved locale at startup', async () => {
    localStorage.setItem('admin.locale', 'hy')
    await loadI18n()

    expect(document.documentElement.lang).toBe('hy')
  })

  it('follows every locale switch', async () => {
    const { setLocale } = await loadI18n()

    setLocale(MessageLanguage.Ru)
    await nextTick()

    expect(document.documentElement.lang).toBe('ru')
  })
})
