/**
 * Tests for `useTemplate`: the operator's template choice resolves a slot loader from the
 * registry, and an unknown name falls back to the default template.
 *
 * @module tests/composables/useTemplate.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { templates } from '~/templates/registry'
import { DEFAULT_TEMPLATE } from '~/templates/types'
import { TemplateName } from '~/types/templatename'
import { TemplateSlot } from '~/types/templateslot'
import { useTemplate } from '~/composables/useTemplate'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** `defineAsyncComponent` is a Vue auto-import, so it is stubbed to record the loader it was given. */
beforeEach(() => {
  vi.stubGlobal('defineAsyncComponent', (loader: unknown) => ({ loader }))
})

/**
 * Makes the operator's `portal.template` setting read as one value.
 *
 * @param template The `portal.template` setting value.
 * @returns Resolves once the settings are loaded.
 */
const chooseTemplate = (template: string): Promise<void> => loadPortalSettings(replies, { [PortalSettingKey.Template]: template })

describe('useTemplate', () => {
  it('names the template the operator chose', async () => {
    await chooseTemplate(TemplateName.Nova)

    expect(useTemplate().name.value).toBe(TemplateName.Nova)
  })

  it.each(['', 'no-such-template'])('falls back to the default for %j', async (value) => {
    await chooseTemplate(value)

    expect(useTemplate().name.value).toBe(DEFAULT_TEMPLATE)
  })

  it('resolves a slot through the active template loader', async () => {
    await chooseTemplate(TemplateName.Nova)

    const component = useTemplate().slot(TemplateSlot.Header) as unknown as { loader: unknown }

    expect(component.loader).toBe(templates[TemplateName.Nova][TemplateSlot.Header])
  })

  it('resolves slots of the default template when the name is unknown', async () => {
    await chooseTemplate('typo')

    const component = useTemplate().slot(TemplateSlot.Home) as unknown as { loader: unknown }

    expect(component.loader).toBe(templates[DEFAULT_TEMPLATE][TemplateSlot.Home])
  })
})
