<template>
  <UiStack direction="row" align="center" justify="between" wrap data-testid="domain-header">
    <UiStack direction="row" align="center">
      <UiTile :icon="Globe" tone="primary" size="xl" />
      <UiStack gap="none">
        <UiHeading :level="1" size="subsection">{{ fullName }}</UiHeading>
        <UiText size="sm" tone="muted">{{ $t('client.domains.expires') }} {{ formatDate(domain.expiresAt) }}</UiText>
      </UiStack>
    </UiStack>
    <ClientStatusBadge :status="domain.status" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * DomainHeader. The title row of the domain detail page: the domain name, its expiry date and
 * its status badge.
 */
import { Globe } from 'lucide-vue-next'
import type { DomainDetail } from '~/types/domaindetail'
import { formatDate } from '~/utils/formatDate'

/** Props of DomainHeader. */
interface Props {
  /** The domain being shown. */
  domain: DomainDetail
  /** The full domain name. */
  fullName: string
}

defineProps<Props>()
</script>
