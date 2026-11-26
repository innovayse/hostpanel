/**
 * The support working hours the operator publishes on the contact page.
 *
 * The hours and their time zone are the operator's own values, from the admin-managed
 * `portal.contact.*` settings or the matching environment variables. The hours can be written
 * per language; the time zone is one value for all. Nothing is written
 * in the source: a self-hosted install has its own hours and its own zone, and an install
 * that sets neither shows no row at all.
 *
 * @module composables/useContactHours
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'
import { timezoneOffsetLabel } from '~/utils/timezoneOffsetLabel'

/**
 * Reads the published working hours.
 *
 * @returns The reactive display line.
 */
export const useContactHours = () => {
  const { get } = usePortalSettings()
  const { getLocalized } = useLocalizedSetting()

  /**
   * Hours followed by the time zone's current offset (the zone is an IANA id; an old free-text value is shown as written).
   * Empty when the hours are empty, even if a time zone is set: a zone alone says nothing.
   */
  const workingHours = computed(() => {
    const hours = getLocalized({ key: PortalSettingKey.ContactHours, envKey: 'portalContactHours' })
    if (!hours) {
      return ''
    }

    const timezone = get(PortalSettingKey.ContactTimezone, 'portalContactTimezone')
    return timezone ? `${hours} ${timezoneOffsetLabel(timezone)}` : hours
  })

  return { workingHours }
}
