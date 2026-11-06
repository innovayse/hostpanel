<template>
  <UiStack gap="sm" class="flex-1 md:min-h-0" data-testid="plugin-preview">
    <UiText v-if="block.title" as="p" weight="semibold" data-testid="plugin-preview-title">{{ block.title }}</UiText>
    <UiStack direction="row" gap="sm" align="center" wrap data-testid="plugin-preview-bar">
      <UiSegmentedControl
        v-if="block.sources.length > 1"
        v-model="sourceId"
        :options="sourceOptions"
        :label="$t('plugins.previewSource')"
        size="sm"
        testid="plugin-preview-source"
      />
      <UiButton
        v-if="canSelect"
        :variant="selection?.active.value ? UiButtonVariant.Primary : UiButtonVariant.Outline"
        size="sm"
        :aria-pressed="selection?.active.value"
        data-testid="plugin-preview-select"
        @click="toggleMode"
      >{{ selection?.active.value ? $t('plugins.previewSelectDone') : $t('plugins.previewSelect') }}</UiButton>
      <UiText
        v-if="address"
        size="xs"
        tone="muted"
        class="min-w-40 flex-1 truncate rounded-md border border-gray-200 bg-gray-50 px-2.5 py-1.5 font-mono dark:border-white/10 dark:bg-white/5"
        data-testid="plugin-preview-address"
      >{{ address }}</UiText>
      <UiSegmentedControl
        v-model="size"
        :options="sizeOptions"
        :label="$t('plugins.previewSize')"
        size="sm"
        testid="plugin-preview-size"
      />
      <UiLink
        v-if="showFrame && address"
        :href="address"
        external
        :underline="false"
        class="rounded-md border border-gray-200 px-3 py-1 text-xs dark:border-white/10"
        testid="plugin-preview-open"
      >{{ $t('plugins.previewOpen') }}</UiLink>
    </UiStack>
    <UiText v-if="canSelect && selection?.active.value" size="sm" role="status" data-testid="plugin-preview-select-hint">{{ $t('plugins.previewSelectHint') }}</UiText>
    <UiStack align="center" class="relative min-h-[420px] flex-1" data-testid="plugin-preview-canvas">
      <UiCard
        v-if="showFrame && address"
        padding="none"
        :class="['relative min-h-[420px] w-full flex-1 overflow-hidden shadow-lg', size === PluginPreviewSize.Phone ? 'max-w-[375px]' : 'max-w-[980px]']"
        data-testid="plugin-preview-viewport"
      >
        <UiPreviewFrame ref="frame" :key="frameKey" :src="address" :title="source?.label ?? ''" testid="plugin-preview-frame" />
      </UiCard>
      <UiAlert v-else-if="insecureContext" :variant="UiAlertVariant.Warning" class="w-full" data-testid="plugin-preview-insecure">{{ $t('plugins.previewInsecure') }}</UiAlert>
      <UiText v-else-if="!loading" size="sm" tone="muted" data-testid="plugin-preview-empty">{{ $t('plugins.previewEmpty') }}</UiText>
      <UiBadge v-if="showFrame && noteText" class="absolute right-3 top-3 !bg-gray-900 !text-white shadow-lg" data-testid="plugin-preview-note">{{ noteText }}</UiBadge>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginPreviewBlock - one address read from a route, in a sandboxed frame, with a source and a size switch.
 *
 * The frame shows only an address `pluginFrameAddress` admits against the page's `frameOrigins`, and only in a document
 * the panel's server wrote the narrow `frame-src` for this page. A CSP belongs to the document, so a router move here, from
 * another page of the same plugin too, loads the page once in full; the session flag keeps that to one try per page, so a
 * document that never matches shows nothing rather than reloading for ever. Anything refused shows no frame, no link and no sentence about why,
 * with one exception that is no refusal: a browser may hide `Server-Timing` outside a secure context (the standard allows
 * it, Chromium does not), and then over plain http the document's policy cannot be read at all. The frame stays off and a
 * warning says why, since no reload could change it. Where the entry is readable, plain http works like any other address.
 *
 * The address of a site does not change when its content does, so a refresh of this block draws a new frame once its
 * read has answered, or, when the view names a `note`, once the note changed: an address that also changed loads once, and the sandbox stays as it is. The optional `note` reads
 * an object of sentences by source id, written over the frame; it is text, and only ever beside a frame.
 *
 * With a `selection` the reader can point at elements in the frame. The panel cannot read an opaque-origin frame, so a small
 * script there reports what was pointed at, and any script in the frame could send the same thing: a message is a claim. It
 * is taken only from this frame's window (`event.source`; the origin is `null` and proves nothing), only in the exact versioned
 * shape, only naming the release the plugin says is on the preview, only on a source the view lets select, and a toggle only
 * while the mode is on. The select button appears only after the frame said it is ready, so a site without the script, the
 * live one above all, offers none. What is then selected is what the plugin's server confirms. Messages to the frame go to
 * target `*`, since an opaque origin cannot be named, so they carry the mode and element ids and nothing else, and only to a
 * frame that said it is ready. The selection is this block's own: another preview on the page neither sees nor clears it, and
 * a preview that declares none holds none, listens to nothing, posts nothing and clears nothing.
 */
