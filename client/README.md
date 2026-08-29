# Innovayse

A modern, full-cycle digital agency website built with Nuxt 3, featuring web development services, SEO, PPC advertising, and SaaS products.

## Tech Stack

- **Framework:** Nuxt 3
- **Styling:** Tailwind CSS
- **Animations:** @vueuse/motion
- **Icons:** Nuxt Icon (Phosphor Icons)
- **i18n:** @nuxtjs/i18n (English & Russian)
- **TypeScript:** Full type support

## Features

- Responsive design with mobile-first approach
- Dark theme with gradient accents
- Smooth scroll animations (v-motion)
- Multi-language support (EN/RU)
- SEO optimized with meta tags
- Modern glass-morphism UI effects

## Pages

| Route | Description |
|-------|-------------|
| `/` | Home page with all landing sections |
| `/about` | Company information, values, timeline, tech stack |
| `/services` | Services overview |
| `/services/[service]/[branch]` | Individual service details |
| `/products` | SaaS products showcase with comparison |
| `/portfolio` | Project portfolio gallery |
| `/portfolio/[project]` | Individual project case study |
| `/blog` | Blog listing with category filtering |
| `/blog/[slug]` | Individual blog post |
| `/contact` | Contact form and information |
| `/faq` | Frequently asked questions |
| `/terms` | Terms of Service |
| `/privacy` | Privacy Policy |

## Project Structure

```
├── assets/
│   └── styles/
│       └── animations.css      # Custom CSS animations
├── components/
│   ├── layout/
│   │   ├── Header.vue          # Navigation header
│   │   └── Footer.vue          # Site footer
│   ├── sections/               # Homepage sections
│   │   ├── Hero.vue
│   │   ├── Services.vue
│   │   ├── Products.vue
│   │   ├── Portfolio.vue
│   │   ├── Testimonials.vue
│   │   ├── FAQ.vue
│   │   ├── Process.vue
│   │   ├── Partners.vue
│   │   ├── WhyChooseUs.vue
│   │   └── CTA.vue
│   └── ui/                     # Reusable UI components
│       └── Button.vue
├── composables/                # Vue composables
├── constants/                  # Static data
├── layouts/
│   └── default.vue             # Default layout
├── locales/
│   ├── en/                     # English translations
│   └── ru/                     # Russian translations
├── pages/                      # File-based routing
├── plugins/
│   ├── i18n.ts                 # i18n loader
│   └── motion.ts               # Motion plugin
├── public/                     # Static assets
├── types/                      # TypeScript types
└── utils/                      # Utility functions
```

## Getting Started

### Prerequisites

- Node.js 18+
- npm or pnpm

### Installation

```bash
# Clone the repository
git clone https://github.com/your-repo/innovayse.git
cd innovayse

# Install dependencies
npm install

# Start development server
npm run dev
```

### Build for Production

```bash
# Build the application
npm run build

# Preview production build
npm run preview
```

## Configuration

### Environment Variables

Copy `client/.env.example` to `client/.env`. It holds no credential for an external service, and
none may be added: this app is the one a browser talks to, and every secret it used to carry now
belongs to the API's environment instead.

### Contact Form Delivery — Email And Telegram

**Neither is configured here.** The contact form posts to the C# backend, which sends the mail
through the one SMTP relay this platform configures and posts the enquiry to Telegram itself — so
the relay credentials and the bot token live in the API's environment and nowhere else. This app
held both until they moved; putting either back would give a secret a second container to leak
from, and the platform a second client of the same service.

Set them in the repository root's `.env`:

```env
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USER=your-email@example.com
SMTP_PASSWORD=your-password
# Where contact-form enquiries are delivered (Notifications__ContactEmail on the API).
EMAIL_TO=contact@yourdomain.com
# Optional. Both or neither — the API refuses to start on half of a bot.
TELEGRAM_BOT_TOKEN=123456789:ABCdefGHIjklMNOpqrsTUVwxyz
TELEGRAM_CHAT_ID=123456789
```

Left unset, `EMAIL_TO` makes the form answer `503 CONTACT_NOT_CONFIGURED` and the page says the
form is unavailable. It never reports a message as sent that was not.

Left unset, the Telegram pair simply means no chat post: the enquiry is still delivered by email
and the API logs that it posted nowhere. A Telegram failure is logged too, and never turns a
delivered enquiry into a failure the visitor is shown.

### Telegram Bot Setup

1. **Create a Bot:**
   - Open Telegram and search for `@BotFather`
   - Send `/newbot` and follow the instructions
   - Copy the bot token

2. **Get Your Chat ID:**
   - Search for `@userinfobot` on Telegram
   - Send any message to get your chat ID
   - For groups: add the bot to the group, then use `@getidsbot`

3. **Add both to the repository root's `.env`**, not to `client/.env`.

### i18n

Translations are stored in `locales/` directory. To add a new language:

1. Create a new folder in `locales/` (e.g., `locales/de/`)
2. Copy all JSON files from `locales/en/`
3. Translate the content
4. Add the locale to `nuxt.config.ts` and `plugins/i18n.ts`

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start development server |
| `npm run build` | Build for production |
| `npm run preview` | Preview production build |
| `npm run lint` | Run ESLint |
| `npm run lint:fix` | Fix ESLint errors |

## Design System

### Colors

- **Primary:** Cyan/Sky Blue (`#0ea5e9`)
- **Secondary:** Purple/Violet (`#8b5cf6`)
- **Background:** Dark (`#0a0a0f`, `#0d0d12`)
- **Text:** White & Gray variants

### Typography

- **Font:** System font stack
- **Headings:** Bold, gradient text effects
- **Body:** Gray-400 for readability

### Components

All components use consistent styling:
- Rounded corners (`rounded-xl`, `rounded-2xl`)
- Glass effects (`bg-white/5`, `backdrop-blur`)
- Border accents (`border-white/10`)
- Hover transitions (`transition-all duration-300`)

## License

MIT License - see LICENSE file for details.

## Contact

- Website: [innovayse.com](https://innovayse.com)
- Email: contact@innovayse.com
