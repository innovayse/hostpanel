import { afterEach, describe, expect, it } from 'vitest'
import { getRuntimeConfig, normalizeBasePath, normalizeOrigin, publicAssetUrl, storefrontAssetUrl } from '@/lib/runtimeConfig'

/** Runtime config reader: normalisation, absent config and asset URLs. */
describe('runtimeConfig', () => {
  afterEach(() => {
    delete window.__APP_CONFIG__
  })

  it('normalises empty, slashless and trailing-slash paths', () => {
    expect(normalizeBasePath(undefined)).toBe('/')
    expect(normalizeBasePath('')).toBe('/')
    expect(normalizeBasePath('/')).toBe('/')
    expect(normalizeBasePath('admin')).toBe('/admin')
    expect(normalizeBasePath('/admin/')).toBe('/admin')
  })

  it('falls back to the root base when config.js is absent', () => {
    expect(getRuntimeConfig().basePath).toBe('/')
  })

  it('reads the base path injected by config.js', () => {
    window.__APP_CONFIG__ = { basePath: '/admin/' }
    expect(getRuntimeConfig().basePath).toBe('/admin')
  })

  it('builds public asset URLs under the base path', () => {
    expect(publicAssetUrl('integrations/a.svg')).toBe('/integrations/a.svg')
    window.__APP_CONFIG__ = { basePath: '/admin' }
    expect(publicAssetUrl('integrations/a.svg')).toBe('/admin/integrations/a.svg')
  })

  it('normalises the storefront origin', () => {
    expect(normalizeOrigin(undefined)).toBe('')
    expect(normalizeOrigin(' https://host.example.com/ ')).toBe('https://host.example.com')
    window.__APP_CONFIG__ = { clientBaseUrl: 'https://host.example.com/' }
    expect(getRuntimeConfig().clientBaseUrl).toBe('https://host.example.com')
  })

  it('resolves a stored root-relative image against the storefront origin', () => {
    window.__APP_CONFIG__ = { clientBaseUrl: 'https://host.example.com' }
    expect(storefrontAssetUrl('/demo/partners/a.svg')).toBe('https://host.example.com/demo/partners/a.svg')
  })

  it('keeps an operator upload on the admin origin, which proxies it to the API', () => {
    window.__APP_CONFIG__ = { clientBaseUrl: 'https://host.example.com' }
    expect(storefrontAssetUrl('/uploads/slides/a.png')).toBe('/uploads/slides/a.png')
  })

  it('leaves absolute, protocol-relative, data and empty URLs alone', () => {
    window.__APP_CONFIG__ = { clientBaseUrl: 'https://host.example.com' }
    expect(storefrontAssetUrl('https://cdn.example.com/a.png')).toBe('https://cdn.example.com/a.png')
    expect(storefrontAssetUrl('//cdn.example.com/a.png')).toBe('//cdn.example.com/a.png')
    expect(storefrontAssetUrl('data:image/png;base64,AAAA')).toBe('data:image/png;base64,AAAA')
    expect(storefrontAssetUrl('')).toBe('')
  })

  it('keeps a relative path as it is when no storefront origin is configured', () => {
    expect(storefrontAssetUrl('/demo/partners/a.svg')).toBe('/demo/partners/a.svg')
  })
})
