/**
 * Regression test for the admin image's start script (`docker/nginx/admin-entrypoint.sh`): the two optional
 * addresses it validates, `CLIENT_BASE_URL` and `DOCS_URL`, must accept an empty value.
 *
 * The check used to pipe `printf '%s' "$VALUE"` into `grep`. For an empty value printf writes no line, grep
 * sees no input and reports no match, so the container refused to start with "DOCS_URL must be empty or an
 * http(s) address" and crash looped. The script cannot run to the end here (it writes under
 * `/usr/share/nginx/html`), so the tests read only what it says about its inputs: the validation runs first
 * and exits with status 1 and a named message, anything after it fails differently or not at all.
 */
import { spawnSync } from 'node:child_process'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The start script the admin image ships. */
const SCRIPT = join(__dirname, '..', '..', '..', 'docker', 'nginx', 'admin-entrypoint.sh')

/**
 * Runs the start script with the given variables and nothing else from the caller's environment. Both optional
 * addresses default to a valid value so a test about one of them is not decided by the other, which the script checks
 * first or last; a variable passed as `undefined` is left unset.
 *
 * @param env - The variables the script reads, overriding the defaults.
 * @returns The exit status and what the script wrote to stderr.
 */
const runEntrypoint = (env: Record<string, string | undefined>): { status: number | null, stderr: string } => {
  const merged: Record<string, string | undefined> = {
    PATH: process.env.PATH,
    ADMIN_BASE_URL_PATH: '/admin',
    CLIENT_BASE_URL: 'https://host.example.com',
    DOCS_URL: 'https://innovayse.github.io/hostpanel',
    ...env
  }
  const result = spawnSync('sh', [SCRIPT], {
    env: Object.fromEntries(Object.entries(merged).filter(([, value]) => value !== undefined)),
    encoding: 'utf8'
  })

  return { status: result.status, stderr: result.stderr }
}

describe('docker/nginx/admin-entrypoint.sh', () => {
  it('accepts an empty DOCS_URL, which hides the documentation links', () => {
    expect(runEntrypoint({ DOCS_URL: '' }).stderr).not.toContain('DOCS_URL must be')
  })

  it('accepts a DOCS_URL that is not set at all', () => {
    expect(runEntrypoint({ DOCS_URL: undefined }).stderr).not.toContain('DOCS_URL must be')
  })

  it('accepts an empty CLIENT_BASE_URL, which leaves root-relative image paths relative', () => {
    expect(runEntrypoint({ CLIENT_BASE_URL: '' }).stderr).not.toContain('CLIENT_BASE_URL must be')
  })

  it('accepts an http(s) address for both', () => {
    const { stderr } = runEntrypoint({ DOCS_URL: 'https://innovayse.github.io/hostpanel/', CLIENT_BASE_URL: 'https://host.example.com' })

    expect(stderr).not.toContain('DOCS_URL must be')
    expect(stderr).not.toContain('CLIENT_BASE_URL must be')
  })

  it('still refuses a DOCS_URL that is not an http(s) address', () => {
    const { status, stderr } = runEntrypoint({ DOCS_URL: 'javascript:alert(1)' })

    expect(status).toBe(1)
    expect(stderr).toContain('DOCS_URL must be empty or an http(s) address')
  })

  it('still refuses a CLIENT_BASE_URL that is not an http(s) origin', () => {
    const { status, stderr } = runEntrypoint({ CLIENT_BASE_URL: 'ftp://host.example.com' })

    expect(status).toBe(1)
    expect(stderr).toContain('CLIENT_BASE_URL must be empty or an http(s) origin')
  })
})
