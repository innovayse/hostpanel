<template>
  <UiImage
    v-if="iconSrc"
    :src="iconSrc"
    :alt="plugin?.name ?? ''"
    :width="size"
    :height="size"
    :fit="UiImageFit.Contain"
    loading="eager"
    unoptimised
    class="flex-shrink-0"
    testid="service-panel-plugin-icon"
    @error="failed = true"
  />
  <component :is="fallback" v-else :size="size" :stroke-width="2" class="flex-shrink-0" />
</template>

<script setup lang="ts">
/**
 * ServicePanelIcon. The picture on an "Open panel" control: the icon of the plugin that serves the
 * service's server, named after the plugin, or the generic icon the control already had when no plugin
 * serves the module, the plugin ships no icon, or the picture cannot be loaded.
 */
import { computed, ref } from 'vue'
import type { Component } from 'vue'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import type { ClientService } from '~/types/clientservice'
import { UiImageFit } from '~/utils/ui/enums'

/** Props of ServicePanelIcon. */
interface Props {
  /** The service whose panel the control opens; only its server's plugin is read. */
  service: Pick<ClientService, 'server'>
  /** The generic icon drawn when no plugin picture is available. */
  fallback: Component
  /** Edge of the square the picture or icon is drawn in, in pixels. */
  size?: number
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  size: 14,
})

/** The store that owns the address of the plugin icon route. */
const store = useServiceManagementStore()

/** True once the browser failed to load the plugin's picture, so the generic icon takes its place. */
const failed = ref(false)

/** The plugin that serves the server's module; null when none does. */
const plugin = computed(() => props.service.server?.panelPlugin ?? null)

/** Address of the plugin's picture; undefined when there is none to draw. */
const iconSrc = computed<string | undefined>(() =>
  plugin.value?.hasIcon && !failed.value ? store.panelIconUrl(plugin.value.id) : undefined)
</script>
