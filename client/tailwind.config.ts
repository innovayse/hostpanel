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
    extend: {
      colors: {
        primary: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          200: '#bae6fd',
          300: '#7dd3fc',
          400: '#38bdf8',
          500: '#0ea5e9',
          600: '#0284c7',
          700: '#0369a1',
          800: '#075985',
          900: '#0c4a6e',
          950: '#082f49',
        },
        secondary: {
          50: '#faf5ff',
          100: '#f3e8ff',
          200: '#e9d5ff',
          300: '#d8b4fe',
          400: '#c084fc',
          500: '#a855f7',
          600: '#9333ea',
          700: '#7e22ce',
          800: '#6b21a8',
          900: '#581c87',
          950: '#3b0764',
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
        // Constant across both colour modes — the brand mark, not a theme token.
        brand: 'linear-gradient(135deg, #5D3FFF, #00D1FF)',
        glow1: 'radial-gradient(closest-side, var(--glow1), transparent)',
        glow2: 'radial-gradient(closest-side, var(--glow2), transparent)',
      },
      boxShadow: {
        panel: 'var(--sh)',
      },
      fontFamily: {
        // sans stays Inter so the classic template is untouched.
        sans: ['Inter Variable', 'Inter', 'system-ui', 'sans-serif'],
        aurora: ['Noto Sans Armenian', 'system-ui', 'sans-serif'],
        display: ['Noto Serif Armenian', 'serif'],
        // nova is Latin-first, but Inter carries no Armenian coverage, so the
        // hy locale would fall through to whatever the system picked. Noto Sans
        // Armenian sits behind it to cover those glyphs.
        nova: ['Inter Variable', 'Inter', 'Noto Sans Armenian', 'system-ui', 'sans-serif'],
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
