<template>
  <UiStack gap="none" data-testid="domains-transfer">
    <!-- Hero with the transfer form -->
    <UiSection padding="lg" :contained="false" class="relative overflow-hidden bg-ui-page">
      <UiDecor class="inset-0">
        <UiDecor class="top-0 left-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[150px] animate-blob" />
        <UiDecor class="bottom-0 right-1/3 w-[400px] h-[400px] bg-primary-500/15 rounded-full blur-[130px] animate-blob animation-delay-2000" />
      </UiDecor>

      <UiContainer class="relative z-10 text-center">
        <!-- Breadcrumb -->
        <UiStack direction="row" align="center" justify="center" gap="xs" class="mb-6 text-sm">
          <UiLink :to="localePath(SITE_DOMAINS_PATH)" tone="muted" :underline="false" testid="domains-transfer-crumb">
            {{ $t('domains.card.name') }}
          </UiLink>
          <UiText :as="UiTextTag.Span" size="inherit" tone="subtle">/</UiText>
          <UiText :as="UiTextTag.Span" size="inherit">{{ $t('domains.tabTransfer') }}</UiText>
        </UiStack>

        <UiBadge variant="secondary" size="lg" rounded icon="mdi:swap-horizontal" class="mb-6 glass backdrop-blur-sm">
          {{ $t('domains.tabTransfer') }}
        </UiBadge>

        <UiHeading :level="1" size="display" class="mb-6 leading-tight">
          {{ $t('domains.promoTransferTitle') }}
          <UiGradientText class="block pb-2 bg-gradient-to-r from-primary-700 to-cyan-700 dark:from-primary-300 dark:to-cyan-300">
            {{ $t('domains.card.name') }}
          </UiGradientText>
        </UiHeading>

        <UiText tone="muted" size="lg" class="mb-10">{{ $t('domains.promoTransferDesc') }}</UiText>

        <!-- Tab switcher -->
        <UiStack direction="row" justify="center" gap="xs" class="mb-6">
          <UiButton variant="subtle" size="sm" icon="mdi:web" :to="localePath(SITE_DOMAINS_PATH)" data-testid="domains-tab-register">
            {{ $t('domains.tabRegister') }}
          </UiButton>
          <UiButton variant="primary" size="sm" icon="mdi:swap-horizontal" data-testid="domains-tab-transfer">
            {{ $t('domains.tabTransfer') }}
          </UiButton>
        </UiStack>

        <!-- Transfer form -->
        <UiStack gap="none" class="text-left">
          <UiCard data-testid="domains-transfer-form">
            <UiStack>
              <UiHeading :level="2" size="subsection">{{ $t('domains.transferFormTitle') }}</UiHeading>
              <UiInput
                v-model="domain"
                :label="$t('domains.transferDomainLabel')"
                :placeholder="$t('domains.transferDomainPlaceholder')"
                size="sm"
                testid="domains-transfer-domain"
              />
              <UiStack gap="none">
                <UiInput
                  v-model="epp"
                  :label="$t('domains.transferAuthLabel')"
                  :placeholder="$t('domains.transferEppPlaceholder')"
                  size="sm"
                  testid="domains-transfer-epp"
                />
                <UiStack direction="row" justify="end" gap="none" class="mt-1">
                  <UiTooltip>
                    <UiButton variant="ghost" size="sm" icon="mdi:help-circle-outline" data-testid="domains-transfer-help">
                      {{ $t('domains.transferAuthHelp') }}
                    </UiButton>
                    <template #content>
                      {{ $t('domains.transferAuthHelpText') }}
                    </template>
                  </UiTooltip>
                </UiStack>
              </UiStack>
              <UiButton variant="primary" full-width icon="mdi:cart-plus" data-testid="domains-transfer-submit" @click="emit('submit')">
                {{ $t('hosting.addToCart') }}
              </UiButton>
            </UiStack>
          </UiCard>
          <UiText size="xs" tone="subtle" class="mt-3 text-center">{{ $t('domains.transferNote') }}</UiText>
        </UiStack>
      </UiContainer>
    </UiSection>

    <!-- Promo: back to registration -->
    <UiSection padding="sm" class="bg-ui-page">
      <UiCard class="flex items-start justify-between gap-4" data-testid="domains-transfer-promo">
        <UiStack gap="none" align="start">
          <UiHeading :level="2" size="subsection" class="mb-1">{{ $t('domains.title') }} {{ $t('domains.titleHighlight') }}</UiHeading>
          <UiText tone="muted" size="sm" class="mb-4">{{ $t('domains.card.description') }}</UiText>
          <UiButton variant="outline" size="sm" icon="mdi:web" :to="localePath(SITE_DOMAINS_PATH)" data-testid="domains-transfer-search">
            {{ $t('domains.search') }} {{ $t('domains.card.name') }}
          </UiButton>
        </UiStack>
        <Globe :size="40" :stroke-width="1.5" class="text-primary-400/40 flex-shrink-0" />
      </UiCard>
    </UiSection>
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
import { Globe } from 'lucide-vue-next'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

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
