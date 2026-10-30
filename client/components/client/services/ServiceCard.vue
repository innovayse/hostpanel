<template>
  <UiLinkCard :to="awaitsSetup ? clientServiceSetupPath(service.id) : clientServicePath(service.id)" :highlight="awaitsSetup" :testid="`service-card-${service.id}`">
    <UiStack direction="row" justify="between" align="start" class="mb-3">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0">
        <UiTile size="lg" :icon="Server" tone="primary" />
        <UiStack gap="none" class="min-w-0">
          <UiText as="h3" size="sm" weight="semibold" truncate class="group-hover:text-primary-600 dark:group-hover:text-primary-400 transition-colors">
            {{ service.productName }}
          </UiText>
          <UiText as="span" size="xs" tone="subtle" truncate>{{ service.domain || $t('client.dashboard.noDomain') }}</UiText>
        </UiStack>
      </UiStack>
      <div class="flex-shrink-0">
        <UiBadge v-if="awaitsSetup" variant="info" size="sm" data-testid="service-needs-setup">{{ $t('client.dashboard.needsSetup') }}</UiBadge>
        <ClientStatusBadge v-else :status="service.status" />
      </div>
    </UiStack>

    <UiGrid cols="2" gap="sm" class="mt-4">
      <UiStack gap="none">
        <UiText as="span" size="xs" tone="subtle">{{ $t('client.services.nextDue') }}</UiText>
        <UiText as="span" size="xs" weight="medium" tone="muted">{{ formatDate(service.nextRenewalAt) }}</UiText>
      </UiStack>
      <UiStack gap="none">
        <UiText as="span" size="xs" tone="subtle">{{ $t('client.services.amount') }}</UiText>
        <UiText as="span" size="xs" weight="medium" tone="muted">{{ formatCurrency(service.price, currency) }}</UiText>
      </UiStack>
    </UiGrid>

    <ClientServicesUsageBar
      v-if="service.usage && hasUsageLimit(service.usage.diskLimitMb)"
      class="mt-4"
      :label="$t('client.services.diskUsage')"
      :used-mb="service.usage.diskUsedMb"
      :limit-mb="service.usage.diskLimitMb"
    />
  </UiLinkCard>
</template>

<script setup lang="ts">
/**
 * ServiceCard - one service of the list: name, domain, status (or a "needs setup" badge, never for a digital service), next
 * renewal date, recurring amount and a disk-usage bar when the server reported a limit. The whole card links to the service, or to
 * its setup wizard while it has no control-panel account yet.
 */
import { computed } from 'vue'
import { clientServicePath, clientServiceSetupPath } from '~/constants/clientRoutes'
import { Server } from 'lucide-vue-next'
import type { ClientService } from '~/types/clientservice'
import type { CurrencyInfo } from '~/utils/formatCurrency'
import { formatCurrency } from '~/utils/formatCurrency'
import { formatDate } from '~/utils/formatDate'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'
import { hasUsageLimit } from '~/utils/serviceUsage'

/** Props of ServiceCard. */
interface Props {
  /** The service the card shows. */
  service: ClientService
  /** What the account says the amounts are denominated in. */
  currency: CurrencyInfo
}

/** The props, read for the setup decision. */
const props = defineProps<Props>()

/** Whether the card says "needs setup" and links to the wizard; never for a digital service. */
const awaitsSetup = computed(() => serviceAwaitsSetup(props.service))
</script>
