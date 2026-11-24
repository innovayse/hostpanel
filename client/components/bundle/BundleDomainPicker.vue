<template>
  <UiCard data-testid="bundle-domain">
    <UiStack>
      <UiStack gap="none">
        <UiHeading :level="2" size="label" class="tracking-widest">{{ $t('bundles.domain.title') }}</UiHeading>
        <UiText size="sm" tone="muted" class="mt-1" data-testid="bundle-domain-years">{{ $t('domains.years', years) }}</UiText>
      </UiStack>

      <UiAlert v-if="error" variant="error" data-testid="bundle-domain-error">{{ error }}</UiAlert>

      <!-- The chosen domain -->
      <UiStack v-if="selected" direction="row" align="center" justify="between" gap="sm" data-testid="bundle-domain-selected">
        <UiStack gap="none" class="min-w-0">
          <UiText weight="semibold" class="break-all">{{ selected.name }}</UiText>
          <UiText size="xs" tone="muted">{{ selected.action === DomainOrderAction.Transfer ? $t('domains.tabTransfer') : $t('domains.tabRegister') }}</UiText>
        </UiStack>
        <UiButton variant="subtle" size="sm" icon="mdi:pencil-outline" data-testid="bundle-domain-change" @click="emit('clear')">
          {{ $t('bundles.domain.change') }}
        </UiButton>
      </UiStack>

      <template v-else>
        <UiChipTabs
          v-model="action"
          :tabs="actionTabs"
          :label="$t('bundles.domain.actionLabel')"
          testid="bundle-domain-tabs"
        />

        <!-- Register: search a name across the offered extensions -->
        <UiStack v-if="action === DomainOrderAction.Register" gap="sm">
          <UiForm spacing="none" @submit="emit('search', term)">
            <UiStack direction="row" wrap align="end" gap="xs">
              <UiInput
                v-model="term"
                :placeholder="$t('domains.placeholder')"
                :disabled="!hasZones"
                class="min-w-[200px] flex-1"
                testid="bundle-domain-input"
              />
              <UiButton type="submit" variant="primary" icon="mdi:magnify" :disabled="!hasZones" data-testid="bundle-domain-search">{{ $t('domains.search') }}</UiButton>
            </UiStack>
          </UiForm>

          <UiText v-if="!hasZones" size="sm" tone="muted" data-testid="bundle-domain-no-zones">{{ $t('aurora.domainSearch.noZones') }}</UiText>
          <UiSkeleton v-else-if="pending" :lines="3" data-testid="bundle-domain-pending" />
          <UiStack v-else gap="xs">
            <UiCard
              v-for="result in results"
              :key="result.name"
              padding="sm" class="flex flex-wrap items-center justify-between gap-3 shadow-none"
              :data-testid="`bundle-domain-result-${result.name}`"
            >
              <UiStack direction="row" align="center" gap="sm" class="min-w-0">
                <UiText class="break-all font-mono">{{ result.name }}</UiText>
                <UiBadge size="sm" rounded :variant="AVAILABILITY_TONES[result.status]">
                  {{ $t(`aurora.domainSearch.${result.status}`) }}
                </UiBadge>
              </UiStack>
              <UiStack direction="row" align="center" gap="sm">
                <UiText weight="bold">{{ result.price }}</UiText>
                <UiButton
                  v-if="result.status === DomainResultStatus.Available"
                  variant="primary"
                  size="sm"
                  icon="mdi:check"
                  :data-testid="`bundle-domain-pick-${result.name}`"
                  @click="emit('select', { action: DomainOrderAction.Register, name: result.name, eppCode: '' })"
                >
                  {{ $t('common.select') }}
                </UiButton>
                <UiButton
                  v-else-if="result.status === DomainResultStatus.Taken"
                  variant="subtle"
                  size="sm"
                  icon="mdi:swap-horizontal"
                  :data-testid="`bundle-domain-transfer-${result.name}`"
                  @click="startTransfer(result.name)"
                >
                  {{ $t('aurora.domainSearch.transfer') }}
                </UiButton>
              </UiStack>
            </UiCard>
          </UiStack>
        </UiStack>

        <!-- Transfer: the name and its authorization code -->
        <UiForm v-else spacing="sm" @submit="submitTransfer">
          <UiInput
            v-model="transferName"
            :label="$t('domains.transferDomainLabel')"
            :placeholder="$t('domains.transferDomainPlaceholder')"
            testid="bundle-domain-transfer-name"
          />
          <UiInput
            v-model="transferEpp"
            :label="$t('domains.transferAuthLabel')"
            :placeholder="$t('domains.transferEppPlaceholder')"
            testid="bundle-domain-transfer-epp"
          />
          <UiButton type="submit" variant="primary" full-width icon="mdi:swap-horizontal" data-testid="bundle-domain-transfer-submit">{{ $t('bundles.domain.useTransfer') }}</UiButton>
        </UiForm>
      </template>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The domain step of a bundle that includes one: register a name found by search, or transfer one
 * in with its authorization code. The registration period is fixed by the bundle and shown, never
 * chosen here.
 *
 * Presentational: the page owns the domain search and the pricing. This component reports the
 * typed term (`search`), the chosen domain (`select`) and the wish to pick another (`clear`).
 */
import { computed, ref } from 'vue'
import { AVAILABILITY_TONES } from '~/utils/domainStatus'
import type { DomainResult } from '~/types/domainresult'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { BundleDomainChoice } from '~/types/bundledomainchoice'
import type { ChipTab } from '~/types/chiptab'
import { DomainOrderAction } from '~/types/domainorderaction'

/** Props of BundleDomainPicker. */
interface Props {
  /** Years of registration the bundle includes; fixed, shown to the visitor. */
  years: number
  /** The availability rows of the last search. */
  results: DomainResult[]
  /** Whether a search is under way. */
  pending: boolean
  /** False when the operator has priced no extensions. */
  hasZones: boolean
  /** The domain the visitor chose; null while none is chosen. */
  selected: BundleDomainChoice | null
  /** Why the last choice was refused; empty when it was accepted. */
  error: string
}

defineProps<Props>()

/** Events of BundleDomainPicker. */
const emit = defineEmits<{
  /** The visitor searched for a name. */
  search: [term: string]
  /** The visitor chose a domain. */
  select: [choice: BundleDomainChoice]
  /** The visitor wants to pick another domain. */
  clear: []
}>()

/** Translator for the active locale. */
const { t } = useI18n()

/** Whether the domain is registered or transferred in. */
const action = ref<DomainOrderAction>(DomainOrderAction.Register)

/** The two ways to get the domain. */
const actionTabs = computed<ChipTab<DomainOrderAction>[]>(() => [
  { value: DomainOrderAction.Register, label: t('domains.tabRegister'), testid: 'bundle-domain-tab-register' },
  { value: DomainOrderAction.Transfer, label: t('domains.tabTransfer'), testid: 'bundle-domain-tab-transfer' },
])

/** The name typed in the search field. */
const term = ref('')

/** The name typed in the transfer form. */
const transferName = ref('')

/** The authorization code typed in the transfer form. */
const transferEpp = ref('')

/**
 * Moves a taken name to the transfer form.
 *
 * @param name The taken domain the visitor wants to transfer in.
 */
const startTransfer = (name: string): void => {
  transferName.value = name
  action.value = DomainOrderAction.Transfer
}

/** Reports the typed transfer as the chosen domain. */
const submitTransfer = (): void => {
  emit('select', { action: DomainOrderAction.Transfer, name: transferName.value.trim().toLowerCase(), eppCode: transferEpp.value.trim() })
}
</script>
