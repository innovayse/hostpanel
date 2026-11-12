/**
 * Tests for PluginChatSteps: a row per step carrying its state, the stopped note, and how many steps are done.
 *
 * @module tests/components/plugin/ChatSteps.test
 */

import { describe, expect, it } from 'vitest'
import PluginChatSteps from '~/components/plugin/ChatSteps.vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { PluginChatStepState } from '~/types/pluginchatstepstate'

describe('PluginChatSteps', () => {
  it('draws a row per step with its state and counts the finished ones', () => {
    const wrapper = mountWithKit(PluginChatSteps, {
      props: {
        steps: [
          { key: 'a', label: 'Done one', state: PluginChatStepState.Done },
          { key: 'b', label: 'Running one', state: PluginChatStepState.Running },
          { key: 'c', label: 'Stopped one', state: PluginChatStepState.Stopped },
        ],
      },
      mocks: { $t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${params.done}/${params.total}` : key) },
    })

    expect(wrapper.findAll('[data-testid="plugin-chat-step"]').map(row => row.attributes('data-state'))).toEqual(['done', 'running', 'stopped'])
    expect(wrapper.findAll('[data-testid="plugin-chat-step-stopped"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-chat-steps-count"]').text()).toBe('plugins.chatStepsCount 1/3')
  })
})
