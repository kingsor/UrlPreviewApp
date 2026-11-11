# Deploy su DigitalOcean App Platform con GitHub Actions

Ogni push sul branch `main` compila e testa l'applicazione, poi la pubblica su **DigitalOcean App Platform** all'indirizzo **https://urlpreview.neetpiq.com**.

```
git push main ──► GitHub Actions
                   ├─ build:  restore → build → test
                   └─ deploy: digitalocean/app_action/deploy (applica .do/app.yaml)
                                  └─► App Platform: clona il repo → docker build (Dockerfile) → rollout
```

| File | Ruolo |
|---|---|
| [`Dockerfile`](Dockerfile) | Immagine multi-stage: SDK .NET 10 per `dotnet publish`, runtime `aspnet:10.0`, utente non root, porta 8080. |
| [`.do/app.yaml`](.do/app.yaml) | App spec: regione, servizio, porta, health check `/healthz`, variabili d'ambiente, dominio. |
| [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) | CI (build + test) e, se passa, deploy su App Platform; dopo il deploy crea il tag `vX.Y.Z` e la GitHub Release (versione da `Directory.Build.props`). |

L'app è un host ASP.NET Core: serve il client Blazor WebAssembly (con prerendering) ed espone `GET /api/metadata`, quindi serve un **servizio** (container) e non uno static site. App Platform non ha un buildpack .NET 10, per questo si usa il Dockerfile.

In `.do/app.yaml` il servizio ha `deploy_on_push: false`: App Platform non fa deploy da solo a ogni push, lo fa solo il workflow **dopo** che i test sono passati.

---

## 1. Prerequisiti

