/**
 * Server-side i18n helper
 *
 * Reads the `x-locale` request header (sent by the frontend useApi composable)
 * and returns a localized string for server-side validation/error messages.
 *
 * Usage:
 *   throw createError({ statusCode: 400, statusMessage: serverT(event, 'error.domainRequired') })
 */
import type { H3Event } from 'h3'

type Locale = 'en' | 'ru' | 'hy'

const messages: Record<Locale, Record<string, string>> = {
  en: {
    'error.domainRequired': 'Domain name is required',
    'error.registerFields': 'First name, last name, email and password are required',
    'error.emailRequired': 'Email is required',
    'error.emailInUse': 'This email address is already registered. Please sign in or use a different email.',
    'error.registerFailed': 'Registration failed. Please try again.'
  },
  ru: {
    'error.domainRequired': 'Укажите доменное имя',
    'error.registerFields': 'Имя, фамилия, email и пароль обязательны',
    'error.emailRequired': 'Укажите email',
    'error.emailInUse': 'Этот email уже зарегистрирован. Войдите или используйте другой email.',
    'error.registerFailed': 'Регистрация не удалась. Попробуйте ещё раз.'
  },
  hy: {
    'error.domainRequired': 'Տիրույթի անունը պարտադիր է',
    'error.registerFields': 'Անուն, ազգանուն, էլ. հասցե և գաղտնաբառ պարտադիր են',
    'error.emailRequired': 'Էլ. հասցեն պարտադիր է',
    'error.emailInUse': 'Այս էլ. հասցեն արդեն գրանցված է: Մուտք գացեք կամ այլ էլ. հասցե օգտագործեք:',
    'error.registerFailed': 'Գրանցումը ձախողվել է: Կրկին փորձեք:'
  }
}

export function serverT(event: H3Event, key: string): string {
  const raw = getHeader(event, 'x-locale') ?? 'en'
  const locale: Locale = (['en', 'ru', 'hy'] as Locale[]).includes(raw as Locale)
    ? (raw as Locale)
    : 'en'
  return messages[locale][key] ?? messages.en[key] ?? key
}