import { usePluginSelection } from '~/composables/usePluginSelection'
import { usePluginSource, usePluginView } from '~/composables/usePluginView'
import { PLUGIN_SELECTION_MESSAGE_TYPE, PLUGIN_SELECTION_MESSAGE_VERSION } from '~/constants/pluginSelection'
import type { PluginPreviewBlock } from '~/types/pluginblock'
import { PluginPreviewSize } from '~/types/pluginpreviewsize'
import { PluginSelectionEvent } from '~/types/pluginselectionevent'
import { parsePluginSelectionMessage, pluginDocumentLoaded, pluginFrameAddress, readPluginField } from '~/utils/plugins'
import { UiAlertVariant, UiButtonVariant } from '~/utils/ui/enums'

/** Props of PluginPreviewBlock. */
interface Props {
  /** The block. */
  block: PluginPreviewBlock
}

/** The session key of the one full load tried for a page. */
const RELOAD_KEY_PREFIX = 'plugin-frame-reload:'

/** The props. */
const props = defineProps<Props>()

/** The translator of the size switch. */
const { t } = useI18n()
/** The view's context: the frame origins. */
const context = usePluginView()
/** The address of the page on screen, which the document must have been served for. */
const route = useRoute()

/** The view's selections, which this block joins only when it declares one. */
const selections = usePluginSelection()
/** This block's own selection; null when the view declares none for it. */
const selection = props.block.selection ? selections.open() : null
/** The frame on screen. */
const frame = ref<{ contentWindow: () => Window | null } | null>(null)
/** Whether the frame on screen said it can select. */
const frameReady = ref(false)

/** The selected source's id; the first to begin with. */
const sourceId = ref(props.block.sources[0]?.id ?? '')
/** The selected size. */
const size = ref<PluginPreviewSize>(props.block.size === PluginPreviewSize.Phone ? PluginPreviewSize.Phone : PluginPreviewSize.Desktop)
/** Whether this document may hold the frame. */
const documentReady = ref(false)
/** Whether an admitted address cannot be framed because this insecure page's browser hides the document's timing entries. */
const insecureContext = ref(false)

/** The selected source. */
const source = computed(() => props.block.sources.find(candidate => candidate.id === sourceId.value))

/** The source switch's options. */
const sourceOptions = computed(() => props.block.sources.map(candidate => ({
  value: candidate.id,
  label: candidate.label,
  testid: `plugin-preview-source-${candidate.id}`,
})))

/** The size switch's options. */
const sizeOptions = computed(() => [
  { value: PluginPreviewSize.Desktop, label: t('plugins.previewDesktop'), testid: 'plugin-preview-size-desktop' },
  { value: PluginPreviewSize.Phone, label: t('plugins.previewPhone'), testid: 'plugin-preview-size-phone' },
])

/** The selected source's value. */
const { value, loading, reload } = usePluginSource(
  () => (source.value ? { route: source.value.route, ...(source.value.field ? { field: source.value.field } : {}) } : undefined),
  () => props.block.id,
)

/** The block's note source: read only when the view names one. */
const { value: notes } = usePluginSource(() => props.block.note, () => props.block.id)

/** The note of the selected source; empty when there is none or it is not text. */
const noteText = computed(() => {
  const note = readPluginField(notes.value, sourceId.value)
  return typeof note === 'string' ? note : ''
})

/** The block's release source: read only when the view lets the reader select. */
const { value: release } = usePluginSource(
  () => (props.block.selection ? { route: props.block.selection.release.route, field: props.block.selection.release.field } : undefined),
  () => props.block.id,
)

/** The release on the preview, as the plugin's server says; empty when unknown. */
const expectedRelease = computed(() => (typeof release.value === 'string' ? release.value : ''))

/** Whether the source on screen is one the view lets select. */
const selectableSource = computed(() => props.block.selection?.sources.includes(sourceId.value) === true)

/** How many times a refresh of this block has drawn a new frame. */
const revision = ref(0)
/** Whether a refresh was asked for and its read has not answered yet. */
let refreshing = false

/** The frame's identity: a new value is a new frame, which is what makes the browser load the address again. */
const frameKey = computed(() => `${address.value}#${revision.value}`)

/** The address that may be framed; null when the value is refused. */
const address = computed(() => pluginFrameAddress(value.value, context.frameOrigins, window.location.origin))

/** Whether the frame may be drawn. */
const showFrame = computed(() => address.value !== null && documentReady.value)

