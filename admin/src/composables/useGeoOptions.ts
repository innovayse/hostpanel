import { computed } from 'vue'
import { CountriesAtlas } from '@innovayse/geo-atlas'

/**
 * Provides geo-atlas based country and phone code options for AppSelect dropdowns.
 * Eliminates duplication across AddClientView, ClientProfileView, and ContactFormModal.
 *
 * @returns Country options, phone code options, and phone code resolver.
 */
export function useGeoOptions() {
  /** All countries from geo-atlas. */
  const geoCountries = CountriesAtlas.getCountries()

  /** Country options for the AppSelect dropdown, sourced from geo-atlas. */
  const countryOptions = computed<{ value: string; label: string; displayLabel: string }[]>(() =>
    [{ value: '', label: 'Select country', displayLabel: 'Select country' },
     ...geoCountries.filter(c => c.iso2).map(c => ({
       value: String(c.iso2),
       label: `${c.emoji ?? ''} ${c.name}`,
       displayLabel: `${c.emoji ?? ''} ${c.name}`,
     }))]
  )

  /** Phone code options keyed by ISO2 for uniqueness. */
  const phoneCodeOptions = computed<{ value: string; label: string; displayLabel: string }[]>(() =>
    geoCountries
      .filter(c => c.phone && c.name && c.iso2)
      .map(c => ({
        value: String(c.iso2),
        label: `${c.emoji ?? ''} +${c.phone}`,
        displayLabel: `${c.emoji ?? ''} ${c.name} (+${c.phone})`,
      }))
      .sort((a, b) => a.displayLabel.localeCompare(b.displayLabel))
  )

  /**
   * Resolves the phone code for a given country ISO2 code.
   *
   * @param countryIso2 - The ISO2 country code.
   * @returns The phone code string (e.g. "+1"), defaults to "+1" if not found.
   */
  function resolvePhoneCode(countryIso2: string): string {
    const c = geoCountries.find(g => g.iso2 === countryIso2)
    return c?.phone ? `+${c.phone}` : '+1'
  }

  /**
   * Parses a phone string into country ISO2 and number parts.
   *
   * @param phone - Full phone string (e.g. "+1 5551234567").
   * @returns Object with countryIso2 and number.
   */
  function parsePhone(phone: string): { countryIso2: string; number: string } {
    const match = phone.match(/^(\+\d+)\s+(.+)$/)
    if (match) {
      const code = match[1]
      const found = geoCountries.find(g => g.phone && `+${g.phone}` === code)
      if (found?.iso2) return { countryIso2: String(found.iso2), number: match[2] }
    }
    return { countryIso2: 'US', number: phone }
  }

  return { geoCountries, countryOptions, phoneCodeOptions, resolvePhoneCode, parsePhone }
}
