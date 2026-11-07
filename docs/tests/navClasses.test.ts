/**
 * Unit tests for the navigation bar classes written into the pre-rendered pages: a page with a sidebar
 * gets `has-sidebar`, the home page gets `home top`, and anything else is left alone.
 */
import { describe, expect, it } from 'vitest'
import { navBarClassesFor, withNavBarClasses } from '../.vitepress/navClasses'

/** A page as VitePress renders it before hydration, with the content classes given. */
const page = (contentClasses: string): string =>
  `<div class="VPNav"><div class="VPNavBar" data-v-1><div class="VPNavBarTitle has-sidebar"></div></div></div><div class="VPContent${contentClasses}" id="VPContent">`

describe('navBarClassesFor', () => {
  it('returns has-sidebar for a page with a sidebar', () => {
    expect(navBarClassesFor(' has-sidebar')).toBe(' has-sidebar')
  })

  it('returns home and top for the home page', () => {
    expect(navBarClassesFor(' is-home')).toBe(' home top')
  })

  it('returns nothing for a page with neither', () => {
    expect(navBarClassesFor('')).toBe('')
  })
})

describe('withNavBarClasses', () => {
  it('puts has-sidebar on the bar of a page with a sidebar', () => {
    const html = withNavBarClasses(page(' has-sidebar'))

    expect(html).toContain('<div class="VPNavBar has-sidebar" data-v-1>')
    expect(html).toContain('<div class="VPNavBarTitle has-sidebar">')
  })

  it('puts home and top on the bar of the home page', () => {
    expect(withNavBarClasses(page(' is-home'))).toContain('<div class="VPNavBar home top" data-v-1>')
  })

  it('leaves a page without content classes unchanged', () => {
    const html = page('')

    expect(withNavBarClasses(html)).toBe(html)
  })

  it('leaves a document without the content element unchanged', () => {
    const html = '<div class="VPNavBar"></div>'

    expect(withNavBarClasses(html)).toBe(html)
  })

  it('changes only the first bar', () => {
    const html = withNavBarClasses(`${page(' has-sidebar')}<div class="VPNavBar">`)

    expect(html.match(/VPNavBar has-sidebar/g)).toHaveLength(1)
  })
})
