/**
 * Fixtures for `/api/auth/*`: the deployment mode, the first-run flag and the signed-in operator.
 */
import type { AuthModeResponse, LocalLoginResponse, MeResponse, SetupRequiredResponse } from '../../src/types/auth'

/** A local-mode deployment: the only mode that shows the email/password form. */
export const localMode: AuthModeResponse = { mode: 'local' }

/** An installation that already has an Admin, so the login page offers no bootstrap. */
export const setupDone: SetupRequiredResponse = { required: false }

/** The operator `GET /api/auth/me` answers with once a session exists. */
export const adminMe: MeResponse = {
  email: 'admin@example.test',
  firstName: 'Ada',
  lastName: 'Lovelace',
  roles: ['Admin'], emailVerified: true }

/** Credentials the stubbed `POST /api/auth/login` accepts. */
export const validCredentials = { email: 'admin@example.test', password: 'correct-horse' } as const

/** Body of a successful local sign-in with no second factor. */
export const loginOk: LocalLoginResponse = { accessToken: 'e2e-token', expiresIn: 900 }

/** The problem+json body the API sends for rejected credentials. */
export const loginRejected = {
  type: 'auth.invalid_credentials',
  title: 'Unauthorized',
  status: 401,
  detail: 'The email or password is not correct.',
} as const
