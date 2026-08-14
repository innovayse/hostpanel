/**
 * Shared lifetime for the `refresh_token` cookie.
 *
 * This value has to track innovayse-sso's own refresh-token lifetime, set in
 * `Innovayse.SSO.API/Program.cs` as `SetRefreshTokenLifetime(TimeSpan.FromDays(30))`.
 * It had drifted: every one of the three places that store a refresh token wrote its
 * own `60 * 60 * 24 * 7`, so on the eighth day the browser dropped a credential the
 * SSO would still have accepted for another three weeks, and the user was sent back
 * through a sign-in they did not need.
 *
 * Defining it once is the point — three copies are what let it drift in the first
 * place. If the SSO's lifetime changes, change it here and nowhere else.
 */
export const REFRESH_TOKEN_MAX_AGE = 60 * 60 * 24 * 30
