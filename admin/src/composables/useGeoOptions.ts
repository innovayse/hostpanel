/** Country and phone-code options built from the geo atlas, shared by the client forms. */
import { computed } from 'vue'
import CountriesData from '@innovayse/geo-atlas/lite'
import { translate } from '@/lib/translate'

/**
 * Provides geo-atlas based country and phone code options for UiSelect dropdowns.
 * Eliminates duplication across AddClientView, ClientProfileView, and ContactFormModal.
 *
 * @returns Country options, phone code options, and phone code resolver.
 */
export const useGeoOptions = () => {
  /** All countries from geo-atlas. The `/lite` entry carries only the country list; the root entry would bundle the multi-megabyte per-country state and city data this app never reads. */
  const geoCountries = CountriesData.getCountries()

  /** Country options for the UiSelect dropdown, sourced from geo-atlas. */
  const countryOptions = computed<{ value: string; label: string; displayLabel: string }[]>(() =>
    [{ value: '', label: translate('ui.selectCountry'), displayLabel: translate('ui.selectCountry') },
     ...geoCountries.filter(c => c.iso2).map(c => ({
       value: String(c.iso2),
       label: `${c.emoji ?? ''} ${c.name}`,
       displayLabel: `${c.emoji ?? ''} ${c.name}`,
     }))]
  )

  /** Phone code options keyed by ISO2 for uniqueness; the search also matches the native name and the ISO code. */
  const phoneCodeOptions = computed<{ value: string; label: string; displayLabel: string; keywords: string }[]>(() =>
    geoCountries
      .filter(c => c.phone && c.name && c.iso2)
      .map(c => ({
        value: String(c.iso2),
        label: `${c.emoji ?? ''} +${c.phone}`,
        displayLabel: `${c.emoji ?? ''} ${c.name} (+${c.phone})`,
        keywords: [c.native, c.iso2].filter(Boolean).join(' '),
      }))
      .sort((a, b) => a.displayLabel.localeCompare(b.displayLabel))
  )

  /**
   * Resolves the phone code for a given country ISO2 code.
   *
   * @param countryIso2 - The ISO2 country code.
   * @returns The phone code string (e.g. "+1"), defaults to "+1" if not found.
   */
  const resolvePhoneCode = (countryIso2: string): string => {
    const c = geoCountries.find(g => g.iso2 === countryIso2)
    return c?.phone ? `+${c.phone}` : '+1'
  }

  /**
   * Parses a phone string into country ISO2 and number parts.
   *
   * @param phone - Full phone string (e.g. "+1 5551234567").
   * @returns Object with countryIso2 and number.
   */
  const parsePhone = (phone: string): { countryIso2: string; number: string } => {
    const match = phone.match(/^(\+\d+)\s+(.+)$/)
    if (match) {
      const code = match[1]
      const found = geoCountries.find(g => g.phone && `+${g.phone}` === code)
      // Group 2 is mandatory in the regex above, so it is always present; falling back to
      // the whole phone string keeps the same shape as the unmatched return below.
      if (found?.iso2) {
        return { countryIso2: String(found.iso2), number: match[2] ?? phone }
      }
    }
    return { countryIso2: 'US', number: phone }
  }

  return { geoCountries, countryOptions, phoneCodeOptions, resolvePhoneCode, parsePhone }
}
