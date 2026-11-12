/**
 * What a read that the server fails costs on the wire, shared by the widget tests that count the requests of a failing read.
 *
 * @module tests/helpers/failedRead
 */

/** How many times a read is asked for when the first answer is a server failure: the transport asks once more. */
export const FAILED_READ_REQUESTS = 2
