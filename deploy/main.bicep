// Orchestra on Azure App Service, enough to give Twilio a real HTTPS URL.
//
// Deliberately small: a plan, a web app, a SQL database. No front door, no
// key vault, no slots. Those are worth adding when this stops being a proof of
// concept, and adding them now would be guessing at how it gets run.
//
//   az deployment group create -g <rg> -f deploy/main.bicep \
//      --parameters namePrefix=orchestra sqlAdminPassword=<password>

@description('Prefix for every resource name. Must be globally unique for the web app.')
param namePrefix string

@description('Where to put it. Defaults to the resource group\'s region.')
param location string = resourceGroup().location

@description('SQL administrator login.')
param sqlAdminLogin string = 'orchestraadmin'

@description('SQL administrator password. Pass at deploy time; never commit it.')
@secure()
param sqlAdminPassword string

@description('B1 is the smallest tier that gets a custom domain and always-on.')
param appServiceSku string = 'B1'

var webAppName = '${namePrefix}-web'
var sqlServerName = '${namePrefix}-sql'
var databaseName = 'Orchestra'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-plan'
  location: location
  sku: {
    name: appServiceSku
  }
  properties: {
    reserved: false
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Lets App Service reach the database without opening it to the internet at
// large. 0.0.0.0 is Azure's own convention for "Azure services", not "everyone".
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      alwaysOn: true

      // Blazor Server is a persistent connection. Without affinity a reconnect
      // can land on a different instance and the circuit is gone.
      webSocketsEnabled: true

      // Twilio signs the URL it called. Behind App Service's proxy the app sees
      // http and an internal host, so the forwarded headers have to be honoured
      // or every signature check fails - see WhatsApp:PublicWebhookUrl, which is
      // the belt to this braces.
      appSettings: [
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'App__BaseUrl'
          value: 'https://${webAppName}.azurewebsites.net'
        }
        {
          name: 'WhatsApp__PublicWebhookUrl'
          value: 'https://${webAppName}.azurewebsites.net/api/whatsapp/incoming'
        }
      ]
      connectionStrings: [
        {
          name: 'Portal'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;'
        }
      ]
    }
  }
}

// Every secret is set AFTER deployment, by hand or by the pipeline's secret
// store. Putting them in this file would put them in git, which is the mistake
// this project has already made once.
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webhookUrl string = 'https://${webApp.properties.defaultHostName}/api/whatsapp/incoming'
output secretsStillToSet array = [
  'Smtp__Password'
  'SeedAdmin__Password'
  'Chat__Gemini__ApiKey'
  'WhatsApp__AccountSid'
  'WhatsApp__AuthToken'
  'WhatsApp__Enabled  (set to true once the rest are in)'
]
