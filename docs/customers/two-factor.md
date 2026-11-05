---
title: Two-factor authentication
description: "Turn two-factor authentication on or off for a local Hostpanel account, sign in with a 6-digit code and recover access when you lose your phone."
keywords: "two-factor authentication, 2fa, authenticator app, lost phone"
---

# Two-factor authentication

Two-factor authentication (2FA) asks for a 6-digit code from an authenticator app on your phone after you enter your password. Even if someone knows your password, they cannot sign in without your phone.

This feature is available with local accounts only. <Label type="local" />

<Callout type="warning">
Keep access to your authenticator app. There are no recovery codes. If you lose your phone and cannot access the app, contact your hosting provider for help regaining access to your account.
</Callout>

## Turn on two-factor authentication

<Steps>
<li>

Go to **Account**, then **Account Details**, and find the **Two-Factor Authentication** card. It shows **2FA Disabled**.

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

There are no recovery codes. If you lose access to your authenticator app, contact your hosting provider for help regaining access to your account.

## Turn off two-factor authentication

<Steps>
<li>

Go to **Account**, then **Account Details**, and find the **Two-Factor Authentication** card. It shows **2FA Enabled**.

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
