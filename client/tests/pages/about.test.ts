/**
 * Tests for the About page: the operator's story as one section, and a redirect to contact when no
 * story is set. The real story reader runs against the operator's settings from the stub server.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { useAboutText } from '~/composables/useAboutText'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import LegalDocument from '~/components/legal/LegalDocument.vue'
import Page from '~/pages/about.vue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useAboutText })

/**
 * Publishes the story the operator wrote, one blank line between paragraphs.
 *
 * @param paragraphs The paragraphs the operator set.
 * @returns Resolves once the settings are loaded.
 */
const publishStory = (paragraphs: string[]): Promise<void> =>
  loadPortalSettings(replies, { [PortalSettingKey.AboutText]: paragraphs.join('\n\n') })

/** `navigateTo` spy, renewed before each test. */
let navigateTo: ReturnType<typeof vi.fn>

describe('pages/about', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
    registerPageComponents({ LegalDocument })
    navigateTo = vi.fn()
    vi.stubGlobal('navigateTo', navigateTo)
  })

  it('shows the title and every paragraph of the story', async () => {
    await publishStory(['We host sites.', 'We register domains.'])

    const wrapper = await mountPage(Page)

    expect(wrapper.find('h1').text()).toBe('about.title')
    const story = wrapper.find('[data-testid="legal-section-story"]')
    expect(story.text()).toContain('We host sites.')
    expect(story.text()).toContain('We register domains.')
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('sends the visitor to contact when no story is set', async () => {
    await publishStory([])

    await mountPage(Page)

    expect(navigateTo).toHaveBeenCalledWith('/en/contact', { redirectCode: 302 })
  })
})
