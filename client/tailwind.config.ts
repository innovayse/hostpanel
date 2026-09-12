import type { Config } from 'tailwindcss'
import plugin from 'tailwindcss/plugin'

export default <Partial<Config>>{
  darkMode: 'class',
  // The Nuxt Tailwind module scans components/, layouts/, pages/ and friends by
  // default. Portal templates deliberately live outside those directories, so
  // without listing templates/ here every utility used only inside a template
  // is silently dropped from the bundle — the markup renders, unstyled.
  content: [
    './components/**/*.{vue,js,ts}',
    './layouts/**/*.{vue,js,ts}',
    './pages/**/*.{vue,js,ts}',
    './plugins/**/*.{js,ts}',
    './composables/**/*.{js,ts}',
    './utils/**/*.{js,ts}',
    './templates/**/*.{vue,js,ts}',
    './app.vue',
    './error.vue',
  ],
  theme: {
    screens: {
      xs: '480px',
      sm: '640px',
      md: '768px',
      lg: '1024px',
      xl: '1280px',
      '2xl': '1536px',
    },
    extend: {
      colors: {
        // The brand scales read the :root variables plugins/brand-tokens.ts emits from
        // portal.brand.primary / .accent, with today's literal as the fallback — an
        // install that sets neither renders exactly what it did. The `r g b` form is
        // what lets the opacity modifier (bg-primary-500/30) keep working.
        primary: {
          50: 'rgb(var(--brand-primary-50, 240 249 255) / <alpha-value>)',
          100: 'rgb(var(--brand-primary-100, 224 242 254) / <alpha-value>)',
          200: 'rgb(var(--brand-primary-200, 186 230 253) / <alpha-value>)',
          300: 'rgb(var(--brand-primary-300, 125 211 252) / <alpha-value>)',
          400: 'rgb(var(--brand-primary-400, 56 189 248) / <alpha-value>)',
          500: 'rgb(var(--brand-primary-500, 14 165 233) / <alpha-value>)',
          600: 'rgb(var(--brand-primary-600, 2 132 199) / <alpha-value>)',
          700: 'rgb(var(--brand-primary-700, 3 105 161) / <alpha-value>)',
          800: 'rgb(var(--brand-primary-800, 7 89 133) / <alpha-value>)',
          900: 'rgb(var(--brand-primary-900, 12 74 110) / <alpha-value>)',
          950: 'rgb(var(--brand-primary-950, 8 47 73) / <alpha-value>)',
        },
        // Text on a filled primary (buttons in classic and the shared components) and on
        // the lighter tint aurora's gradient and nova's dark-mode brand are built from. The
        // plugin decides white or near-black per colour; unset keeps today's white / near-black.
        'on-primary': 'rgb(var(--brand-on-primary, 255 255 255) / <alpha-value>)',
        'on-tint': 'rgb(var(--brand-on-tint, 8 9 15) / <alpha-value>)',
        secondary: {
          50: 'rgb(var(--brand-accent-50, 250 245 255) / <alpha-value>)',
          100: 'rgb(var(--brand-accent-100, 243 232 255) / <alpha-value>)',
          200: 'rgb(var(--brand-accent-200, 233 213 255) / <alpha-value>)',
          300: 'rgb(var(--brand-accent-300, 216 180 254) / <alpha-value>)',
          400: 'rgb(var(--brand-accent-400, 192 132 252) / <alpha-value>)',
          500: 'rgb(var(--brand-accent-500, 168 85 247) / <alpha-value>)',
          600: 'rgb(var(--brand-accent-600, 147 51 234) / <alpha-value>)',
          700: 'rgb(var(--brand-accent-700, 126 34 206) / <alpha-value>)',
          800: 'rgb(var(--brand-accent-800, 107 33 168) / <alpha-value>)',
          900: 'rgb(var(--brand-accent-900, 88 28 135) / <alpha-value>)',
          950: 'rgb(var(--brand-accent-950, 59 7 100) / <alpha-value>)',
        },
        // aurora template design tokens. Each holds a complete colour value,
        // several of them rgba, so Tailwind's opacity modifier cannot decompose
        // them: use border-line, never border-line/50.
        page: 'var(--page)',
        tx: 'var(--tx)',
        tx2: 'var(--tx2)',
        mut: 'var(--mut)',
        mut2: 'var(--mut2)',
        line: 'var(--line)',
        line2: 'var(--line2)',
        surf: 'var(--surf)',
        panel: 'var(--panel)',
        input: 'var(--input)',
        ac1: 'var(--ac1)',
        ac2: 'var(--ac2)',
        ac3: 'var(--ac3)',
        acbg: 'var(--ac-bg)',
        ok: 'var(--ok)',
        danger: 'var(--danger)',

        // nova template design tokens. Prefixed --n-* and scoped to .tpl-nova
        // (and html[data-template='nova']) so they cannot collide with the
        // aurora tokens above. --n-border is rgba, so the opacity modifier
        // cannot decompose it: use border-nova-border, never /50.
        nova: {
          bg: 'var(--n-bg)',
          surface: 'var(--n-surface)',
          'surface-2': 'var(--n-surface-2)',
          ink: 'var(--n-ink)',
          muted: 'var(--n-muted)',
          brand: 'var(--n-brand)',
          'brand-hover': 'var(--n-brand-hover)',
          'on-brand': 'var(--n-on-brand)',
          'on-accent': 'var(--n-on-accent)',
          accent: 'var(--n-accent)',
          border: 'var(--n-border)',
          success: 'var(--n-success)',
          danger: 'var(--n-danger)',
        },
      },
      backgroundImage: {
        card: 'var(--card)',
        'card-hi': 'var(--card-hi)',
        'hero-grad': 'var(--hero-grad)',
        // Constant across both colour modes — the brand mark, not a theme token. Reads the
        // operator's accent and primary, falling back to the original purple → cyan.
        brand: 'linear-gradient(135deg, rgb(var(--brand-accent-500, 93 63 255)), rgb(var(--brand-primary-400, 0 209 255)))',
        glow1: 'radial-gradient(closest-side, var(--glow1), transparent)',
        glow2: 'radial-gradient(closest-side, var(--glow2), transparent)',
      },
      boxShadow: {
        panel: 'var(--sh)',
      },
      fontFamily: {
        // Each family reads the operator's typeface from :root first (see
        // plugins/brand-tokens.ts) and falls back to the template's own — sans stays Inter
        // so classic is untouched, aurora stays Noto Sans Armenian. --font-body drives the
        // body families and --font-heading the display ones.
        sans: ['var(--font-body, "Inter Variable", Inter, system-ui, sans-serif)'],
        aurora: ['var(--font-body, "Noto Sans Armenian", system-ui, sans-serif)'],
        display: ['var(--font-heading, "Noto Serif Armenian", serif)'],
        // nova is Latin-first, but Inter carries no Armenian coverage, so the
        // hy locale would fall through to whatever the system picked. Noto Sans
        // Armenian sits behind it to cover those glyphs.
        nova: ['var(--font-body, "Inter Variable", Inter, "Noto Sans Armenian", system-ui, sans-serif)'],
        // JetBrains Mono carries no Armenian coverage, so an .հայ domain and the
        // dram sign both fall through to whatever the system picks and render as
        // the wrong glyphs. Noto Sans Armenian sits in the fallback chain to
        // cover them; Latin and digits still come from JetBrains Mono.
        mono: ['JetBrains Mono', 'Noto Sans Armenian', 'ui-monospace', 'monospace'],
      }
    }
  },
  plugins: [
    plugin(function({ addComponents }) {
      addComponents({
        '.container-custom': {
          width: '100%',
          marginLeft: 'auto',
          marginRight: 'auto',
          paddingLeft: '1rem',
          paddingRight: '1rem',
          '@screen sm': {
            maxWidth: '640px',
            paddingLeft: '1.5rem',
            paddingRight: '1.5rem',
          },
          '@screen md': {
            maxWidth: '768px',
          },
          '@screen lg': {
            maxWidth: '1024px',
            paddingLeft: '2rem',
            paddingRight: '2rem',
          },
          '@screen xl': {
            maxWidth: '1280px',
          },
        }
      })
    })
  ]
}
