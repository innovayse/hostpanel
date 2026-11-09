<template>
  <UiPopover
    v-if="apps.length > 0"
    v-model="open"
    panel-class="w-[520px]"
  >
    <template #trigger="{ toggle }">
      <UiButton
        variant="bare"
        :title="t('aurora.apps.title')"
        :aria-label="t('aurora.apps.title')"
        :aria-expanded="open"
        aria-haspopup="menu"
        data-testid="app-launcher-trigger"
        class="h-10 w-10 rounded-xl border border-line2 text-tx2"
        :class="open ? 'bg-acbg' : ''"
        @click="toggle"
      >
        <Icon name="lucide:layout-grid" class="h-[19px] w-[19px]" />
      </UiButton>
    </template>

    <template #default="{ close }">
      <UiGrid cols="3" gap="xs">
        <UiButton
          v-for="app in apps"
          :key="app.id"
          variant="bare"
          :href="app.url"
          role="menuitem"
          full-width
          :data-testid="`app-launcher-${app.id}`"
          class="rounded-xl px-3 py-3 text-tx hover:bg-acbg"
          @click="close"
        >
          <UiStack gap="xs" align="start" class="w-full min-w-0 text-left">
            <UiTile :class="[tileClass(app.id), 'font-mono text-tile-ink']">{{ app.tag }}</UiTile>
            <UiText as="span" size="sm" weight="semibold" tone="inherit">{{ app.label }}</UiText>
            <UiText as="span" size="xs" tone="inherit" class="leading-snug text-mut2">{{ app.desc }}</UiText>
          </UiStack>
        </UiButton>
      </UiGrid>
    </template>
  </UiPopover>
</template>

<script setup lang="ts">
/**
 * aurora app launcher.
 *
 * Renders nothing at all unless the deployment has apps to offer, so the header
 * of a stock install is unchanged. The design's "Manage all services" footer
 * link is left out: it points at `#` in the design, and there is no such page.
 *
 * Each tile gets its gradient from a named token class keyed by app id; the composable's own
 * `tint` string (a CSS gradient) is not used here because it could only be applied through an
 * inline style.
 */

import { PortalAppId } from '~/types/portalappid'

const { t } = useI18n()
/** Apps the deployment offers; empty hides the launcher. */
const { apps } = usePortalApps()

/** Whether the panel is open (v-model of the popover). */
const open = ref(false)

/** Tile gradient token per known app id; an app this map does not know gets the brand gradient. */
const TILE_CLASSES: Readonly<Record<PortalAppId, string>> = {
  [PortalAppId.Account]: 'bg-tile-sky',
  [PortalAppId.Tasks]: 'bg-tile-mint',
  [PortalAppId.Erp]: 'bg-tile-lilac',
  [PortalAppId.Hostpanel]: 'bg-tile-pink',
  [PortalAppId.Sheets]: 'bg-tile-cyan',
  [PortalAppId.Mail]: 'bg-tile-indigo',
  [PortalAppId.Docs]: 'bg-tile-ice',
  [PortalAppId.Calendar]: 'bg-tile-rose',
}

/**
 * Gradient class for one app's tile.
 *
 * @param appId - Id of the app, as `usePortalApps` lists it.
 * @returns A literal `bg-tile-*` class, or the brand gradient for an unknown id.
 */
const tileClass = (appId: PortalAppId): string => TILE_CLASSES[appId] ?? 'bg-brand'
</script>
