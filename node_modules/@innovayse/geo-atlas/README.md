# @innovayse/geo-atlas

Offline countries, states and cities database with complete multilingual support.
Zero API calls — all data is bundled locally.

[![npm version](https://img.shields.io/npm/v/@innovayse/geo-atlas.svg)](https://www.npmjs.com/package/@innovayse/geo-atlas)
[![npm downloads](https://img.shields.io/npm/dm/@innovayse/geo-atlas.svg)](https://www.npmjs.com/package/@innovayse/geo-atlas)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **250 countries** with ISO2/ISO3 codes, flags, phone codes, capitals, coordinates
- **147,000+ cities** across all countries with coordinates
- **Complete multilingual city names**: English, Russian (ru), Armenian (hy) + 15 more languages
- **Country translations**: all 250 countries translated to Russian and Armenian
- **Native script** support for each city and country
- **Dual ESM + CJS build** — works in browsers, Node.js, Nuxt 3, Vite
- **TypeScript** — full type declarations included
- **Zero runtime dependencies** — fully offline, no API calls

## Installation

```bash
npm install @innovayse/geo-atlas
```

## Usage

### Get all countries

```ts
import { CountriesAtlas } from '@innovayse/geo-atlas'

const countries = CountriesAtlas.getCountries()
// [{ iso2: 'AM', name: 'Armenia', native: 'Հայաստան', emoji: '🇦🇲', phone: 374, ... }]
```

### Find a country by ISO2

```ts
const armenia = CountriesAtlas.find('AM')
// { iso2: 'AM', name: 'Armenia', translations: { hy: 'Հայաստան', ru: 'Армения' }, ... }
```

### Get states/regions — sync (Node.js / CJS)

```ts
const states = CountriesAtlas.getStates('AM')
// [{ name: 'Yerevan', cities: [...] }, ...]
```

### Get states/regions — async (Browser / ESM)

`getStatesAsync` uses dynamic `import()` and works in all environments including browsers:

```ts
const states = await CountriesAtlas.getStatesAsync('AM')
// [{ name: 'Yerevan', cities: [...] }, ...]
```

### Get cities

```ts
const cities = states[0].cities
// [{ name: 'Yerevan', native: 'Երևան', translations: { ru: 'Ереван', hy: 'Երևան' }, ... }]
```

## City Translation Fields

Each city includes:

| Field | Description | Example |
|-------|-------------|---------|
| `name` | English name | `"Yerevan"` |
| `native` | Native script | `"Երևան"` |
| `translations.ru` | Russian | `"Ереван"` |
| `translations.hy` | Armenian | `"Երևան"` |
| `translations.fr` | French | `"Erevan"` |
| `translations.de` | German | `"Eriwan"` |
| `translations.zh` | Chinese | `"埃里温"` |
| `translations.ar` | Arabic | `"يريفان"` |

**Supported translation keys:** `ru`, `hy`, `uk`, `ar`, `zh`, `es`, `fr`, `de`, `it`, `pt`, `pl`, `tr`, `ja`, `ko`, `nl`

## Country Fields

```ts
type Country = {
  iso2: string        // 'AM'
  iso3: string        // 'ARM'
  name: string        // 'Armenia'
  native: string      // 'Հայաստան'
  emoji: string       // '🇦🇲'
  capital: string     // 'Yerevan'
  phone: number       // 374
  latitude: string    // '40.0'
  longitude: string   // '45.0'
  translations: {
    hy: string        // 'Հայաստան'
    ru: string        // 'Армения'
    // + 15 more languages
  }
  currency: string    // 'AMD'
  timezones: Timezone[]
}
```

## Nuxt 3 / Vite Setup

To prevent esbuild from processing the large JSON data files (which causes OOM crashes), exclude the package from Vite's dependency optimization:

```ts
// nuxt.config.ts
export default defineNuxtConfig({
  vite: {
    optimizeDeps: {
      exclude: ['@innovayse/geo-atlas']
    }
  }
})
```

Then use `getStatesAsync` for browser-side city loading:

```ts
// composables/useCountries.ts
import { CountriesAtlas } from '@innovayse/geo-atlas'

// Async — works in browser (ESM dynamic import)
const cities = await CountriesAtlas.getStatesAsync('AM')

// Sync — works in Node.js / SSR only
const cities = CountriesAtlas.getStates('AM')
```

## Multilingual Country/City Names

```ts
type GeoLocale = 'en' | 'ru' | 'hy'

/** Get localized country name with fallback chain */
function getCountryName(country: Country, locale: GeoLocale): string {
  if (locale === 'en') return country.name
  return country.translations?.[locale] ?? country.native ?? country.name
}

/** Get localized city name with fallback chain */
function getCityName(city: City, locale: GeoLocale): string {
  if (locale === 'en') return city.name
  return city.translations?.[locale] ?? city.native ?? city.name
}
```

## Validation

```ts
import { ValidatorAtlas } from '@innovayse/geo-atlas'

ValidatorAtlas.isValidCountry('AM')            // true
ValidatorAtlas.isValidCountry('XX')            // false
ValidatorAtlas.isValidState('AM', 'Yerevan')   // true
```

## License

MIT © [Innovayse](https://github.com/innovayse)