/** Whether the select button is offered. */
const canSelect = computed(() => selectableSource.value && showFrame.value && frameReady.value && expectedRelease.value !== '')

/**
 * Tells the frame the mode and what is selected.
 *
 * @returns Nothing.
 */
const tellFrame = (): void => {
  if (!selection || !frameReady.value) {
    return
  }
  // '*' because an opaque origin cannot be named as a target; this message holds no secret and no customer data.
  frame.value?.contentWindow()?.postMessage({
    type: PLUGIN_SELECTION_MESSAGE_TYPE,
    version: PLUGIN_SELECTION_MESSAGE_VERSION,
    event: PluginSelectionEvent.State,
    active: selection.active.value,
    selected: selection.chips.value.map(chip => chip.id),
  }, '*')
}

/**
 * Takes one message from a window. Everything failing a check is dropped without a word: the sender may be anything.
 *
 * @param event - The message.
 * @returns Nothing.
 */
const onFrameMessage = (event: MessageEvent): void => {
  const own = frame.value?.contentWindow()
  if (!selection || !props.block.selection || !own || event.source !== own || !selectableSource.value) {
    return
  }
  const message = parsePluginSelectionMessage(event.data, expectedRelease.value)
  if (!message) {
    return
  }
  if (message.event === PluginSelectionEvent.Ready) {
    frameReady.value = true
    tellFrame()
  } else if (selection.active.value && message.event === PluginSelectionEvent.Toggle) {
    void selection.toggle(props.block.selection.route, expectedRelease.value, message.id)
  } else if (selection.active.value && message.event === PluginSelectionEvent.Exit) {
    selection.active.value = false
  }
}

/**
 * Leaves the mode on Esc when the focus is in the panel; inside the frame the script reports it.
 *
 * @param event - The key.
 * @returns Nothing.
 */
const onKeydown = (event: KeyboardEvent): void => {
  if (selection && event.key === 'Escape' && selection.active.value) {
    selection.active.value = false
  }
}

/**
 * Turns the mode on or off.
 *
 * @returns Nothing.
 */
const toggleMode = (): void => {
  if (!selection) {
    return
  }
  selection.active.value = !selection.active.value
  if (selection.active.value) {
    selection.focus()
  }
}

/**
 * Checks the document once an address is admitted: this one holds the frame, or the page is loaded in full once.
 *
 * @returns Nothing.
 */
const checkDocument = (): void => {
  // Asked afresh on every move: a frame this document could hold on one page says nothing about the next.
  documentReady.value = false
  insecureContext.value = false
  if (address.value === null) {
    return
  }
  const entry = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined
  if (window.isSecureContext === false && !entry?.serverTiming?.length) {
    insecureContext.value = true
    return
  }
  const reloadKey = `${RELOAD_KEY_PREFIX}${route.path}`
  if (pluginDocumentLoaded(entry, route.path)) {
    sessionStorage.removeItem(reloadKey)
    documentReady.value = true
    return
  }
  if (sessionStorage.getItem(reloadKey) === null) {
    sessionStorage.setItem(reloadKey, '1')
    window.location.assign(window.location.href)
  }
}

watch([address, () => route.path], checkDocument)
onMounted(() => {
  if (selection) {
    window.addEventListener('message', onFrameMessage)
    window.addEventListener('keydown', onKeydown)
  }
})
onBeforeUnmount(() => {
  if (selection) {
    window.removeEventListener('message', onFrameMessage)
    window.removeEventListener('keydown', onKeydown)
    selections.close(selection)
  }
})
if (selection) {
  // What the frame draws follows what the panel holds, never the other way round.
  watch([selection.active, selection.chips], tellFrame, { deep: true })
}
// A new frame says again that it is ready, or says nothing and offers no selection.
watch(() => frameKey.value, () => {
  frameReady.value = false
})
// A selection names paths of one release; a new one may hold other content at them, so nothing carries over.
watch(expectedRelease, (next, last) => {
  if (!selection) {
    return
  }
  selection.release.value = next
  if (last !== '' && next !== last) {
    selection.clear()
    frameReady.value = false
  }
})
watch(sourceId, () => {
  selection?.clear()
  frameReady.value = false
})
watch(sourceId, reload)
watch(() => (props.block.id ? context.refreshes.value[props.block.id] ?? 0 : 0), () => {
  refreshing = true
})
watch(loading, (reading) => {
  if (!reading && refreshing) {
    refreshing = false
    // A note says which content the frame holds, so with one only a changed note draws a new frame: a follow-up reads
    // this block every few seconds, and a site that reloads each time could not be looked at.
    if (!props.block.note) {
      revision.value += 1
    }
  }
})
watch([sourceId, noteText], ([id, note], [lastId, lastNote]) => {
  if (id === lastId && note !== lastNote && lastNote !== '') {
    revision.value += 1
  }
})
</script>
