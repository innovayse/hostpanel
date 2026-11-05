/**
 * Regression test for the admin SPA's nginx config (`docker/nginx/admin.conf`): a route that
 * shares its name with a folder in `public/` must still fall through to the SPA.
 *
 * nginx cannot be run here, so the test reads the config and checks the one directive that matters:
 * a `try_files` that lists `$uri/` answers a directory hit (`/integrations`, the folder of integration
 * logos) with a 301 to the container's own host and port, so reloading or deep-linking that page
 * sent the browser to an address that does not exist.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The admin root: the folder above `tests/`. */
const ADMIN_ROOT = join(__dirname, '..', '..')

/** The nginx config the admin image ships. */
const CONFIG = readFileSync(join(ADMIN_ROOT, '..', 'docker', 'nginx', 'admin.conf'), 'utf8')

describe('docker/nginx/admin.conf', () => {
  it('falls back to the SPA without trying directories, which nginx answers with a redirect', () => {
    const tryFiles = CONFIG.split('\n').find(line => line.trim().startsWith('try_files'))

    expect(tryFiles?.trim()).toBe('try_files $uri /index.html;')
  })

  it('has a public/ folder named like an SPA route, which is why directories must not be tried', () => {
    const publicFolders = readdirSync(join(ADMIN_ROOT, 'public'), { withFileTypes: true }).filter(entry => entry.isDirectory()).map(entry => entry.name)

    expect(publicFolders).toContain('integrations')
    expect(existsSync(join(ADMIN_ROOT, 'src', 'views', 'IntegrationsView.vue'))).toBe(true)
  })

  it('compresses the text bundle, also for requests that arrive through the edge proxy', () => {
    expect(CONFIG).toMatch(/^\s*gzip on;$/m)
    expect(CONFIG).toMatch(/^\s*gzip_proxied any;$/m)
    expect(CONFIG).toMatch(/gzip_types[^;]*application\/javascript/)
  })

  it('lets the browser keep content-hashed assets for good and revalidate the shell', () => {
    const assets = CONFIG.slice(CONFIG.indexOf('location /assets/'))
    const shell = CONFIG.slice(CONFIG.indexOf('location = /index.html'))

    expect(assets).toContain('Cache-Control "public, max-age=31536000, immutable"')
    expect(shell).toContain('Cache-Control "no-cache"')
  })

  it('revalidates the theme pre-paint script, which keeps its name across releases, under the same policy as the shell', () => {
    const start = CONFIG.indexOf('location = /theme-boot.js')
    const block = CONFIG.slice(start, CONFIG.indexOf('}', start))

    expect(start).toBeGreaterThan(-1)
    expect(block).toContain('snippets/admin-csp.conf')
    expect(block).toContain('Cache-Control "no-cache"')
  })

  it('answers a missing asset with a 404, never with the HTML shell', () => {
    const assets = CONFIG.slice(CONFIG.indexOf('location /assets/'), CONFIG.indexOf('}', CONFIG.indexOf('location /assets/')))

    expect(assets).not.toContain('try_files')
  })
})
