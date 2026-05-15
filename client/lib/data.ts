import type { Service, Product, Project, Testimonial, FAQ, TimelineEvent, TeamStats, Partner, ProcessStep } from '~/types'

/**
 * Company statistics displayed in Hero section
 */
export const teamStats: TeamStats = {
  projects: 150,
  years: 8,
  teamSize: 50,
  clients: 120
}

/**
 * Services offered by Innovayse
 * Displayed in Services section and Services page
 */
export const services: Service[] = [
  {
    id: 'web-development',
    icon: 'code-braces',
    category: 'development',
    branchKeys: ['corporate', 'ecommerce', 'webapp', 'landing', 'cms'],
    branchIcons: {
      corporate: 'mdi:briefcase',
      ecommerce: 'mdi:cart',
      webapp: 'mdi:application',
      landing: 'mdi:rocket-launch',
      cms: 'mdi:folder-multiple'
    },
    link: '/services#development'
  },
  {
    id: 'mobile-development',
    icon: 'cellphone',
    category: 'development',
    branchKeys: ['ios', 'android', 'crossplatform', 'pwa'],
    branchIcons: {
      ios: 'mdi:apple',
      android: 'mdi:android',
      crossplatform: 'mdi:devices',
      pwa: 'mdi:earth'
    },
    link: '/services#development'
  },
  {
    id: 'technical-seo',
    icon: 'magnify',
    category: 'seo',
    branchKeys: ['audit', 'vitals', 'schema', 'crawl'],
    branchIcons: {
      audit: 'mdi:wrench',
      vitals: 'mdi:gauge',
      schema: 'mdi:code-tags',
      crawl: 'mdi:bug'
    },
    link: '/services#seo'
  },
  {
    id: 'content-seo',
    icon: 'file-document',
    category: 'seo',
    branchKeys: ['keyword', 'strategy', 'onpage', 'linkbuilding', 'local', 'ecommerce', 'international'],
    branchIcons: {
      keyword: 'mdi:magnify',
      strategy: 'mdi:strategy',
      onpage: 'mdi:file-document',
      linkbuilding: 'mdi:link-variant',
      local: 'mdi:map-marker',
      ecommerce: 'mdi:shopping',
      international: 'mdi:earth'
    },
    link: '/services#seo'
  },
  {
    id: 'google-ads',
    icon: 'target',
    category: 'ppc',
    branchKeys: ['search', 'display', 'social', 'shopping', 'video', 'remarketing', 'app'],
    branchIcons: {
      search: 'mdi:magnify',
      display: 'mdi:image',
      social: 'mdi:account-group',
      shopping: 'mdi:cart',
      video: 'mdi:video',
      remarketing: 'mdi:refresh',
      app: 'mdi:cellphone'
    },
    link: '/services#ppc'
  },
  {
    id: 'yandex-direct',
    icon: 'flash',
    category: 'ppc',
    branchKeys: ['search', 'yan', 'retargeting', 'smart'],
    branchIcons: {
      search: 'mdi:magnify',
      yan: 'mdi:image',
      retargeting: 'mdi:arrow-u-left-top',
      smart: 'mdi:star-four-points'
    },
    link: '/services#ppc'
  }
]

/**
 * SaaS products developed by Innovayse
 * Displayed in Products section and Products page
 * Text content (name, tagline, description, features, pricing) loaded from i18n
 */
