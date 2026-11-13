<template>
  <UiTrustBar
    :items="badges"
    surface-class="border border-line bg-surf"
    text-class="text-tx2"
    class="w-full"
  />
</template>

<script setup lang="ts">
/**
 * aurora trust bar: the guarantees, centred in one card with even gaps, each with its icon in a
 * tinted chip. Accepted payment methods are not listed: which gateways are live is the
 * operator's configuration, and a fixed list of brands would be a claim nothing backs.
 */
import { UiTone } from '~/utils/ui/enums'

const { t } = useI18n()

/** Guarantees with their icon and the tone of the chip behind it. */
const badges = computed(() => [
  { key: 'ssl', icon: 'lucide:lock', label: t('aurora.trust.ssl'), tone: UiTone.Success },
  { key: 'refund', icon: 'lucide:shield-check', label: t('aurora.trust.refund'), tone: UiTone.Primary },
  { key: 'noHidden', icon: 'lucide:badge-percent', label: t('aurora.trust.noHidden'), tone: UiTone.Secondary },
])
</script>
