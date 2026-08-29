/**
 * `POST /api/contact` — the public contact form.
 *
 * Nothing is delivered here. This route hands the submission to the C# backend's
 * `POST /api/contact`, which sends the mail through the one `IEmailSender` the platform
 * configures and posts the enquiry to the operator's chat through `IContactNotifier`. Both used
 * to be done in this file, each with a credential of its own in this container's runtime config —
 * the mail relay's password, then the Telegram bot token — and neither has any other reason to
 * be in a browser-facing app. This route now holds no credential at all.
 *
 * It also used to wrap the send in `if (smtpHost && smtpUser && emailTo)` and answer
 * `{ success: true }` either way — so a deployment with incomplete configuration told every
 * visitor their message had arrived while nothing was sent and nothing was logged. Nothing here
 * may reintroduce that: a failure from the backend is rethrown with its status and its code
 * intact, and the browser never sees a success this route did not earn.
 *
 * The best-effort rule the Telegram relay had went with it rather than being dropped: the mail is
 * what decides the visitor's answer, and a chat outage is logged by the handler instead of
 * turning a delivered enquiry into a failure. It is enforced one layer further in now, where the
 * code can see whether the mail actually went.
 *
 * @module server/api/contact.post
 */

import type { H3Event } from 'h3'
import type { ContactMessage } from '~/types/contactmessage'

/**
 * Relays one contact-form submission to the backend.
 *
 * @param event - The incoming request.
 * @returns 204 No Content, and only once the backend has accepted the message.
 * @throws The backend's own error, status and `code` intact, when it refuses or cannot deliver.
 */
export default defineEventHandler(async (event: H3Event) => {
  const body = await readBody<ContactMessage>(event)

  // No field checks here. The backend validates every one of them and is the only side that
  // writes the wording; a copy in this file would be a second answer to the same question, in a
  // language this route cannot translate. See `api-driven-frontend.md`.
  // `unknown`, not `void`: the backend answers 204 with an empty body, and nothing here reads
  // the result -- what matters is that this line throws when the backend refuses.
  await internalApiCall<unknown>(event, '/contact', {
    method: 'POST',
    body: {
      name: body?.name,
      email: body?.email,
      phone: body?.phone,
      service: body?.service,
      message: body?.message,
      submittedAt: body?.timestamp,
    },
  })

  // 204, matching the backend. The old `{ success: true }` body was never read — the page
  // treats a resolved promise as sent — and a success flag is exactly the shape that let a
  // failed send be reported as a delivery.
  setResponseStatus(event, 204)
  return null
})
