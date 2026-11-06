# Questions and Fixes

## I cannot sign in

| What you see | What to do |
|---|---|
| "Invalid email or password" | Check for typing mistakes. Remember that too many wrong tries locks you. |
| Locked until a time | Wait 15 minutes, or use **Forgot password?** to unlock. |
| It says you must confirm your email | Open the email we sent and click the link. Look in the spam folder too. |
| It says you are waiting for approval | An admin has to approve you. Ask them. See [Approving New People](Approving-New-People.md). |

## I did not get the email

1. Check spam.
2. Ask an admin to check the email settings (`Smtp`) on [Settings and Secrets](Settings-and-Secrets.md).
3. On the sign-up page you may see "We could not send the email just now". Your account was still made. Try the **Forgot password?** link later to get a fresh email.

## The app will not start because of the database

The database engine may be asleep. Wake it:

```bash
sqllocaldb stop MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

If you see a message that the TeamPortal database files exist but are not attached, the message on screen tells you the exact command to run. If you do not need the old data, you can delete both database files and start again. The demo data is made again.

## The chat bot says "not written up"

That is the bot being honest. The answer is not in your processes. Open [My Training Data](My-Training-Data.md), look for the missing facts, then add them in [Business Processes](Business-Processes.md).

## The chat bot gives an answer with a note about the portal

The AI brain could not be reached (for example, the key ran out), so the built-in helper answered instead. Check your key on [Settings and Secrets](Settings-and-Secrets.md).

## My screenshot will not upload

- Is it a **PNG, JPG or WEBP**?
- Is it **5 MB or smaller**?
- Is it a real picture? Renaming a different kind of file will not work.

## I cannot see the Add or Delete buttons, or I get "no permission"

Your role may not allow it. See [Roles and Permissions](Roles-and-Permissions.md) and ask an admin.

## The page stopped reacting

Orchestra keeps a live connection to the server. If it drops, a box appears. Press **Retry** or reload the page.

## Is there a notification bell?

The code has a place for notifications, but nothing sends them yet, so it stays empty.

## Where do I report a problem?

Tell your admin. If you are a developer, open an issue on the repository.
