<template>
  <section :aria-label="t('nova.trust.performance')" class="border-b border-nova-border bg-nova-surface">
    <ul class="mx-auto flex max-w-[1240px] flex-wrap items-center justify-center gap-x-8 gap-y-4 px-4 py-6 sm:px-6 lg:px-8">
      <!--
        The uptime figure is the operator's own published number and appears only
        when they have entered one. This project has nothing that measures
        uptime, so a default here would be a number somebody made up.
      -->
      <li v-if="uptime" class="flex items-center gap-2.5 text-[15px] text-nova-ink">
        <Icon name="lucide:activity" class="h-[18px] w-[18px] text-nova-success" aria-hidden="true" />
        <span><strong class="font-bold">{{ uptime }}</strong> {{ t('nova.trust.uptimeLabel') }}</span>
      </li>

      <li v-for="item in items" :key="item.key" class="flex items-center gap-2.5 text-[15px] text-nova-ink">
        <Icon :name="item.icon" class="h-[18px] w-[18px] text-nova-brand" aria-hidden="true" />
        {{ t(item.titleKey) }}
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
/**
 * nova trust bar.
 *
 * Qualitative by design. Every line states something the platform does rather
 * than how well it does it, which is the only kind of claim this codebase can
 * stand behind — with the single exception of the uptime figure, which is the
 * operator's to publish and stays hidden until they do.
 */
import { TRUST_ITEMS, TRUST_UPTIME_SETTING } from '~/templates/nova/content'
import { useVisibleFeatures } from '~/templates/nova/features'

const { t } = useI18n()
const { get } = usePortalSettings()

const items = useVisibleFeatures(TRUST_ITEMS)
const uptime = computed(() => get(TRUST_UPTIME_SETTING[0], TRUST_UPTIME_SETTING[1]))
</script>