- Un account DigitalOcean con un metodo di pagamento (il servizio `apps-s-1vcpu-0.5gb` costa circa 5 $/mese).
- Accesso al pannello DNS del dominio `neetpiq.com`.
- Diritti di amministratore sul repository GitHub `kingsor/UrlPreviewApp`.
- Facoltativi: [`doctl`](https://docs.digitalocean.com/reference/doctl/how-to/install/) (`doctl auth init`) e [GitHub CLI](https://cli.github.com/) (`gh auth login`).

## 2. Accesso di DigitalOcean al repository GitHub

App Platform costruisce l'immagine clonando il repository, quindi deve poterlo leggere:

1. Pannello DigitalOcean → **Apps → Create App → GitHub**.
2. Se il repository non compare, clicca **Edit your GitHub permissions** / **Manage access** e autorizza la GitHub app di DigitalOcean su `kingsor/UrlPreviewApp`.
3. Puoi chiudere la procedura guidata senza creare l'app: la crea il workflow.

## 3. Token API e configurazione di GitHub

1. Pannello DigitalOcean → **API → Tokens → Generate New Token**. Scopi: **Full access**, oppure custom con almeno `app` (create, read, update). Copia il token.
2. Nel repository GitHub:
   - **Settings → Environments → New environment** → `production` (facoltativo: *Required reviewers*, *Deployment branches* solo `main`).
   - **Settings → Secrets and variables → Actions** → secret `DIGITALOCEAN_ACCESS_TOKEN` con il token.

Con GitHub CLI:

```bash
gh secret set DIGITALOCEAN_ACCESS_TOKEN -R kingsor/UrlPreviewApp
```

## 4. Primo deploy

```bash
git push origin main
```

Il job `deploy` usa `digitalocean/app_action/deploy@v2`: al primo run **crea** l'app `urlpreviewapp` da `.do/app.yaml`, ai run successivi la **aggiorna** con lo spec e attende la fine del deploy. Per seguire la run: scheda **Actions**, oppure `gh run watch -R kingsor/UrlPreviewApp`.

In alternativa, a mano:

```bash
doctl apps create --spec .do/app.yaml          # prima volta
doctl apps list                                # recupera l'ID
doctl apps update <app-id> --spec .do/app.yaml # modifiche allo spec
doctl apps create-deployment <app-id>          # redeploy
```

Al termine l'app risponde subito su `https://urlpreviewapp-xxxxx.ondigitalocean.app` (lo trovi in **Apps → urlpreviewapp**, oppure `doctl apps get <app-id> --format DefaultIngress`).

## 5. Dominio urlpreview.neetpiq.com

Il dominio è già dichiarato nello spec (`domains:`), quindi App Platform lo aspetta: manca solo il record DNS, perché `neetpiq.com` non è gestito da DigitalOcean.

1. In **Apps → urlpreviewapp → Settings → Domains** controlla il valore indicato per `urlpreview.neetpiq.com` (è l'host `.ondigitalocean.app` dell'app).
2. Nel pannello DNS di `neetpiq.com` crea:

   | Tipo | Nome | Valore | TTL |
   |---|---|---|---|
   | `CNAME` | `urlpreview` | `urlpreviewapp-xxxxx.ondigitalocean.app.` | 3600 (o auto) |

   - Se il DNS è su **Cloudflare**, imposta il record su **DNS only** (nuvola grigia): con il proxy attivo App Platform non riesce a validare il dominio e a emettere il certificato.
   - Se esiste già un record `urlpreview` (A, AAAA o CNAME), rimuovilo.
3. Attendi che lo stato del dominio diventi **Active**: App Platform emette e rinnova da solo il certificato TLS (Let's Encrypt). Di solito servono pochi minuti, a volte fino a un'ora per la propagazione DNS.

Verifica la risoluzione:

```bash
nslookup urlpreview.neetpiq.com
```

## 6. Verifica

```bash
curl -I https://urlpreview.neetpiq.com/
curl https://urlpreview.neetpiq.com/healthz   # {"status":"Healthy","version":"X.Y.Z"}: la versione in produzione
curl "https://urlpreview.neetpiq.com/api/metadata?url=github.com"

# Log
doctl apps logs <app-id> web --type build
doctl apps logs <app-id> web --type run --follow
```

Prova anche il deep link `https://urlpreview.neetpiq.com/?url=github.com` e lo switch chiaro/scuro.

Prima del push puoi provare l'immagine in locale (serve Docker):

```bash
docker build -t urlpreviewapp .
docker run --rm -p 8080:8080 urlpreviewapp
curl http://localhost:8080/healthz
```

## 7. Configurazione

La configurazione dell'app è nella sezione `Preview` di `appsettings.json`; si sovrascrive con variabili d'ambiente nel formato `Sezione__Chiave`, in `envs:` di `.do/app.yaml` (o da **Settings → App-Level / Component Environment Variables**, ma lo spec resta la fonte di verità: il workflow lo riapplica a ogni deploy).

- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: App Platform termina TLS davanti al container. Così l'app vede il vero IP del client (usato come chiave dal **rate limit per IP** di `/api/metadata`) e lo schema `https`.
- Altri override possibili: `Preview__TimeoutSeconds`, `Preview__MaxContentBytes`, `Preview__SiteChecksTimeoutSeconds`, `Preview__UserAgent`.
- `instance_count: 1`: il rate limiter è in memoria, con più istanze ognuna avrebbe il suo contatore.
- ⚠️ **Non impostare mai `Preview__AllowPrivateNetworks=true` in produzione**: disattiva la protezione SSRF (`NetworkGuard`).

## 8. Risoluzione dei problemi

| Problema | Causa / soluzione |
|---|---|
| Il job `deploy` fallisce con *GitHub user does not have access* / *repository not found* | DigitalOcean non è autorizzato sul repository: vedi sezione 2. |
| Il job `deploy` fallisce con `401` / *Unable to authenticate* | Secret `DIGITALOCEAN_ACCESS_TOKEN` assente, scaduto o senza lo scopo `app`. |
| Build dell'immagine fallita | `doctl apps logs <app-id> web --type build`. Riproduci con `docker build .` in locale. |
| Deploy bloccato su *health check failed* | L'app non risponde 200 su `/healthz` sulla porta 8080: controlla i log `--type run`. |
| Dominio fermo su *Pending* / certificato non emesso | Il CNAME manca, punta al valore sbagliato o è proxato da Cloudflare (va su *DNS only*). |
| Molte risposte `429` | Il rate limit è per IP (`Preview:RequestsPerMinute`). Se tutti gli utenti condividono lo stesso IP, il proxy non è riconosciuto: verifica `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; in alternativa usa l'header `do-connecting-ip` come chiave del partitioner in `Program.cs`. |
| Le anteprime di alcuni siti vanno in `504` | Il timeout verso i siti analizzati è `Preview:TimeoutSeconds` (15 s); aumentalo in `envs:`. |
| Out of memory / riavvii | Passa a `instance_size_slug: apps-s-1vcpu-1gb`. |

La CI usa il pacchetto **NuGet** `UrlPreviewParser` (`UseLocalUrlPreviewParser=false`), quindi il repository fratello `../UrlPreviewParser` non serve. Per aggiornare la libreria cambia `UrlPreviewParserVersion` in `Directory.Build.props` e pubblica prima quella versione su NuGet.

### Rimozione

```bash
doctl apps delete <app-id>
```

Poi elimina il record `CNAME urlpreview` dal DNS e il token API.
