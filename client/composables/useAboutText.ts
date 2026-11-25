/**
 * The company story the operator publishes on the About page.
 *
 * The text is the operator's own, from the admin-managed `portal.about.text` settings (one per
 * language, the default one standing in for a language without its own) or the matching
 * environment variables. Nothing is written in the source: a self-hosted install tells its own
 * story, and an install that sets none has no About page.
 *
 * @module composables/useAboutText
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'

/** A blank line, possibly holding spaces, is where one paragraph ends and the next begins. */
const PARAGRAPH_BREAK = /\n\s*\n/

/**
 * Reads the published story for the active language.
 *
 * @returns The reactive list of paragraphs; empty when the operator set no story.
 */
export const useAboutText = () => {
  const { getLocalized } = useLocalizedSetting()

  /** The story in the active language, falling back to the default text. */
  const text = computed(() => getLocalized({ key: PortalSettingKey.AboutText, envKey: 'portalAboutText' }))

  /** The story split into its paragraphs, blank ones dropped. */
  const paragraphs = computed(() =>
    text.value.split(PARAGRAPH_BREAK).map(paragraph => paragraph.trim()).filter(paragraph => paragraph.length > 0))

  return { paragraphs }
}