export const products: Product[] = [
  {
    id: 'web-hosting',
    icon: 'cloud-check',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/hosting',
    color: '#0ea5e9',
    image: '/images/products/hosting.jpg'
  },
  {
    id: 'domains',
    icon: 'earth',
    pricingKeys: [],
    demoUrl: '',
    learnMoreUrl: '/domains',
    color: '#3b82f6',
    image: '/images/products/domains.jpg'
  },
  {
    id: 'kelpie-ai',
    icon: 'brain',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: 'https://kelpie-ai.ai',
    learnMoreUrl: '/products/kelpie-ai',
    color: '#7c3aed',
    image: '/images/products/kelpie-ai.jpg'
  },
  {
    id: 'metricskit-pro',
    icon: 'chart-bar',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/metricskit-pro',
    color: '#ec4899',
    image: '/images/products/metricskit-pro.jpg'
  },
  {
    id: 'smartlearn-system',
    icon: 'school',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/smartlearn-system',
    color: '#8b5cf6',
    image: '/images/products/smartlearn-system.jpg'
  },
  {
    id: 'propsystem-pro',
    icon: 'office-building',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/propsystem-pro',
    color: '#06b6d4',
    image: '/images/products/propsystem-pro.jpg'
  },
  {
    id: 'shopkit-pro',
    icon: 'cart',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/shopkit-pro',
    color: '#f59e0b',
    image: '/images/products/shopkit-pro.jpg'
  },
  {
    id: 'quickbite',
    icon: 'silverware-fork-knife',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/quickbite',
    color: '#ef4444',
    image: '/images/products/quickbite.jpg'
  },
  {
    id: 'taskero',
    icon: 'view-dashboard',
    pricingKeys: ['starter', 'professional', 'enterprise'],
    demoUrl: '#',
    learnMoreUrl: '/products/taskero',
    color: '#10b981',
    image: '/images/products/taskero.jpg'
  }
]

/**
 * Portfolio projects - case studies
 * Displayed in Portfolio section and Portfolio page
 * Text content (title, description, task, process, results, metrics, features, testimonial) loaded from i18n
 */
export const projects: Project[] = [
  {
    id: 'kelpie-ai',
    category: ['saas', 'development'],
    technologies: ['Next.js', 'Python', 'FastAPI', 'OpenAI', 'PostgreSQL', 'Redis', 'AWS'],
    metricKeys: ['users', 'response', 'uptime', 'satisfaction'],
    metricIcons: { users: 'mdi:account-group', response: 'mdi:clock', uptime: 'mdi:check-circle', satisfaction: 'mdi:emoticon-happy' },
    hasTestimonial: true,
    testimonialAuthor: 'Sarah Johnson',
    testimonialCompany: 'Kelpie AI',
    images: [
      ''
    ],
    year: 2025,
    url: 'https://kelpie-ai.ai'
  },
  {
    id: 'uk-voshozhdenie',
    category: ['development', 'saas'],
    technologies: ['Nuxt 3', 'Vue 3', 'Tailwind CSS', 'PostgreSQL', 'Stripe', 'Pinia'],
    metricKeys: ['residents', 'payments', 'reduction', 'satisfaction'],
    metricIcons: { residents: 'mdi:account-group', payments: 'mdi:currency-usd', reduction: 'mdi:trending-down', satisfaction: 'mdi:star' },
    hasTestimonial: true,
    testimonialAuthor: 'Lisa Anderson',
    testimonialCompany: 'UK Voshozhdenie',
    images: [
      '/images/projects/uk-voshozhdenie-1.jpg',
      '/images/projects/uk-voshozhdenie-2.jpg'
    ],
    year: 2023,
    url: 'https://uk-voshozhdenie.ru'
  },
  {
    id: 'eduzdrav',
    category: ['ecommerce', 'seo'],
    technologies: ['React', 'Shopify', 'Node.js', 'MongoDB', 'AWS S3', 'Stripe'],
    metricKeys: ['students', 'courses', 'revenue', 'completion'],
    metricIcons: { students: 'mdi:school', courses: 'mdi:book-open-page-variant', revenue: 'mdi:trending-up', completion: 'mdi:check-circle' },
    hasTestimonial: true,
    testimonialAuthor: 'Dr. Alexander Petrov',
    testimonialCompany: 'Eduzdrav',
    images: [
      '/images/projects/eduzdrav-1.jpg',
      '/images/projects/eduzdrav-2.jpg',
      '/images/projects/eduzdrav-3.jpg'
    ],
    year: 2023,
    url: 'https://eduzdrav.ru'
  },
  {
    id: 'irtekpro',
    category: ['saas', 'development'],
    technologies: ['Nuxt 3', 'Vite', 'C#', 'ASP.NET Core', 'MSSQL', 'Elasticsearch', 'Docker', 'Redis'],
    metricKeys: ['documents', 'users', 'searches', 'uptime'],
    metricIcons: { documents: 'mdi:file-multiple', users: 'mdi:account-group', searches: 'mdi:magnify', uptime: 'mdi:check-circle' },
    hasTestimonial: false,
    images: [
      '/images/projects/irtekpro-1.jpg',
      '/images/projects/irtekpro-2.jpg'
    ],
    year: 2024,
    url: 'https://www.irtekpro.am'
  },
  {
    id: 'stroy-istra',
    category: ['development', 'seo'],
    technologies: ['Nuxt 3', 'Vue 3', 'Tailwind CSS', 'Node.js', 'MongoDB', 'Cloudinary'],
    metricKeys: ['projects', 'clients', 'sqmeters', 'rating'],
    metricIcons: { projects: 'mdi:hammer', clients: 'mdi:account-group', sqmeters: 'mdi:ruler', rating: 'mdi:star' },
    hasTestimonial: false,
    images: [
      '/images/projects/stroy-istra-1.jpg',
      '/images/projects/stroy-istra-2.jpg'
    ],
    year: 2024,
    url: 'https://stroy-istra.ru'
  }
]

