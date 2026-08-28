/** The fields `POST /api/portal/auth/register` needs to create a client account. */
export interface RegisterPayload {
  /** Given name. */
  firstname: string
  /** Family name. */
  lastname: string
  /** Account email — also the sign-in identifier. */
  email: string
  /** Chosen password, in the clear; the transport is what protects it. */
  password: string
}
