/**
 * When a service cancellation takes effect.
 *
 * These are **wire values**, not display text: each string is the exact member name of the
 * backend `CancellationType` enum, which `CancelServiceHandler` feeds to `Enum.Parse` and
 * `CancelServiceValidator` matches case-sensitively. The sentence a person reads is a
 * separate thing and comes from `client.services.cancel*` in the locale files.
 */
export type CancellationType = 'Immediate' | 'EndOfBillingPeriod'
