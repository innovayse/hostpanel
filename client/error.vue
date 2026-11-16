<template>
  <UiStack
    gap="none"
    align="center"
    justify="center"
    class="relative min-h-dvh overflow-hidden bg-gray-50 dark:bg-gray-950"
    data-testid="error-page"
  >
    <!-- Background -->
    <UiBackdrop variant="aurora" />

    <!-- Error Content -->
    <UiContainer class="relative z-10">
      <UiStack gap="lg" class="text-center">
        <!-- Error Code -->
        <UiHeading
          :level="1"
          size="display"
          class="!text-[150px] !leading-none md:!text-[200px] animate-reveal-up motion-reduce:animate-none"
          data-testid="error-status"
        >
          <UiGradientText class="bg-gradient-to-r from-primary-400 via-secondary-400 to-primary-400">
            {{ error.statusCode }}
          </UiGradientText>
        </UiHeading>

        <!-- Error Message Card -->
        <UiCard
          padding="xl"
          class="relative backdrop-blur-xl md:p-12 animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(200)"
        >
          <UiStack gap="md" align="center">
            <component
              :is="errorIcon"
              :size="64"
              :stroke-width="2"
              class="animate-bounce text-primary-500 dark:text-primary-400"
            />

            <UiHeading :level="2" size="title" data-testid="error-title">
              {{ errorTitle }}
            </UiHeading>

            <UiText size="lg" tone="muted" data-testid="error-message">
              {{ errorMessage }}
            </UiText>

            <!-- Actions -->
            <UiStack
              direction="row"
              wrap
              justify="center"
              gap="md"
              class="mt-4"
            >
              <UiButton size="lg" data-testid="error-go-back" @click="handleGoBack">
                <ArrowLeft :size="20" :stroke-width="2" class="mr-2" />
                {{ t('error.goBack') }}
              </UiButton>
              <UiButton
                :to="localePath(SITE_HOME_PATH)"
                variant="subtle"
                size="lg"
                data-testid="error-go-home"
              >
                <Home :size="20" :stroke-width="2" class="mr-2" />
                {{ t('error.goHome') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiCard>

        <!-- Help Text -->
        <UiText
          size="sm"
          tone="muted"
          class="animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(400)"
        >
          {{ t('error.helpText') }}
          <UiLink :to="localePath(SITE_CONTACT_PATH)" testid="error-contact">
            {{ t('error.contactUs') }}
          </UiLink>
        </UiText>
      </UiStack>
    </UiContainer>

    <!-- Corner decorations -->
    <UiStack
      aria-hidden="true"
      gap="none"
      class="pointer-events-none absolute right-0 top-0 h-48 w-48 border-r-2 border-t-2 border-secondary-500/30"
    />
    <UiStack
      aria-hidden="true"
      gap="none"
      class="pointer-events-none absolute bottom-0 left-0 h-48 w-48 border-b-2 border-l-2 border-primary-500/30"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Error page: shown by Nuxt for any unhandled error (404, 403, 500 and so on) with wording and an icon
 * per status, a way back and a way home.
 */

import { revealDelay } from '~/utils/reveal'
import type { Component } from 'vue'
import { ArrowLeft, Home, Search, AlertTriangle, Lock, XCircle } from 'lucide-vue-next'
import { ErrorStatus } from '~/types/errorstatus'
import { SITE_CONTACT_PATH, SITE_HOME_PATH } from '~/constants/siteRoutes'

/** The error Nuxt hands the page. */
interface ErrorDetails {
  /** HTTP status of the failure. */
  statusCode: number
  /** The error's own message; used when no wording exists for the status. */
  message?: string
}

/** Props of the error page. */
interface Props {
  /** The error to explain. */
  error: ErrorDetails
}

/** Props of the page. */
const props = defineProps<Props>()

/** Translates a key in the active language. */
const { t } = useI18n()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

/** Icon per status; any other status uses the generic cross. */
const STATUS_ICONS: Record<ErrorStatus, Component> = {
  [ErrorStatus.NotFound]: Search,
  [ErrorStatus.ServerError]: AlertTriangle,
  [ErrorStatus.Forbidden]: Lock
}

/** The icon for the error's status. */
const errorIcon = computed<Component>(() => STATUS_ICONS[props.error.statusCode as ErrorStatus] ?? XCircle)

/** Title for the error's status, or the generic one. */
const errorTitle = computed(() => t(`error.${props.error.statusCode}.title`, t('error.default.title')))

/** Explanation for the error's status, or the error's own message, or the generic one. */
const errorMessage = computed(() =>
  t(`error.${props.error.statusCode}.message`, props.error.message || t('error.default.message')))

/** Goes one page back, or to the home page when there is no history to go back to. */
const handleGoBack = (): void => {
  if (window.history.length > 1) {
    window.history.back()
  } else {
    navigateTo(localePath(SITE_HOME_PATH))
  }
}
</script>
