/**
 * Tests for DemoLink: hidden when no base URL is configured, shown with the correct href when
 * the base URL is set, and trims a trailing slash before joining the path.
 *
 * DemoLink is a VitePress component that reads theme data via useData(). vitepress is mocked so
 * the component can run in the vitest environment without a full VitePress build.
 */
import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'

vi.mock('vitepress', () => ({
  useData: vi.fn(),
}))

import { useData } from 'vitepress'
import DemoLink from '../../../../docs/.vitepress/theme/components/DemoLink.vue'

/**
 * Mounts DemoLink with the given theme config.
 *
 * @param demoAdminUrl - Base URL for the admin demo.
 * @param demoClientUrl - Base URL for the client demo.
 * @param path - Admin path prop.
 * @param clientPath - Client path prop.
 * @returns The wrapper.
 */
const mountDemoLink = (
  demoAdminUrl: string,
  demoClientUrl: string,
  path?: string,
  clientPath?: string,
) => {
  vi.mocked(useData).mockReturnValue({
    theme: ref({ demoAdminUrl, demoClientUrl }),
  } as ReturnType<typeof useData>)
  return mount(DemoLink, { props: { path, clientPath } })
}

describe('DemoLink', () => {
  it('renders nothing when both base URLs are empty', () => {
    const wrapper = mountDemoLink('', '', '/servers', '/client/services')
    expect(wrapper.find('a').exists()).toBe(false)
  })

  it('renders nothing when base URL is set but no path props are given', () => {
    const wrapper = mountDemoLink('https://demo.example.com', '', undefined, undefined)
    expect(wrapper.find('a').exists()).toBe(false)
  })

  it('shows the admin link with the correct href when base URL and path are set', () => {
    const wrapper = mountDemoLink('https://demo.example.com', '', '/servers')
    const link = wrapper.find('a')

    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://demo.example.com/servers')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('shows the client link with the correct href when base URL and clientPath are set', () => {
    const wrapper = mountDemoLink('', 'https://client.example.com', undefined, '/client/invoices')
    const link = wrapper.find('a')

    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://client.example.com/client/invoices')
  })

  it('trims a trailing slash from the base URL before joining the path', () => {
    const wrapper = mountDemoLink('https://demo.example.com/', '', '/servers')
    const link = wrapper.find('a')

    expect(link.attributes('href')).toBe('https://demo.example.com/servers')
  })

  it('shows both links when both base URLs and paths are set', () => {
    const wrapper = mountDemoLink('https://demo.example.com', 'https://client.example.com', '/servers', '/client/services')
    const links = wrapper.findAll('a')

    expect(links).toHaveLength(2)
    expect(links[0]!.attributes('href')).toBe('https://demo.example.com/servers')
    expect(links[1]!.attributes('href')).toBe('https://client.example.com/client/services')
  })
})
