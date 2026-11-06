# Settings and Secrets

Orchestra reads its settings when it starts. This page explains what they are and, most importantly, **where to keep the private ones**.

## Two kinds of settings

| Kind | Examples | Where it lives |
|---|---|---|
| **Normal** | Which database, which website address, which chat brain | `src/TeamPortal.Web/appsettings.json` |
| **Secret** | Passwords, API keys, tokens | **User secrets**, never in the file |

### Why secrets must stay out of the file

The settings file is saved in git, and git remembers **everything forever**. If you ever type a key into that file and save it, deleting it later does not remove it from the history. Anyone who can read the history can read your key.

The golden rules:

1. **Never** type a password or key into `appsettings.json`.
2. If you ever do, treat the key as **stolen**. Go to the company that gave it to you and make a new one.
3. Keep the empty slot in the file (`"ApiKey": ""`) so everyone knows where the setting goes.

## How to set a secret

Open a terminal in the web project folder and use `user-secrets`. The value is stored on **your** computer, outside the project.

```bash
cd src/TeamPortal.Web
dotnet user-secrets set "SeedAdmin:Password" "your-strong-password"
dotnet user-secrets set "Smtp:Password" "your-email-password"
dotnet user-secrets set "Chat:Gemini:ApiKey" "your-key-here"
```

To see which secrets you have set (it shows the names and values on your own screen):

```bash
dotnet user-secrets list
```

## The settings, one by one

| Setting | What it does |
|---|---|
| `ConnectionStrings:Portal` | Where the database is. It uses LocalDB by default. |
| `App:BaseUrl` | The website address. Used to build the links in emails. |
| `Smtp` | The outgoing email. Sends confirmations, password resets and "you are approved" notes. Port 465 uses a secure connection. |
| `SeedAdmin` | The first administrator, made on the first start. |
| `Storage:RootPath` | Where uploaded screenshots are kept. They are kept **outside** the public web folder and handed out only to signed-in people. |
| `Seed:DemoPassword` | The password for the demo accounts made in development. |
| `Chat:Provider` | Which brain answers: `Portal`, `Gemini` or `OpenAi`. |
| `Chat:Gemini` and `Chat:OpenAi` | Each has an `ApiKey` and a `Model`. You can keep both and switch with one word. |
| `WhatsApp` | Turns the WhatsApp bot on and sets up Twilio. Off by default. |

## Demo accounts

When you run in development, the app makes a few practice accounts and some sample data so there is something to look at. They are for your own computer only. **Do not use that practice data or its passwords in a real company.** Set your own admin password with `SeedAdmin:Password`.

## Settings checklist before going live

- [ ] No keys or passwords in `appsettings.json`
- [ ] `Chat:Provider` is the brain you really want
- [ ] `App:BaseUrl` is your real website address
- [ ] Email settings tested
- [ ] Demo accounts removed or their passwords changed
- [ ] WhatsApp off unless you set it up

Back to [Getting Started](Getting-Started.md).
