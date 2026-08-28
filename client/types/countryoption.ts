/** One entry in the country picker, as `GET /api/portal/public/countries` lists it. */
export interface CountryOption {
  /** ISO 3166-1 alpha-2 code, e.g. "AM" — what the profile stores. */
  value: string
  /** Country name in the requested locale. */
  label: string
}
