# Deploy su Azure App Service con GitHub Actions

Ogni push sul branch `main` compila, testa e pubblica l'applicazione su **Azure App Service (Linux)** tramite il workflow [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml).

```
git push main ──► GitHub Actions
                   ├─ build:  restore → build → test → publish → artifact
                   └─ deploy: login OIDC su Azure → azure/webapps-deploy → App Service
```

L'app è un host ASP.NET Core: serve il client Blazor WebAssembly (con prerendering) ed espone `GET /api/metadata`. Per questo serve un App Service e non Static Web Apps.

Il login da GitHub ad Azure usa **OpenID Connect** (credenziale federata): nel repository non ci sono password né publish profile.

---

## 1. Prerequisiti

- Una sottoscrizione Azure, con il permesso di creare risorse e registrazioni di app in Entra ID.
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az login`).
- Diritti di amministratore sul repository GitHub `kingsor/UrlPreviewApp`, per configurare secrets ed environment.
- Facoltativa: [GitHub CLI](https://cli.github.com/) (`gh auth login`), per impostare i secrets dalla riga di comando.

I comandi qui sotto sono scritti per **bash** (Git Bash, WSL o Cloud Shell). In PowerShell scrivi le assegnazioni come `$RG = "rg-urlpreviewapp"` e usa il backtick (`` ` ``) al posto di `\` per andare a capo.

## 2. Variabili

Scegli i nomi una volta sola. `APP` deve essere univoco a livello globale, perché diventa `https://<APP>.azurewebsites.net`.

```bash
RG=rg-urlpreviewapp
LOCATION=westeurope
PLAN=plan-urlpreviewapp
APP=urlpreviewapp-<suffisso-univoco>
REPO=kingsor/UrlPreviewApp
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
```

## 3. Risorse Azure

```bash
# Resource group
az group create -n $RG -l $LOCATION

# Piano App Service Linux (B1 ≈ 13 $/mese; F1 è gratuito ma senza Always On, con quote CPU e avvio lento a freddo)
az appservice plan create -g $RG -n $PLAN --is-linux --sku B1

# Verifica che il runtime .NET 10 sia disponibile
az webapp list-runtimes --os linux --query "[?contains(@, 'DOTNETCORE')]"

# Web app
az webapp create -g $RG -p $PLAN -n $APP --runtime "DOTNETCORE:10.0"

# Impostazioni di base
az webapp update -g $RG -n $APP --https-only true
az webapp config set -g $RG -n $APP --always-on true --http20-enabled true --ftps-state Disabled --min-tls-version 1.2
```

### App settings

La configurazione dell'app è nella sezione `Preview` di `appsettings.json`. In Azure i valori si possono sovrascrivere con app settings nel formato `Sezione__Chiave`.

```bash
az webapp config appsettings set -g $RG -n $APP --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
  Preview__RequestsPerMinute=30
```

- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: App Service mette davanti all'app un proxy. Con questa impostazione l'app vede il vero IP del client, che il **rate limit per IP** di `/api/metadata` usa come chiave. Sulle immagini Linux di solito è già attiva; impostarla esplicitamente non fa danni.
- Altri override possibili: `Preview__TimeoutSeconds`, `Preview__MaxContentBytes`, `Preview__SiteChecksTimeoutSeconds`, `Preview__UserAgent`.
- ⚠️ **Non impostare mai `Preview__AllowPrivateNetworks=true` in produzione**: disattiva la protezione SSRF (`NetworkGuard`) e permetterebbe di interrogare indirizzi interni di Azure, come `169.254.169.254`.

## 4. Identità per GitHub Actions (OIDC)

```bash
# Registrazione dell'app e service principal
CLIENT_ID=$(az ad app create --display-name "github-$APP" --query appId -o tsv)
az ad sp create --id $CLIENT_ID

# Permessi minimi: Website Contributor solo su questa web app
WEBAPP_ID=$(az webapp show -g $RG -n $APP --query id -o tsv)
az role assignment create --assignee $CLIENT_ID --role "Website Contributor" --scope $WEBAPP_ID

# Credenziale federata: accetta solo token dei job che usano l'environment GitHub "production" di questo repository
az ad app federated-credential create --id $CLIENT_ID --parameters "{
  \"name\": \"github-production\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:$REPO:environment:production\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"
```

> Il `subject` deve coincidere esattamente con l'environment usato dal job `deploy` (`environment: name: production`). Se rinomini l'environment o il repository, aggiorna la credenziale federata.

## 5. Configurazione di GitHub

### Environment

In **Settings → Environments → New environment** crea `production`. Se vuoi, aggiungi:
- **Required reviewers**, per approvare a mano ogni deploy;
- **Deployment branches**: solo `main`.

### Secrets e variabili

| Tipo | Nome | Valore |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | `$CLIENT_ID` |
| Secret | `AZURE_TENANT_ID` | `$TENANT_ID` |
| Secret | `AZURE_SUBSCRIPTION_ID` | `$SUBSCRIPTION_ID` |
| Variable | `AZURE_WEBAPP_NAME` | `$APP` |

