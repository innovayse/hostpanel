<script setup lang="ts">
/** The visual style of the callout box. */
type CalloutType = 'note' | 'warning' | 'danger' | 'tip'

/** Props accepted by the Callout component. */
const props = withDefaults(defineProps<{
  /** Visual style; defaults to `note` (blue info box). */
  type?: CalloutType
}>(), {
  type: 'note',
})

/** Icon character rendered inside the coloured badge for each callout type. */
const icons: Record<CalloutType, string> = {
  note: 'ℹ',
  warning: '⚠',
  danger: '✕',
  tip: '✓',
}
</script>

<template>
  <div :class="['callout', `callout--${props.type}`]">
    <span class="callout__icon" aria-hidden="true">{{ icons[props.type] }}</span>
    <div class="callout__body">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.callout {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  border-left: 4px solid;
  border-radius: 0 8px 8px 0;
  padding: 14px 16px;
  margin: 16px 0;
  font-size: 0.95rem;
  line-height: 1.6;
}

.callout__icon {
  font-size: 1rem;
  flex-shrink: 0;
  margin-top: 1px;
  font-style: normal;
}

.callout__body :deep(> *:first-child) {
  margin-top: 0;
}

.callout__body :deep(> *:last-child) {
  margin-bottom: 0;
}

/* note — blue */
.callout--note {
  border-color: var(--vp-c-brand-1);
  background-color: var(--vp-c-brand-soft);
  color: var(--vp-c-text-1);
}
.callout--note .callout__icon {
  color: var(--vp-c-brand-1);
}

/* tip — green */
.callout--tip {
  border-color: var(--vp-c-tip-2, #10b981);
  background-color: var(--vp-c-tip-soft, rgba(16, 185, 129, 0.1));
  color: var(--vp-c-text-1);
}
.callout--tip .callout__icon {
  color: var(--vp-c-tip-2, #10b981);
}

/* warning — yellow */
.callout--warning {
  border-color: var(--vp-c-warning-2, #f59e0b);
  background-color: var(--vp-c-warning-soft, rgba(245, 158, 11, 0.1));
  color: var(--vp-c-text-1);
}
.callout--warning .callout__icon {
  color: var(--vp-c-warning-2, #f59e0b);
}

/* danger — red */
.callout--danger {
  border-color: var(--vp-c-danger-2, #ef4444);
  background-color: var(--vp-c-danger-soft, rgba(239, 68, 68, 0.1));
  color: var(--vp-c-text-1);
}
.callout--danger .callout__icon {
  color: var(--vp-c-danger-2, #ef4444);
}
</style>
