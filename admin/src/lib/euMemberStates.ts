/**
 * The member states of the European Union, for the screens that only deal with them (the VAT rates form).
 */

/**
 * The 27 member-state codes, ISO 3166-1 alpha-2 as client records store them (Greece is `GR` here, though VAT returns
 * write `EL`). The same set the API accepts for a VAT rate row (`EuMemberStates.Codes` in the backend).
 */
export const EU_MEMBER_STATES: readonly string[] = [
  'AT', 'BE', 'BG', 'CY', 'CZ', 'DE', 'DK', 'EE', 'ES', 'FI', 'FR', 'GR', 'HR', 'HU',
  'IE', 'IT', 'LT', 'LU', 'LV', 'MT', 'NL', 'PL', 'PT', 'RO', 'SE', 'SI', 'SK',
]
