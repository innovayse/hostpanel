<script setup lang="ts">
/**
 * Lists removed plugins whose data the server kept, each with a "Purge data" action.
 *
 * Removing a plugin keeps its settings and stored data so a later install picks up where it left
 * off; purging deletes that data for good. The button only asks: the screen confirms first.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginKeptData. */
defineProps<{
  /** Ids of the removed plugins whose data is still kept. */
  ids: string[]
  /** Id of the plugin whose purge is in flight, null when none. */
  purgingId: string | null
}>()

/** Events emitted by PluginKeptData. */
const emit = defineEmits<{
  /** Emitted with the plugin id when the admin asks to purge its data. */
  purge: [id: string]
}>()
</script>

<template>
  <UiStack gap="sm" data-testid="plugins-kept-data">
    <UiHeading :level="2" variant="section">{{ t('plugins.keptData.title') }}</UiHeading>
    <UiText tone="muted" size="sm">{{ t('plugins.keptData.description') }}</UiText>
    <UiCard v-for="id in ids" :key="id" padding="sm" :data-testid="`plugin-kept-${id}`">
      <UiStack direction="row" align="center" justify="between" gap="md">
        <UiText tone="primary" class="font-mono">{{ id }}</UiText>
        <UiButton
          type="button"
          variant="danger"
          size="sm"
          :disabled="purgingId !== null"
          :data-testid="`plugin-purge-${id}`"
          @click="emit('purge', id)"
        >
          {{ purgingId === id ? t('plugins.keptData.purging') : t('plugins.keptData.purge') }}
        </UiButton>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
