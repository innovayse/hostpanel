---
title: Two-factor authentication
---

# Two-factor authentication

Two-factor authentication (2FA) asks for a 6-digit code from an authenticator app on your phone after you enter your password. Even if someone knows your password, they cannot sign in without your phone.

This feature is available with local accounts only. <Label type="local" />

<Callout type="warning">
Keep access to your authenticator app. There are no recovery codes — if you lose your phone and cannot access the app, you will need to contact support to regain access to your account.
</Callout>

## Turn on two-factor authentication

<Steps>
<li>

Go to **Account → Account Details** and find the **Two-Factor Authentication** card. It shows **2FA Disabled**.

</li>
<li>

Click **Enable**. The **Set Up Two-Factor Authentication** dialog opens.

</li>
<li>

Open your authenticator app and scan the QR code shown on screen. If you cannot scan it, click "Can't scan?" and enter the **Manual secret key** instead.

</li>
<li>

Enter the 6-digit code shown in your app into the **Authentication Code** field.

</li>
<li>

Click **Activate 2FA**. The card now shows **2FA Enabled**.

</li>
</Steps>

| Error | What it means |
|-------|--------------|
| `TWO_FACTOR_CODE_INVALID` | That code did not match. Check your authenticator app and try again. |

## Sign in with a two-factor code

After entering your email and password you are taken to the two-factor step.

<Steps>
<li>

Open your authenticator app and find the 6-digit code for this account.

</li>
<li>

Enter it in the **Authentication code** field and click **Verify and sign in**.

</li>
</Steps>

Click **Back to sign in** to go back and use a different account.

## Lost your phone

There are no recovery codes. If you lose access to your authenticator app, contact your hosting provider's support team — they can remove the two-factor requirement from your account so you can sign in again.

<Callout type="warning">
Keep access to your authenticator app at all times. Without it and without support help, you cannot sign in.
</Callout>

## Turn off two-factor authentication

<Steps>
<li>

Go to **Account → Account Details** and find the **Two-Factor Authentication** card. It shows **2FA Enabled**.

</li>
<li>

Click **Disable**. The **Turn off two-factor authentication** dialog opens.

</li>
<li>

Enter your **Current password** and a current code from your authenticator app.

</li>
<li>

Click **Turn off**. Every signed-in session ends, including the current one. Sign in again.

</li>
</Steps>
