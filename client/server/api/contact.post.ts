/**
 * Contact Form API - Sends messages to Telegram and Email
 */
import nodemailer from 'nodemailer'

interface ContactFormData {
  name: string
  email: string
  phone?: string
  service?: string
  message: string
  timestamp?: string
}

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const body = await readBody<ContactFormData>(event)

  // Validate required fields
  if (!body.name || !body.email || !body.message) {
    throw createError({
      statusCode: 400,
      message: 'Missing required fields'
    })
  }

  // Format message for Telegram (Armenian)
  const telegramMessage = `
🔔 *Նոր հայտ կայքից*

👤 *Անուն:* ${escapeMarkdown(body.name)}
📧 *Էլ. փոստ:* ${escapeMarkdown(body.email)}
${body.phone ? `📱 *Հեռախոս:* ${escapeMarkdown(body.phone)}` : ''}
${body.service ? `🛠 *Ծառայություն:* ${escapeMarkdown(body.service)}` : ''}

💬 *Հաղորդագրություն:*
${escapeMarkdown(body.message)}

───────────────
📅 ${body.timestamp || new Date().toISOString()}
  `.trim()

  try {
    // Send to Telegram
    await $fetch(
      `https://api.telegram.org/bot${config.telegramBotToken}/sendMessage`,
      {
        method: 'POST',
        body: {
          chat_id: config.telegramChatId,
          text: telegramMessage,
          parse_mode: 'Markdown'
        }
      }
    )

    // Send Email
    if (config.smtpHost && config.smtpUser && config.emailTo) {
      const transporter = nodemailer.createTransport({
        host: config.smtpHost,
        port: Number(config.smtpPort) || 587,
        secure: config.smtpPort === '465',
        auth: {
          user: config.smtpUser,
          pass: config.smtpPassword
        }
      })

      const emailHtml = `
        <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
          <h2 style="color: #0ea5e9; border-bottom: 2px solid #0ea5e9; padding-bottom: 10px;">
            🔔 Նոր հայտ կայքից
          </h2>

          <table style="width: 100%; border-collapse: collapse; margin-top: 20px;">
            <tr style="background-color: #f8f9fa;">
              <td style="padding: 12px; border: 1px solid #dee2e6; font-weight: bold; width: 150px;">
                👤 Անուն
              </td>
              <td style="padding: 12px; border: 1px solid #dee2e6;">
                ${body.name}
              </td>
            </tr>
            <tr>
              <td style="padding: 12px; border: 1px solid #dee2e6; font-weight: bold;">
                📧 Էլ. փոստ
              </td>
              <td style="padding: 12px; border: 1px solid #dee2e6;">
                <a href="mailto:${body.email}">${body.email}</a>
              </td>
            </tr>
            ${body.phone ? `
            <tr style="background-color: #f8f9fa;">
              <td style="padding: 12px; border: 1px solid #dee2e6; font-weight: bold;">
                📱 Հեռախոս
              </td>
              <td style="padding: 12px; border: 1px solid #dee2e6;">
                ${body.phone}
              </td>
            </tr>
            ` : ''}
            ${body.service ? `
            <tr${body.phone ? '' : ' style="background-color: #f8f9fa;"'}>
              <td style="padding: 12px; border: 1px solid #dee2e6; font-weight: bold;">
                🛠 Ծառայություն
              </td>
              <td style="padding: 12px; border: 1px solid #dee2e6;">
                ${body.service}
              </td>
            </tr>
            ` : ''}
          </table>

          <div style="margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #0ea5e9;">
            <p style="margin: 0; font-weight: bold; margin-bottom: 10px;">💬 Հաղորդագրություն:</p>
            <p style="margin: 0; white-space: pre-wrap;">${body.message}</p>
          </div>

          <div style="margin-top: 20px; padding: 10px; background-color: #e9ecef; font-size: 12px; color: #6c757d; text-align: center;">
            📅 ${body.timestamp || new Date().toISOString()}
          </div>
        </div>
      `

      await transporter.sendMail({
        from: `"Innovayse Contact Form" <${config.smtpUser}>`,
        to: config.emailTo,
        subject: `🔔 Նոր հայտ - ${body.name}`,
        html: emailHtml
      })
    }

    return {
      success: true,
      message: 'Message sent successfully'
    }
  } catch (error) {
    console.error('Error sending message:', error)
    throw createError({
      statusCode: 500,
      message: 'Failed to send message'
    })
  }
})

// Escape special Markdown characters (only the necessary ones)
function escapeMarkdown(text: string): string {
  return text.replace(/[_*[\]()~`>#+=|{}!]/g, '\\$&')
}
