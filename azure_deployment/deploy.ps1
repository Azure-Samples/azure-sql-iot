# mkdir .ssh
# ssh-keygen -m PEM -t rsa -b 4096 -f .ssh/id_rsa

# [select enter twice]
# cat .ssh/id_rsa.pub
# (save this entire key)

$sshPublicKey=Read-Host "Please enter your ssh public key you just created" -MaskInput


# Collect password 
$adminSqlLogin = "cloudadmin"
$password = Read-Host "Your username is 'cloudadmin'. Please enter a password for your Azure SQL Database server that meets the password requirements"
# Prompt for local ip address
$ipAddress = Read-Host "Disconnect your VPN, open PowerShell on your machine and run '(Invoke-WebRequest -Uri "https://ipinfo.io/ip").Content'. Please enter the value (include periods) next to 'Address': "
Write-Host "Password and IP Address stored"

# Get resource group and location and random string
$resourceGroupName = "[sandbox resource group name]"
$resourceGroup = Get-AzResourceGroup | Where ResourceGroupName -like $resourceGroupName
$uniqueID = Get-Random -Minimum 100000 -Maximum 1000000
$location = $resourceGroup.Location
# Create several uniquely named services
$iotHub = "iothub$($uniqueID)"
$serverName = "iotserver$($uniqueID)"
$iotSite = "iotsite$($uniqueID)"
$iotServerFarm = "iotfarm$($uniqueID)"
$iotSimulator = "iotsimulator$($uniqueID)"
$logWorkspace = "iotlogs$($uniqueID)"
$iotStorageAccount = "iotstorageaccount$($uniqueID)"
$storageContainer = "iotstoragecontainer$($uniqueID)"
$networkInterface = "iotnic$($uniqueID)"
$vNet = "iotvnet$($uniqueID)"
$ipAddressName="publicip$($uniqueID)"
Write-Host "Please note your unique ID for future exercises in this module:"  
Write-Host $uniqueID
Write-Host "Your resource group name is:"
Write-Host $resourceGroupName
Write-Host "Your resources will be deployed in the following region:"
Write-Host $location
Write-Host "Your server name is:"
Write-Host $serverName

# Resource group name and resource group
#$resourceGroupName = "[sandbox resource group name]"
#$resourceGroup = Get-AzResourceGroup | Where ResourceGroupName -like $resourceGroupName
#$location = $resourceGroup.Location
# Get the repository name
$appRepository = Read-Host "Enter your GitHub repository URL (for example, 'https://github.com/[username]/azure-sql-iot'):"
$cloneRepository = git clone $appRepository

az deployment group create -g $resourceGroupName `
--template-file ./azure-sql-iot/azure_deployment/template.json `
    --parameters `
    iothub_name=$iotHub `
    server_sql_name=$serverName `
    server_admin_name=$adminSqlLogin `
    server_admin_password=$(ConvertTo-SecureString -String $password -AsPlainText -Force) `
    site_iot_name=$iotSite `
    serverfarm_iot_name=$iotServerFarm `
    virtualmachine_devicesimulator_name=$iotSimulator `
    logworkspace_name=$logWorkspace `
    storageaccount_iothub_name=$iotStorageAccount `
    storageaccount_iothub_container=$storageContainer `
    networkinterface_devicesimulator_name=$networkInterface `
    virtualnetwork_iot_name=$vNet `
    ip_address_name=$ipAddressName `
    ssh_public_key=$sshPublicKey 

# Create a server firewall rule that allows access from the specified IP range and all Azure services
$serverFirewallRule = New-AzSqlServerFirewallRule `
    -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -FirewallRuleName "AllowedIPs" `
    -StartIpAddress $ipAddress -EndIpAddress $ipAddress 
$allowAzureIpsRule = New-AzSqlServerFirewallRule `
    -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -AllowAllAzureIPs

az vm show --resource-group $resourceGroupName --name $iotSimulator --show-details --query publicIps