<script setup lang="ts">
/**
 * Settings index: every settings section as a glass card, grouped by what it controls.
 *
 * The catalogue is {@link SETTINGS_INDEX}; the screen only renders it. Each card is one link to its
 * section: an icon, the section's name and a line on what it holds. The grid is one column on
 * phones, two on tablets and three on wide screens.
 */
import { useI18n } from 'vue-i18n'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { SETTINGS_INDEX } from '@/lib/settingsIndex'

const { t } = useI18n()
</script>

<template>
  <UiStack gap="none" data-testid="settings-index-view">
    <UiPageHeader :title="t('settings.index.title')" :subtitle="t('settings.index.subtitle')" />
    <!-- The installed version, quietly under the title; absent when it could not be loaded. -->
    <VersionLabel testid="settings-version" class="-mt-4 mb-4" />

    <UiStack v-for="group in SETTINGS_INDEX" :key="group.group" gap="sm" class="mb-8" :data-testid="`settings-group-${group.group}`">
      <UiHeading :level="2" variant="section">
        {{ t(`settings.index.groups.${group.group}`) }}
      </UiHeading>
      <UiGrid :columns="3">
        <UiCard v-for="entry in group.entries" :key="entry.item" class="transition-colors hover:bg-surface-hover">
          <UiLink :to="entry.to" class="block h-full rounded-card p-5" :data-testid="`settings-card-${entry.item}`">
            <UiStack direction="row" align="start" gap="md">
              <UiIcon :markup="entry.icon" class="mt-0.5 text-text-accent" />
              <UiStack gap="xs" class="min-w-0">
                <UiText tone="primary" size="lg" class="font-semibold">
                  {{ t(`settings.index.items.${entry.item}.title`) }}
                </UiText>
                <UiText tone="secondary" size="md">
                  {{ t(`settings.index.items.${entry.item}.description`) }}
                </UiText>
              </UiStack>
            </UiStack>
          </UiLink>
        </UiCard>
      </UiGrid>
    </UiStack>
  </UiStack>
</template>
