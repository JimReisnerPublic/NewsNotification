# Set variables
resourceGroup="NewsResourceGroup"
location="centralus"
storageAccount="newsfuncstore$RANDOM"
functionAppName="NewsFunctionApp$RANDOM"
serviceBusNamespace="NewsServiceBus$RANDOM"
serviceBusQueue="NewsQueue"

#az login

# Create resource group
# az group create --name $resourceGroup --location $location

# Create storage account
az storage account create --name $storageAccount --location $location --resource-group $resourceGroup --sku Standard_LRS

# Create App Service plan
az functionapp plan create --resource-group $resourceGroup --name "NewsFunctionPlan" --location $location --number-of-workers 1 --sku B1 --is-linux

# Create Function App
az functionapp create --name $functionAppName --storage-account $storageAccount --resource-group $resourceGroup --plan "NewsFunctionPlan" --runtime dotnet --functions-version 4

# Create Service Bus namespace
az servicebus namespace create --name $serviceBusNamespace --resource-group $resourceGroup --location $location --sku Standard

# Create Service Bus queue
az servicebus queue create --name $serviceBusQueue --namespace-name $serviceBusNamespace --resource-group $resourceGroup

# Get the Service Bus connection string
serviceBusConnectionString=$(az servicebus namespace authorization-rule keys list --resource-group $resourceGroup --namespace-name $serviceBusNamespace --name RootManageSharedAccessKey --query primaryConnectionString --output tsv)

# Add Service Bus connection string to Function App settings
az functionapp config appsettings set --name $functionAppName --resource-group $resourceGroup --settings "ServiceBusConnection=$serviceBusConnectionString"

echo "Function App Name: $functionAppName"
echo "Service Bus Namespace: $serviceBusNamespace"
echo "Service Bus Queue: $serviceBusQueue"