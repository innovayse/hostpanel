<template>
  <UiSection padding="lg" class="border-b border-nova-border">
    <UiStack gap="none" class="max-w-[75ch]">
      <NovaSectionHeader
        :eyebrow="t('nova.migration.eyebrow')"
        :title="t('nova.migration.title', { site: siteName })"
        :lead="t('nova.migration.lead')"
      >
        <template #badge>
          <!--
            Whether a migration is free is a commercial decision, and nothing in
            the backend records it. The badge waits to be switched on from
            Admin → Settings rather than being printed as a promise nobody made.
          -->
          <UiText
            v-if="freeMigration"
            :as="UiTextTag.Span"
            size="inherit"
            tone="inherit"
            class="rounded-full bg-nova-accent px-3 py-1 text-[12px] font-extrabold uppercase tracking-[0.06em] text-nova-on-accent"
          >
            {{ t('nova.migration.free') }}
          </UiText>
        </template>
      </NovaSectionHeader>
    </UiStack>

    <UiList ordered list-style="bare" class="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
      <UiStack v-for="(step, index) in MIGRATION_STEPS" :key="step.key" :as="UiStackTag.Li" gap="none">
        <UiCard padding="lg" class="relative h-full !border-nova-border !bg-nova-surface">
          <UiStack
            :as="UiStackTag.Span"
            align="center"
            justify="center"
            gap="none"
            class="h-9 w-9 rounded-full bg-nova-surface-2 text-[15px] font-extrabold text-nova-brand"
            aria-hidden="true"
          >{{ index + 1 }}</UiStack>
          <UiHeading :level="3" size="bare" class="mt-4 text-[16px] font-bold text-nova-ink">
            {{ t(step.titleKey) }}
          </UiHeading>
          <UiText size="inherit" tone="inherit" class="mt-2 text-[15px] leading-relaxed text-nova-muted">
            {{ t(step.bodyKey) }}
          </UiText>
        </UiCard>
      </UiStack>
    </UiList>

    <NovaCta :to="localePath(SITE_CONTACT_PATH)" tone="outline" class="mt-9" data-testid="nova-migration-cta">
      {{ t('nova.migration.cta') }}
    </NovaCta>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova migration walkthrough.
 *
 * An ordered list, because the steps happen in that order and the numbering is
 * information rather than decoration: the visible digits are hidden from
 * assistive technology, which gets the ordering from the list itself.
 */
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import { SETTING_ON } from '~/constants/settingValue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { MIGRATION_FREE_SETTING, MIGRATION_STEPS } from '~/templates/nova/content'
import { SITE_CONTACT_PATH } from '~/constants/siteRoutes'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()

/** Operator-set site name that the copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
/** Reads the operator settings. */
const { get } = usePortalSettings()

/** Whether the operator has switched the "free migration" badge on. */
const freeMigration = computed(() => get(MIGRATION_FREE_SETTING, '') === SETTING_ON)
</script>
