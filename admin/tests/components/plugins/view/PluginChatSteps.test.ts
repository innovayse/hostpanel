/**
 * Unit tests for PluginChatSteps: a row per step carrying its state, the stopped note, and how many steps are done.
 */
import { describe, expect, it } from 'vitest'
import PluginChatSteps from '@/components/plugins/view/PluginChatSteps.vue'
import { PluginChatStepState } from '@/types/pluginview'
import { mountWithApp } from '../../../helpers/mount'

describe('PluginChatSteps', () => {
  it('draws a row per step with its state and counts the finished ones', () => {
    const wrapper = mountWithApp(PluginChatSteps, {
      props: {
        steps: [
          { key: 'a', label: 'Done one', state: PluginChatStepState.Done },
          { key: 'b', label: 'Running one', state: PluginChatStepState.Running },
          { key: 'c', label: 'Stopped one', state: PluginChatStepState.Stopped },
        ],
      },
    })

    expect(wrapper.findAll('[data-testid="plugin-chat-step"]').map((row) => row.attributes('data-state'))).toEqual(['done', 'running', 'stopped'])
    expect(wrapper.findAll('[data-testid="plugin-chat-step-stopped"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-chat-steps-count"]').text()).toBe('1 of 3 done')
  })

  it('spins only a running step, and stops the spin for reduced motion', () => {
    const wrapper = mountWithApp(PluginChatSteps, {
      props: { steps: [{ key: 'a', label: 'Working', state: PluginChatStepState.Running }, { key: 'b', label: 'Finished', state: PluginChatStepState.Done }] },
    })
    const [running, done] = wrapper.findAll('[data-testid="plugin-chat-step"] svg')

    expect(running?.classes()).toContain('animate-spin')
    expect(running?.classes()).toContain('motion-reduce:animate-none')
    expect(done?.classes()).not.toContain('animate-spin')
  })
})
