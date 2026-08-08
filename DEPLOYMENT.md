# Deployment Guide — Azure

## Prerequisites
- Azure subscription
- Azure CLI installed (`az --version`)
- GitHub repo with the project
- `.NET 8 SDK`

---

## 1. Azure Resources Setup

```bash
# Variables — change these
RESOURCE_GROUP="workshop-zagreb-rg"
LOCATION="westeurope"           # closest to Zagreb
APP_NAME="workshop-zagreb"
SQL_SERVER="workshop-zagreb-sql"
SQL_DB="WorkshopZagrebDb"
SQL_ADMIN="sqladmin"
STORAGE_ACCOUNT="workshopzagrebstorage"

# 1. Resource Group
az group create --name $RESOURCE_GROUP --location $LOCATION

# 2. App Service Plan (B1 = ~$13/mo)
az appservice plan create \
  --name "${APP_NAME}-plan" \
  --resource-group $RESOURCE_GROUP \
  --sku B1 --is-linux

# 3. Web App (.NET 8)
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan "${APP_NAME}-plan" \
  --runtime "DOTNETCORE:8.0"

# 4. Azure SQL Server + Database
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN \
  --admin-password "ChangeMe123!"   # CHANGE THIS

az sql db create \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_DB \
  --service-objective S0           # ~$15/mo, plenty for this

# Allow Azure services to connect
az sql server firewall-rule create \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --name AllowAzureIPs \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# 5. Blob Storage
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS

az storage container create \
  --name "workshop-media" \
  --account-name $STORAGE_ACCOUNT \
  --public-access blob              # icons/images are public
```

---

## 2. App Service Environment Variables

Set these in the Azure Portal (App Service → Configuration → Application settings) or via CLI:

```bash
# SQL Connection String
az webapp config connection-string set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings DefaultConnection="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Database=${SQL_DB};User ID=${SQL_ADMIN};Password=ChangeMe123!;Encrypt=True;" \
  --connection-string-type SQLAzure

# Blob Storage Connection String
BLOB_CONN=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT --resource-group $RESOURCE_GROUP --query connectionString -o tsv)

az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings AzureBlobStorage__ConnectionString="$BLOB_CONN"
```

**Never commit connection strings to Git.** Use environment variables or Azure Key Vault.

---

## 3. GitHub Actions CI/CD

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build & Publish
        run: |
          dotnet restore
          dotnet build --configuration Release
          dotnet publish -c Release -o ./publish

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: workshop-zagreb
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ./publish
```

Get the publish profile: Azure Portal → App Service → "Get Publish Profile" → save as GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`.

---

## 4. Run Migrations on First Deploy

After deployment, run migrations via Azure CLI or add a startup migration in `Program.cs`:

```csharp
// Program.cs — add before app.Run()
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();  // safe to call repeatedly
}
```

---

## 4b. Email (Google Workspace SMTP)

The site sends mail (newsletter confirmations, workshop announcements, inquiry
notifications) via `EmailService.cs`, which authenticates to `smtp.gmail.com`
with a Google account + App Password. `hello@workshopzagreb.com` is a Google
**Group** (a shared inbox multiple people read), not a real mailbox — Groups
can't hold 2-Step Verification or an App Password directly, so the app
authenticates as an actual admin user instead and sends *as* the group via a
verified Gmail "Send mail as" alias:

- **Username** (the account that logs in to SMTP): `admin@workshopzagreb.com`
- **From** (what recipients see, and where `SendInquiryAsync` delivers new
  inquiries — see `EmailService.cs`'s `adminEmail`, deliberately derived from
  `From`, not `Username`): `hello@workshopzagreb.com`

To get a new App Password if the current one is ever revoked/rotated:
1. Sign in to Gmail as `admin@workshopzagreb.com`.
2. Confirm `hello@workshopzagreb.com` is still listed under **Settings → Accounts
   and Import → Send mail as** (this is what makes sending "as" a Group work at
   all — a plain alias add fails with "already used by another group").
3. `myaccount.google.com/security` → turn on 2-Step Verification (this account
   only — do not do this on someone else's personal account without asking).
4. `myaccount.google.com/apppasswords` → generate one, name it something
   identifiable like "Workshop Zagreb Website".

Set it on Azure — **App Service → Settings → Environment variables → Add**:

| Name | Value |
|---|---|
| `Email__Smtp__Password` | the App Password |
| `Email__Smtp__Username` | `admin@workshopzagreb.com` |

(Double underscore maps to `:` in ASP.NET Core config — `Email__Smtp__Password`
becomes `Email:Smtp:Password`.) No need to set `Email__Smtp__From` — that value
is already correct in the checked-in `appsettings.json`.

For local dev, use user secrets instead (never touches git):
```bash
dotnet user-secrets set "Email:Smtp:Password" "xxxxxxxxxxxxxxxx"
dotnet user-secrets set "Email:Smtp:Username" "admin@workshopzagreb.com"
```

If `Email:Smtp:Password` is unset, `EmailService` logs a warning and silently
skips sending — safe default for local dev, but means a missing/rotated App
Password on Azure fails silently too. Worth checking App Service **Log stream**
for `Email:Smtp not fully configured` warnings if emails seem to stop arriving.

---

## 5. Custom Domain

1. Buy domain (e.g. `workshopzagreb.hr` from domains.hr or similar Croatian registrar).
2. Azure Portal → App Service → Custom domains → Add custom domain.
3. Add CNAME record at registrar pointing to `workshop-zagreb.azurewebsites.net`.
4. Azure Portal → TLS/SSL → Add App Service Managed Certificate (free, auto-renews).

---

## Free Tier — Testing Before Launch

**You can build and test the entire site for free.** No credit card charges until you're ready to go live.

| Resource | Free option | Limit |
|----------|-------------|-------|
| App Service | F1 tier | 60 CPU min/day, sluggish under load |
| Azure SQL | 32-day free trial on first creation | One-time only |
| Blob Storage | Fractions of a cent at this scale | Effectively free |
| Azure account | $200 credit for first 30 days | Covers full stack for a month |

**The only things you can't do on F1:**
- Custom domain (stuck on `workshop-zagreb.azurewebsites.net`)
- HTTPS on that domain

**Practical workflow:**
1. Develop and test everything on F1 — free, no time pressure after the $200 credit period
2. Show the owners the working site on the `.azurewebsites.net` URL
3. Upgrade to B1 + Basic SQL (~$18/mo) only on launch day when the real domain goes live

You don't touch billing until the site is ready to be public.

---

## Estimated Monthly Cost (Production)

| Resource | Tier | ~Cost/mo |
|----------|------|---------|
| App Service | B1 | $13 |
| Azure SQL | Basic | $5 |
| Blob Storage | LRS, <1 GB | < $1 |
| **Total** | | **~$18/mo** |

> S0 SQL (~$15/mo) if you need more performance later. Upgrade to B2 App Service if traffic grows significantly.
