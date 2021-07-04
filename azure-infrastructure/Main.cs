using System;
using System.Collections.Generic;
using azurerm;
using Constructs;
using HashiCorp.Cdktf;

namespace AzureInfrastructure
{
    public class AzureApplications : TerraformStack
    {
        public AzureApplications(Construct scope, string id) : base(scope, id)
        {
            // Update organization name
            const string orgName = "jet";

            // define resources here
            var azureProvider = new AzurermProvider(this, "AzureRm", new AzurermProviderConfig
            {
                Features = new[] { new AzurermProviderFeatures() }
            });

            var resourceGroup = new ResourceGroup(this, "basket", new ResourceGroupConfig
            {
                Name = "basket",
                Location = "canadaeast",
                Provider = azureProvider
            });

            var appServicePlan = new AppServicePlan(this, "appServicePlan", new AppServicePlanConfig
            {
                Name = $"{orgName}-appServicePlan",
                Kind = "Linux",
                Reserved = true,
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                Sku = new IAppServicePlanSku[] { new AppServicePlanSku { Size = "P1V2", Tier = "Premium" } },
                DependsOn = new ITerraformDependable[] { resourceGroup }
            });

            var appService = new AppService(this, "appService", new AppServiceConfig
            {
                Name = $"{orgName}-appService",
                AppServicePlanId = appServicePlan.Id,
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                ClientAffinityEnabled = false,
                HttpsOnly = true,
                DependsOn = new ITerraformDependable[] { appServicePlan },
                AppSettings = new Dictionary<string, string>
                {
                    {"Environment", "Production"},
                }
            });

            var appServiceSlot = new AppServiceSlot(this, "appServiceSlot", new AppServiceSlotConfig
            {
                Name = $"{orgName}-appServiceSlot",
                AppServicePlanId = appServicePlan.Id,
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                AppServiceName = appService.Name,
                HttpsOnly = true,
                DependsOn = new ITerraformDependable[] { appService },
                AppSettings = new Dictionary<string, string>
                {
                    {"Environment", "Production"}
                }
            });

            var storageAccount = new StorageAccount(this, "ssd", new StorageAccountConfig
            {
                Name = $"{orgName}ssd",
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                AccountKind = "StorageV2",
                AccountReplicationType = "LRS",
                AccountTier = "Premium"
            });

            var functionApp = new FunctionApp(this, "functionApp", new FunctionAppConfig
            {
                Name = $"{orgName}-functionApp",
                AppServicePlanId = appServicePlan.Id,
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                Version = "3",
                StorageAccountName = storageAccount.Name,
                StorageAccountAccessKey = storageAccount.PrimaryAccessKey,
                OsType = "linux",
                SiteConfig = new IFunctionAppSiteConfig[]
                {
                    new FunctionAppSiteConfig
                    {
                        AlwaysOn = true
                    }
                },
                AppSettings = new Dictionary<string, string>()
                {
                    {"WEBSITE_RUN_FROM_PACKAGE", ""},
                    {"FUNCTIONS_WORKER_RUNTIME", "dotnet"}
                },
                DependsOn = new ITerraformDependable[] { appServicePlan, storageAccount }
            });

            // Grab tenant Id from azure and update tenantId for keyVault
            // $ az account list
            // Uncomment below lines

            // var keyVault = new KeyVault(this, "keyvault", new KeyVaultConfig
            // {
            //     Name = $"{orgName}-keyvault-1",
            //     Location = resourceGroup.Location,
            //     ResourceGroupName = resourceGroup.Name,
            //     SkuName = "standard",
            //     TenantId = "<replace_tenant_id>"
            // });

            var cosmosdbAccount = new CosmosdbAccount(this, "cosmosdb", new CosmosdbAccountConfig
            {
                Name = $"{orgName}-cosmosdb",
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                OfferType = "Standard",
                Kind = "GlobalDocumentDB",
                GeoLocation = new ICosmosdbAccountGeoLocation[]
                {
                    new CosmosdbAccountGeoLocation
                    {
                        Location = resourceGroup.Location,
                        FailoverPriority = 0,
                        ZoneRedundant = false
                    }
                },
                ConsistencyPolicy = new ICosmosdbAccountConsistencyPolicy[]
                {
                    new CosmosdbAccountConsistencyPolicy
                    {
                        ConsistencyLevel = "Session"
                    }
                }
            });

            var appServiceOutput = new TerraformOutput(this, "appweburl", new TerraformOutputConfig
            {
                Value = $"https://{appService.Name}.azurewebsites.net"
            });
            var functionAppOutput = new TerraformOutput(this, "fnWebUrl", new TerraformOutputConfig
            {
                Value = $"https://{functionApp.Name}.azurewebsites.net"
            });
            var cosmosDbOutput = new TerraformOutput(this, "cosmosDbURL", new TerraformOutputConfig
            {
                Value = cosmosdbAccount.Endpoint
            });
        }

        public static void Main(string[] args)
        {
            var app = new App();
            _ = new AzureApplications(app, "azure");
            app.Synth();
            Console.WriteLine("App synth complete");
        }
    }
}