/**
 * Factory for team members in the backend's `ClientUserDto` shape, so the team tests build the same
 * wire object `GET /api/portal/client/users` sends instead of each spelling out eight fields.
 *
 * @module tests/helpers/teamMember
 */

import type { TeamMember } from '~/types/teammember'

/**
 * Builds a removable member who never signed in.
 *
 * @param overrides - Fields to set on top of the defaults.
 * @returns The member.
 */
export const makeTeamMember = (overrides: Partial<TeamMember> = {}): TeamMember => ({
  userId: '2',
  firstName: 'Hal',
  lastName: 'Helper',
  email: 'helper@example.com',
  isOwner: false,
  permissions: 4,
  lastLoginAt: null,
  createdAt: '2025-06-01T00:00:00Z',
  ...overrides
})
