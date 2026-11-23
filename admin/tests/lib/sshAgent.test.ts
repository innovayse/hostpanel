/**
 * Unit tests for the SSH key agent setup helpers: the one-command installer verifies the hash
 * before unpacking, quotes every value and refuses to build a command it cannot make safe; the
 * firewall and sshd lines carry one rule per HostPanel address and firewall, scoped to the agent
 * port, and nothing for an unusable address.
 */
import { describe, expect, it } from 'vitest'
import { buildInstallCommand, buildNetworkLines, shellQuote } from '@/lib/sshAgent'
import type { ServerSshSetupDto } from '@/types/serverssh'
import { SSH_AGENT_PROFILE } from '../helpers/serverFixtures'

/** A complete setup. */
const SETUP: ServerSshSetupDto = {
  serverId: 4,
  profile: SSH_AGENT_PROFILE,
  publicKey: 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b hostpanel-server-4',
  binding: '11111111-1111-4111-8111-111111111111',
  agentUsername: 'hpsshkeys',
  bundleVersion: '1.1.0',
  bundleSha256: 'a'.repeat(64),
  bundlePath: '/api/public/ssh-agent/bundle.tar.gz',
  sourceAddresses: [],
}

describe('buildInstallCommand', () => {
  it('downloads from this HostPanel, checks the hash, then installs with profile, binding and key', () => {
    const command = buildInstallCommand('https://panel.example.com/', SETUP) ?? ''

    // One subshell: set -e and the cleanup trap must not leak into the admin's root shell.
    expect(command.startsWith('(set -e; ')).toBe(true)
    expect(command.endsWith(')')).toBe(true)
    const steps = command.slice(1, -1).split('; ')
    expect(steps[2]).toBe(`trap 'rm -rf "$D"' EXIT`)
    expect(steps[3]).toBe(`curl -fsSL --proto '=https' -o "$D/agent.tgz" 'https://panel.example.com/api/public/ssh-agent/bundle.tar.gz'`)
    expect(steps[4]).toBe(`echo "${'a'.repeat(64)}  $D/agent.tgz" | sha256sum -c -`)
    expect(steps.findIndex((s) => s.includes('sha256sum'))).toBeLessThan(steps.findIndex((s) => s.startsWith('tar ')))
    expect(steps[6]).toBe(`"$D/install.sh" --provider '${SSH_AGENT_PROFILE}' --binding '${SETUP.binding}' '${SETUP.publicKey}'`)
    expect(command).not.toContain('PRIVATE')
  })

  it('keeps the hash check but drops the https-only flag for a plain-http lab', () => {
    const command = buildInstallCommand('http://localhost:5148', SETUP) ?? ''

    expect(command).toContain('sha256sum -c -')
    expect(command).not.toContain('--proto')
  })

  it('builds nothing without a profile, an identity or a real hash', () => {
    expect(buildInstallCommand('https://p', { ...SETUP, profile: null })).toBeNull()
    expect(buildInstallCommand('https://p', { ...SETUP, publicKey: null })).toBeNull()
    expect(buildInstallCommand('https://p', { ...SETUP, binding: null })).toBeNull()
    expect(buildInstallCommand('https://p', { ...SETUP, bundleSha256: 'abc; rm -rf /' })).toBeNull()
  })
})

describe('shellQuote', () => {
  it('neutralises embedded quotes and shell syntax', () => {
    expect(shellQuote(`x' ; rm -rf / ; echo '`)).toBe(`'x'\\'' ; rm -rf / ; echo '\\'''`)
  })
})

describe('buildNetworkLines', () => {
  it('allows only HostPanel on the agent port, per firewall, and exempts it from sshd penalties', () => {
    const lines = buildNetworkLines(['203.0.113.10'], 2222)

    expect(lines?.csf).toBe(`csf -a 203.0.113.10 'HostPanel SSH key agent'`)
    expect(lines?.firewalld).toBe(
      `firewall-cmd --permanent --add-rich-rule='rule family="ipv4" source address="203.0.113.10" port port="2222" protocol="tcp" accept' && firewall-cmd --reload`,
    )
    expect(lines?.ufw).toBe(`ufw allow from 203.0.113.10 to any port 2222 proto tcp`)
    expect(lines?.sshd).toBe('PerSourcePenaltyExemptList 203.0.113.10')
  })

  it('writes one rule per address, with the IPv6 family where needed', () => {
    const lines = buildNetworkLines(['203.0.113.10', '2001:db8::10'], 22)

    expect(lines?.ufw.split('\n')).toEqual([
      'ufw allow from 203.0.113.10 to any port 22 proto tcp',
      'ufw allow from 2001:db8::10 to any port 22 proto tcp',
    ])
    expect(lines?.firewalld).toContain('rule family="ipv6" source address="2001:db8::10"')
    expect(lines?.sshd).toBe('PerSourcePenaltyExemptList 203.0.113.10,2001:db8::10')
  })

  it('builds nothing without an address, and never passes shell syntax through', () => {
    expect(buildNetworkLines([], 22)).toBeNull()
    expect(buildNetworkLines(['1.2.3.4; rm -rf /'], 22)).toBeNull()
    expect(buildNetworkLines(['203.0.113.10'], 0)).toBeNull()
  })
})