Puoi impostarli dalla UI (**Settings → Secrets and variables → Actions**) oppure con GitHub CLI:

```bash
gh secret set AZURE_CLIENT_ID       -R $REPO -b "$CLIENT_ID"
gh secret set AZURE_TENANT_ID       -R $REPO -b "$TENANT_ID"
gh secret set AZURE_SUBSCRIPTION_ID -R $REPO -b "$SUBSCRIPTION_ID"
gh variable set AZURE_WEBAPP_NAME   -R $REPO -b "$APP"
```

Questi tre ID non sono credenziali, ma tenerli fra i secrets evita di esporli nei log.

## 6. Il workflow

Il file `.github/workflows/deploy.yml` si avvia con un **push su `main`** oppure a mano (**Actions → Deploy to Azure App Service → Run workflow**).

| Job | Passaggi |
|---|---|
| `build` | `setup-dotnet` 10.0.x → `dotnet restore` → `build -c Release` → `dotnet test` → `dotnet publish src/UrlPreviewApp` → upload dell'artifact `webapp` |
| `deploy` | download dell'artifact → `azure/login@v2` (OIDC) → `azure/webapps-deploy@v3` (zip deploy) |

- Se un test fallisce, il deploy non parte.
- `concurrency: deploy-production` mette in coda i deploy: due push ravvicinati non si sovrappongono.
- Solo il job `deploy` ha il permesso `id-token: write`, che serve a ottenere il token OIDC.
- La CI usa il pacchetto **NuGet** `UrlPreviewParser` (`UseLocalUrlPreviewParser=false` è il valore predefinito in `Directory.Build.props`), quindi il repository fratello `../UrlPreviewParser` non serve. Per aggiornare la libreria cambia `UrlPreviewParserVersion` in `Directory.Build.props` e pubblica prima quella versione su NuGet.

## 7. Primo deploy e verifica

```bash
git add .github/workflows/deploy.yml deploy.md
git commit -m "Add Azure App Service deployment workflow"
git push origin main
```

Per seguire la run: scheda **Actions** del repository, oppure `gh run watch -R $REPO`.

Al termine:

```bash
# Pagina principale (prerender)
curl -I https://$APP.azurewebsites.net/

# API
curl "https://$APP.azurewebsites.net/api/metadata?url=github.com"

# Log in tempo reale
az webapp log config -g $RG -n $APP --docker-container-logging filesystem
az webapp log tail -g $RG -n $APP
```

Prova anche il deep link `https://<APP>.azurewebsites.net/?url=github.com` e lo switch chiaro/scuro.

## 8. Note e risoluzione dei problemi

| Problema | Causa / soluzione |
|---|---|
| `DOTNETCORE:10.0` non compare in `list-runtimes` | Il runtime non è ancora disponibile nella regione scelta: prova un'altra regione, oppure pubblica self-contained (`dotnet publish ... --self-contained -r linux-x64`) e imposta lo startup command `./UrlPreviewApp`. |
| `azure/login` fallisce con `AADSTS70021` / *No matching federated identity record* | Il `subject` della credenziale federata non coincide: controlla il nome del repository e quello dell'environment (`production`). |
| `403` / *AuthorizationFailed* durante il deploy | Manca l'assegnazione del ruolo `Website Contributor` sulla web app (le assegnazioni possono impiegare qualche minuto a propagarsi). |
| Molte risposte `429` | Il rate limit è per IP (`Preview:RequestsPerMinute`). Se tutti gli utenti risultano con lo stesso IP, verifica `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. |
| Il primo caricamento è lento | Senza Always On (piano F1) l'app si spegne quando è inattiva. Usa B1 o superiore con `--always-on true`. |
| Le anteprime di alcuni siti vanno in `504` | Il timeout verso i siti analizzati è `Preview:TimeoutSeconds` (15 s); aumentalo con un app setting, se serve. |

### Dominio personalizzato (facoltativo)

```bash
az webapp config hostname add -g $RG --webapp-name $APP --hostname www.example.com
az webapp config ssl create -g $RG -n $APP --hostname www.example.com   # certificato gestito gratuito
az webapp config ssl bind -g $RG -n $APP --certificate-thumbprint <thumbprint> --ssl-type SNI
```

Prima di eseguire questi comandi crea i record DNS (`CNAME` verso `<APP>.azurewebsites.net` e il `TXT asuid.www` con il verification ID della web app).

### Alternativa: publish profile

In alternativa a OIDC puoi scaricare il publish profile (`az webapp deployment list-publishing-profiles --xml`), salvarlo nel secret `AZURE_WEBAPP_PUBLISH_PROFILE` e passarlo a `azure/webapps-deploy` con `publish-profile:`. È **sconsigliato**: contiene credenziali a lunga scadenza, va ruotato a mano e richiede di abilitare l'autenticazione di base (SCM) sulla web app.

### Rimozione

```bash
az group delete -n $RG --yes
az ad app delete --id $CLIENT_ID
```