/**
 * Client testimonials for carousel
 * Text content (text, position) loaded from i18n locale files
 */
export const testimonials: Testimonial[] = [
  { id: 'testimonial-1', name: 'Sarah Johnson', company: 'Kelpie AI', avatar: '/avatars/sarah.jpg', rating: 5 },
  { id: 'testimonial-2', name: 'Dr. Alexander Petrov', company: 'Eduzdrav', avatar: '/avatars/alexander.jpg', rating: 5 },
  { id: 'testimonial-3', name: 'Michael Brown', company: 'Brown & Associates', avatar: '/avatars/michael.jpg', rating: 5 },
  { id: 'testimonial-6', name: 'Lisa Anderson', company: 'UK Voshozhdenie', avatar: '/avatars/lisa.jpg', rating: 5 }
]

/**
 * Frequently asked questions by category
 * Text content (question, answer) loaded from i18n locale files
 */
export const faqs: FAQ[] = [
  { id: 'faq-1', category: 'general' },
  { id: 'faq-2', category: 'general' },
  { id: 'faq-3', category: 'general' },
  { id: 'faq-4', category: 'general' },
  { id: 'faq-5', category: 'general' },
  { id: 'faq-6', category: 'development' },
  { id: 'faq-7', category: 'development' },
  { id: 'faq-8', category: 'development' },
  { id: 'faq-9', category: 'development' },
  { id: 'faq-10', category: 'seo' },
  { id: 'faq-11', category: 'seo' },
  { id: 'faq-12', category: 'seo' },
  { id: 'faq-13', category: 'seo' },
  { id: 'faq-14', category: 'products' },
  { id: 'faq-15', category: 'products' }
]

/**
 * Company timeline - key milestones
 * Text content (title, description) loaded from i18n locale files
 */
export const timeline: TimelineEvent[] = [
  { year: 2016 },
  { year: 2018 },
  { year: 2020 },
  { year: 2022 },
  { year: 2023 },
  { year: 2024 },
  { year: 2025 }
]

/**
 * Clients - Real companies from portfolio projects
 * Text content (description, industry) loaded from i18n locale files
 */
export const clients: Partner[] = [
  {
    id: 'kelpie-ai',
    name: 'Kelpie AI',
    icon: 'mdi:brain',
    url: 'https://kelpie-ai.ai'
  },
  {
    id: 'uk-voshozhdenie',
    name: 'UK Voshozhdenie',
    icon: 'mdi:chart-line',
    url: 'https://uk-voshozhdenie.ru'
  },
  {
    id: 'eduzdrav',
    name: 'Eduzdrav',
    icon: 'mdi:school',
    url: 'https://eduzdrav.ru'
  },
  {
    id: 'irtekpro',
    name: 'IrtekPro',
    icon: 'mdi:scale-balance',
    url: 'https://www.irtekpro.am'
  },
  {
    id: 'stroy-istra',
    name: 'Stroy-Istra',
    icon: 'mdi:hammer',
    url: 'https://stroy-istra.ru'
  }
]

/**
 * Process steps - how we work
 * Text content (title, description) loaded from i18n locale files
 */
export const processSteps: ProcessStep[] = [
  { id: 'discovery', order: 1, icon: 'mdi:chat-processing' },
  { id: 'design', order: 2, icon: 'mdi:ruler-square' },
  { id: 'development', order: 3, icon: 'mdi:code-tags' },
  { id: 'launch', order: 4, icon: 'mdi:rocket-launch' }
]

