/**
 * Schema.org structured data composable
 * Provides JSON-LD markup for SEO
 */

import { useHead } from '#app'

export const useSchemaOrg = () => {
  const config = useRuntimeConfig()
  const baseUrl = config.public.baseUrl || 'https://yourdomain.com'

  /**
   * Organization schema
   */
  const organizationSchema = () => {
    return {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      '@id': `${baseUrl}/#organization`,
      name: 'Innovayse',
      url: baseUrl,
      logo: {
        '@type': 'ImageObject',
        url: `${baseUrl}/logo.png`
      },
      description: 'Full-cycle digital agency specializing in web development, SEO, PPC advertising, and SaaS products',
      foundingDate: '2016',
      contactPoint: {
        '@type': 'ContactPoint',
        telephone: '+374-33-731673',
        contactType: 'customer service',
        availableLanguage: ['English', 'Russian', 'Armenian']
      },
      address: {
        '@type': 'PostalAddress',
        addressCountry: 'AM',
        addressLocality: 'Yerevan'
      },
      sameAs: [
        'https://t.me/innovayse',
        'https://wa.me/37433731673'
      ],
      email: 'contact@yourdomain.com'
    }
  }

  /**
   * LocalBusiness schema
   */
  const localBusinessSchema = () => {
    return {
      '@context': 'https://schema.org',
      '@type': 'LocalBusiness',
      '@id': `${baseUrl}/#organization`,
      name: 'Innovayse',
      image: `${baseUrl}/logo.png`,
      url: baseUrl,
      telephone: '+374-33-731673',
      email: 'contact@yourdomain.com',
      address: {
        '@type': 'PostalAddress',
        addressCountry: 'AM',
        addressLocality: 'Yerevan'
      },
      geo: {
        '@type': 'GeoCoordinates',
        latitude: 40.175837,
        longitude: 44.512906
      },
      openingHoursSpecification: {
        '@type': 'OpeningHoursSpecification',
        dayOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
        opens: '09:00',
        closes: '18:00'
      },
      priceRange: '$$',
      aggregateRating: {
        '@type': 'AggregateRating',
        ratingValue: '4.9',
        reviewCount: '150'
      }
    }
  }

  /**
   * WebSite schema with search action
   */
  const websiteSchema = () => {
    return {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      '@id': `${baseUrl}/#website`,
      url: baseUrl,
      name: 'Innovayse',
      description: 'Full-cycle digital agency - Web Development, SEO, PPC, SaaS',
      publisher: {
        '@id': `${baseUrl}/#organization`
      },
      inLanguage: ['en', 'ru', 'hy']
    }
  }

  /**
   * Service schema
   */
  const serviceSchema = (service: {
    name: string
    description: string
    url: string
    serviceType: string
  }) => {
    return {
      '@context': 'https://schema.org',
      '@type': 'Service',
      name: service.name,
      description: service.description,
      url: service.url.startsWith('http') ? service.url : `${baseUrl}${service.url}`,
      serviceType: service.serviceType,
      provider: {
        '@id': `${baseUrl}/#organization`
      },
      areaServed: {
        '@type': 'Country',
        name: 'Worldwide'
      }
    }
  }

  /**
   * Product schema (for SaaS products)
   */
  const productSchema = (product: {
    name: string
    description: string
    url: string
    image?: string
  }) => {
    return {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: product.name,
      description: product.description,
      url: product.url.startsWith('http') ? product.url : `${baseUrl}${product.url}`,
      applicationCategory: 'BusinessApplication',
      operatingSystem: 'Web',
      offers: {
        '@type': 'AggregateOffer',
        priceCurrency: 'USD',
        lowPrice: '99',
        highPrice: '999',
        offerCount: '3'
      },
      aggregateRating: {
        '@type': 'AggregateRating',
        ratingValue: '4.8',
        reviewCount: '120'
      },
      provider: {
        '@id': `${baseUrl}/#organization`
      }
    }
  }

  /**
   * BreadcrumbList schema
   */
  const breadcrumbSchema = (items: Array<{ name: string; url: string }>) => {
    return {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: items.map((item, index) => ({
        '@type': 'ListItem',
        position: index + 1,
        name: item.name,
        item: item.url.startsWith('http') ? item.url : `${baseUrl}${item.url}`
      }))
    }
  }

  /**
   * FAQ schema
   */
  const faqSchema = (faqs: Array<{ question: string; answer: string }>) => {
    return {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: faqs.map(faq => ({
        '@type': 'Question',
        name: faq.question,
        acceptedAnswer: {
          '@type': 'Answer',
          text: faq.answer
        }
      }))
    }
  }

  /**
   * Inject schema into head
   */
  const injectSchema = (schema: object | object[]) => {
    const schemas = Array.isArray(schema) ? schema : [schema]

    useHead({
      script: schemas.map(s => ({
        type: 'application/ld+json',
        children: JSON.stringify(s)
      }))
    })
  }

  return {
    baseUrl,
    organizationSchema,
    localBusinessSchema,
    websiteSchema,
    serviceSchema,
    productSchema,
    breadcrumbSchema,
    faqSchema,
    injectSchema
  }
}
