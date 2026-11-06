# Deploying Orchestra, and putting the bot on WhatsApp

Two paths. Take the first one to see it working today; take the second when you
want a URL that survives closing your laptop.

---

## 1. Locally, through a tunnel

Nothing is deployed. Twilio reaches your machine through a tunnel.

**Twilio side.** In the console, open Messaging → Try it out → **Send a WhatsApp
message**. It shows a sandbox number and a join code. Send that code from your
own WhatsApp to their number and you are opted in.

**Your side.**

```bash
dotnet run --project src/TeamPortal.Web
```

Then, in another terminal, expose it:

```bash
ssh -R 80:localhost:5211 nokey@localhost.run
```

That prints an `https://…` URL. Any tunnel works — ngrok, Cloudflare, VS dev
tunnels — it only has to be HTTPS.

**Join the two up.** Set the secrets, including the tunnel URL, because Twilio
signs the address *it* called and it will not match the one your app sees:

```bash
cd src/TeamPortal.Web
dotnet user-secrets set "WhatsApp:Enabled"          "true"
dotnet user-secrets set "WhatsApp:AccountSid"       "AC..."
dotnet user-secrets set "WhatsApp:AuthToken"        "..."
dotnet user-secrets set "WhatsApp:PublicWebhookUrl" "https://your-tunnel/api/whatsapp/incoming"
```

Restart the app. In the Twilio sandbox settings, set **"When a message comes
in"** to that same URL, method **POST**. Message the sandbox number and it
answers from your processes.

Send **reset** to start a fresh conversation.

---

## 2. On Azure

```bash
az group create -n orchestra-rg -l westeurope

az deployment group create -g orchestra-rg -f deploy/main.bicep \
  --parameters namePrefix=orchestra sqlAdminPassword='<a strong password>'
```

The deployment prints the web app name, its URL, and the webhook URL. Publish:

```bash
dotnet publish src/TeamPortal.Web -c Release -o ./publish
cd publish && zip -r ../app.zip . && cd ..

az webapp deploy -g orchestra-rg -n orchestra-web --src-path app.zip --type zip
```

Then set the secrets, which the Bicep file deliberately does not contain:

```bash
az webapp config appsettings set -g orchestra-rg -n orchestra-web --settings \
  Smtp__Password='...' \
  SeedAdmin__Password='...' \
  Chat__Gemini__ApiKey='...' \
  WhatsApp__AccountSid='AC...' \
  WhatsApp__AuthToken='...' \
  WhatsApp__Enabled=true
```

Point Twilio's webhook at the URL the deployment printed.

---

## Things that will catch you

**The signature is over Twilio's URL, not yours.** Behind a tunnel or App
Service's proxy, the scheme and host the app sees are not the ones Twilio
called, and every webhook then fails its signature check with a bare 403. That
is what `WhatsApp:PublicWebhookUrl` is for — set it and the mismatch stops
mattering.

**The sandbox opt-in lapses.** Twilio drops you after a period of inactivity and
messages simply stop arriving. Re-send the join code.

**Anyone in the sandbox can ask anything.** This POC does not check who is
messaging — that was a deliberate choice to keep it simple. The only thing
standing between a stranger and your process documentation is that they would
need your sandbox join code. Do not point a *production* WhatsApp number at this
without adding identity first.

**Costs.** Basic SQL and a B1 plan are small but not free, and Twilio charges
per message. Delete the resource group when you are done with it:

```bash
az group delete -n orchestra-rg
```
