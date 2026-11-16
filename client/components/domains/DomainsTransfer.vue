<template>
  <UiStack gap="none" data-testid="domains-transfer">
    <!-- Hero with the transfer form -->
    <section class="relative py-10 md:py-24 lg:py-32 bg-ui-page overflow-hidden">
      <UiDecor class="inset-0">
        <UiDecor class="top-0 left-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[150px] animate-blob" />
        <UiDecor class="bottom-0 right-1/3 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </UiDecor>

      <UiContainer class="relative z-10 text-center">
        <div>
          <!-- Breadcrumb -->
          <div class="flex items-center justify-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-6">
            <UiLink :to="localePath(SITE_DOMAINS_PATH)" tone="muted" :underline="false" class="hover:text-primary-600 dark:hover:text-primary-400" testid="domains-transfer-crumb">
              {{ $t('domains.card.name') }}
            </UiLink>
            <span>/</span>
            <span class="text-gray-900 dark:text-white">{{ $t('domains.tabTransfer') }}</span>
          </div>

          <span class="inline-flex items-center gap-2 px-4 py-2 mb-6 rounded-full glass border border-secondary-500/20 backdrop-blur-sm">
            <ArrowLeftRight :size="14" :stroke-width="2" class="text-secondary-700 dark:text-secondary-400" />
            <UiText as="span" size="sm" weight="medium" tone="muted">{{ $t('domains.tabTransfer') }}</UiText>
          </span>

          <UiHeading :level="1" size="display" class="mb-6 leading-tight">
            {{ $t('domains.promoTransferTitle') }}
            <span class="block pb-2 text-transparent bg-clip-text bg-gradient-to-r from-primary-700 to-cyan-700 dark:from-primary-300 dark:to-cyan-300">
              {{ $t('domains.card.name') }}
            </span>
          </UiHeading>

          <UiText tone="muted" size="lg" class="mb-10">{{ $t('domains.promoTransferDesc') }}</UiText>

          <!-- Tab switcher -->
          <div class="flex justify-center gap-2 mb-6">
            <UiButton variant="subtle" size="sm" :to="localePath(SITE_DOMAINS_PATH)" data-testid="domains-tab-register">
              <Globe :size="14" :stroke-width="2" class="mr-1.5" />
              {{ $t('domains.tabRegister') }}
            </UiButton>
            <UiButton variant="primary" size="sm" data-testid="domains-tab-transfer">
              <ArrowLeftRight :size="14" :stroke-width="2" class="mr-1.5" />
              {{ $t('domains.tabTransfer') }}
            </UiButton>
          </div>

          <!-- Transfer form -->
          <div class="text-left">
            <UiCard class="space-y-4" data-testid="domains-transfer-form">
              <UiHeading :level="2" size="subsection">{{ $t('domains.transferFormTitle') }}</UiHeading>
              <UiInput
                v-model="domain"
                :label="$t('domains.transferDomainLabel')"
                :placeholder="$t('domains.transferDomainPlaceholder')"
                size="sm"
                testid="domains-transfer-domain"
              />
              <div>
                <UiInput
                  v-model="epp"
                  :label="$t('domains.transferAuthLabel')"
                  :placeholder="$t('domains.transferEppPlaceholder')"
                  size="sm"
                  testid="domains-transfer-epp"
                />
                <div class="flex justify-end mt-1">
                  <UiTooltip>
                    <UiButton variant="ghost" size="sm" class="text-xs text-primary-700 dark:text-primary-300" data-testid="domains-transfer-help">
                      <HelpCircle :size="13" :stroke-width="2" class="mr-1" />
                      {{ $t('domains.transferAuthHelp') }}
                    </UiButton>
                    <template #content>
                      {{ $t('domains.transferAuthHelpText') }}
                    </template>
                  </UiTooltip>
                </div>
              </div>
              <UiButton full-width data-testid="domains-transfer-submit" @click="emit('submit')">
                {{ $t('domains.transferBtn') }}
              </UiButton>
            </UiCard>
            <UiText size="xs" tone="subtle" class="mt-3 text-center">{{ $t('domains.transferNote') }}</UiText>
          </div>
        </div>
      </UiContainer>
    </section>

    <!-- Promo: back to registration -->
    <section class="py-10 bg-ui-page">
      <UiContainer>
        <div>
          <UiCard class="flex items-start justify-between gap-4" data-testid="domains-transfer-promo">
            <div>
              <UiHeading :level="2" size="subsection" class="mb-1">{{ $t('domains.title') }} {{ $t('domains.titleHighlight') }}</UiHeading>
              <UiText tone="muted" size="sm" class="mb-4">{{ $t('domains.card.description') }}</UiText>
              <UiButton variant="outline" size="sm" :to="localePath(SITE_DOMAINS_PATH)" data-testid="domains-transfer-search">
                <Globe :size="14" :stroke-width="2" class="mr-1.5" />
                {{ $t('domains.search') }} {{ $t('domains.card.name') }}
              </UiButton>
            </div>
            <Globe :size="40" :stroke-width="1.5" class="text-primary-400/40 flex-shrink-0" />
          </UiCard>
        </div>
      </UiContainer>
    </section>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The domain-transfer screen: a hero with the transfer form (domain name, authorization code, the
 * help tooltip) and, below it, a card that leads back to registering a new domain.
 *
 * Presentational: the two fields are `v-model`s and the Add-to-Cart button only reports `submit`;
 * the page decides what a transfer request does.
 */
import { ArrowLeftRight, Globe, HelpCircle } from 'lucide-vue-next'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'

/** The domain name to transfer. */
const domain = defineModel<string>('domain', { required: true })

/** The authorization (EPP) code of the domain. */
const epp = defineModel<string>('epp', { required: true })

/** Events of DomainsTransfer. */
const emit = defineEmits<{
  /** The visitor asked to start the transfer. */
  submit: []
}>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
