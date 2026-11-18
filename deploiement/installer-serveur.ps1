#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installe la borne chauffeur dans IIS : rôle IIS, pool d'applications, droits sur le dossier,
    certificat HTTPS et site web. Peut être relancé sans risque.

.DESCRIPTION
    À lancer sur le serveur, depuis le dossier de l'application (celui qui contient Dpl.Borne.Api.dll),
    dans une fenêtre PowerShell ouverte en tant qu'administrateur :

        powershell -ExecutionPolicy Bypass -File .\installer-serveur.ps1

    Sans paramètre, le script crée un certificat auto-signé valable 5 ans pour le nom et les adresses IP
    du serveur, et l'exporte dans certificat-borne-chauffeur.cer. Ce fichier est à installer sur chaque
    borne avec installer-certificat-borne.ps1.

.PARAMETER Empreinte
    Empreinte d'un certificat déjà installé dans « Ordinateur local > Personnel » (par exemple fourni par le
    service informatique). Le script l'utilise au lieu d'en créer un.

.PARAMETER NomsServeur
    Noms et adresses IP que les bornes utiliseront pour joindre le serveur. Par défaut : nom de la machine,
    nom complet dans le domaine et adresses IPv4.

.PARAMETER SansHttps
    Crée le site en HTTP seulement (port 8080 par défaut, le port 80 étant souvent pris par « Default Web Site »). Les bornes devront alors avoir la stratégie Edge
    OverrideSecurityRestrictionsOnInsecureOrigin (voir installation.md, §4).

.EXAMPLE
    .\installer-serveur.ps1
.EXAMPLE
    .\installer-serveur.ps1 -NomsServeur SRVBORNE, srvborne.dpl.local, 172.16.80.10
.EXAMPLE
    .\installer-serveur.ps1 -Empreinte 3A1F0C...
.EXAMPLE
    .\installer-serveur.ps1 -SansHttps
#>
[CmdletBinding()]
param(
    [string]$Dossier = $PSScriptRoot,
    [string]$NomSite = 'BorneChauffeur',
    [int]$Port = 0,
    [string]$Empreinte,
    [string[]]$NomsServeur,
    [switch]$SansHttps
)

$ErrorActionPreference = 'Stop'
if ($Port -eq 0) { $Port = if ($SansHttps) { 8080 } else { 443 } }
$Dossier = (Resolve-Path $Dossier).Path

function Etape($texte) { Write-Host "`n== $texte" -ForegroundColor Cyan }
function Ok($texte) { Write-Host "   $texte" -ForegroundColor Green }
function Arret($texte) { Write-Host "`n$texte" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
Etape "Dossier de l'application"
if (-not (Test-Path (Join-Path $Dossier 'Dpl.Borne.Api.dll'))) {
    Arret "Dpl.Borne.Api.dll introuvable dans $Dossier. Lancez le script depuis le dossier de l'application ou précisez -Dossier."
}
# Fichiers téléchargés depuis GitHub : Windows peut les bloquer.
Get-ChildItem $Dossier -Recurse -File | Unblock-File
Ok $Dossier

# ---------------------------------------------------------------------------
Etape "Rôle IIS"
if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
    # Windows Server
    $r = Install-WindowsFeature Web-Server, Web-Mgmt-Console, Web-Scripting-Tools
    if ($r.RestartNeeded -eq 'Yes') { Write-Warning "Un redémarrage du serveur sera nécessaire." }
} else {
    # Windows 10 / 11 (poste de recette)
    $r = Enable-WindowsOptionalFeature -Online -All -NoRestart -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-ManagementConsole, IIS-ManagementScriptingTools
    if ($r.RestartNeeded) { Write-Warning "Un redémarrage du poste sera nécessaire." }
}
Import-Module WebAdministration
Ok "IIS présent"

# ---------------------------------------------------------------------------
Etape "ASP.NET Core Hosting Bundle .NET 10"
$runtime = Get-ChildItem (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.AspNetCore.App') -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like '10.*' }
$module = Get-WebGlobalModule -Name AspNetCoreModuleV2 -ErrorAction SilentlyContinue
if (-not $runtime) {
    Arret ("Le runtime ASP.NET Core 10 n'est pas installé. Installez le « Hosting Bundle » .NET 10 :`n" +
        "  https://dotnet.microsoft.com/download/dotnet/10.0  (colonne ASP.NET Core Runtime, Windows : Hosting Bundle)`n" +
        "puis relancez ce script.")
}
if (-not $module) {
    Arret ("Le module ASP.NET Core n'est pas enregistré dans IIS. C'est le cas quand le Hosting Bundle a été installé avant IIS :`n" +
        "relancez l'installateur du Hosting Bundle et choisissez « Réparer », puis relancez ce script.")
}
Ok "Runtime $(@($runtime)[-1].Name) et module AspNetCoreModuleV2 présents"

# ---------------------------------------------------------------------------
Etape "Pool d'applications $NomSite"
$pool = "IIS:\AppPools\$NomSite"
if (-not (Test-Path $pool)) { New-WebAppPool -Name $NomSite | Out-Null }
Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''          # « Aucun code managé »
Set-ItemProperty $pool -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $pool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Ok "Aucun code managé, pipeline intégré, toujours démarré"

Etape "Droits du pool sur le dossier"
$identite = "IIS AppPool\$NomSite"
& icacls.exe $Dossier /grant "${identite}:(OI)(CI)RX" /Q | Out-Null
if ($LASTEXITCODE -ne 0) { Arret "icacls a échoué ($LASTEXITCODE)." }
Ok "$identite : lecture et exécution"

# ---------------------------------------------------------------------------
$protocole = if ($SansHttps) { 'http' } else { 'https' }
$autre = Get-WebBinding -Protocol $protocole -Port $Port | Where-Object { $_.ItemXPath -notmatch "@name='$NomSite'" }
if ($autre) {
    $siteOccupant = ($autre.ItemXPath -replace ".*@name='([^']+)'.*", '$1') | Select-Object -First 1
    Arret "Le port $Port ($protocole) est déjà utilisé par le site « $siteOccupant ». Relancez avec un autre port, par exemple -Port 8443."
}

$cert = $null
if (-not $SansHttps) {
    Etape "Certificat HTTPS"
    if ($Empreinte) {
        $Empreinte = ($Empreinte -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
        $cert = Get-Item "Cert:\LocalMachine\My\$Empreinte" -ErrorAction SilentlyContinue
        if (-not $cert) { Arret "Aucun certificat d'empreinte $Empreinte dans « Ordinateur local > Personnel »." }
        Ok "Certificat fourni : $($cert.Subject), valable jusqu'au $($cert.NotAfter.ToString('dd/MM/yyyy'))"
    } else {
        if (-not $NomsServeur) {
            $NomsServeur = @($env:COMPUTERNAME)
            try {
                $complet = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
                if ($complet -and $complet -ne $env:COMPUTERNAME) { $NomsServeur += $complet }
            } catch { }
            $NomsServeur += @(Get-NetIPAddress -AddressFamily IPv4 |
                Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
                Select-Object -ExpandProperty IPAddress)
        }
        $NomsServeur = $NomsServeur | Select-Object -Unique
        $san = ($NomsServeur | ForEach-Object { if ($_ -as [ipaddress]) { "IPAddress=$_" } else { "DNS=$_" } }) -join '&'

        # On réutilise le certificat créé par une exécution précédente s'il couvre toujours les mêmes noms.
        $cert = Get-ChildItem Cert:\LocalMachine\My |
            Where-Object { $_.FriendlyName -eq "Borne chauffeur ($NomSite)" -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
            Where-Object { $c = $_; @($NomsServeur | Where-Object { -not ($_ -as [ipaddress]) } | Where-Object { $c.DnsNameList.Unicode -notcontains $_ }).Count -eq 0 } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
        if (-not $cert) {
            $cert = New-SelfSignedCertificate -Subject "CN=$($NomsServeur[0])" -FriendlyName "Borne chauffeur ($NomSite)" `
                -TextExtension @("2.5.29.17={text}$san", "2.5.29.37={text}1.3.6.1.5.5.7.3.1") `
                -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyUsage DigitalSignature, KeyEncipherment `
                -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(5) -CertStoreLocation 'Cert:\LocalMachine\My'
            Ok "Certificat auto-signé créé pour : $($NomsServeur -join ', ')"
        } else {
            Ok "Certificat existant réutilisé (valable jusqu'au $($cert.NotAfter.ToString('dd/MM/yyyy')))"
        }

        # Partie publique, à installer sur les bornes ; aussi approuvée sur le serveur lui-même.
        $fichierCer = Join-Path $Dossier 'certificat-borne-chauffeur.cer'
        Export-Certificate -Cert $cert -FilePath $fichierCer -Force | Out-Null
        Import-Certificate -FilePath $fichierCer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        Ok "Exporté : $fichierCer"
    }
}

# ---------------------------------------------------------------------------
Etape "Site $NomSite"
if (-not (Get-Website -Name $NomSite)) {
    # Identifiant explicite : New-Website échoue sans lui quand IIS n'a encore aucun site.
    $id = [int](Get-Website | Measure-Object -Property id -Maximum).Maximum + 1
    if ($SansHttps) { New-Website -Name $NomSite -Id $id -PhysicalPath $Dossier -ApplicationPool $NomSite -Port $Port | Out-Null }
    else { New-Website -Name $NomSite -Id $id -PhysicalPath $Dossier -ApplicationPool $NomSite -Port $Port -Ssl | Out-Null }
} else {
    Set-ItemProperty "IIS:\Sites\$NomSite" -Name physicalPath -Value $Dossier
    Set-ItemProperty "IIS:\Sites\$NomSite" -Name applicationPool -Value $NomSite
    if (-not (Get-WebBinding -Name $NomSite -Protocol $protocole -Port $Port)) {
        if ($SansHttps) { New-WebBinding -Name $NomSite -Protocol http -Port $Port } else { New-WebBinding -Name $NomSite -Protocol https -Port $Port }
    }
}

if ($cert) {
    $liaison = "IIS:\SslBindings\0.0.0.0!$Port"
    if (Test-Path $liaison) { Remove-Item $liaison }
    (Get-WebBinding -Name $NomSite -Protocol https -Port $Port).AddSslCertificate($cert.Thumbprint, 'My')
    Ok "Certificat associé au port $Port"
}

if (-not (Get-NetFirewallRule -DisplayName "Borne chauffeur ($Port)" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "Borne chauffeur ($Port)" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}
Ok "Pare-feu : port $Port ouvert en entrée"

Start-WebAppPool -Name $NomSite -ErrorAction SilentlyContinue
Start-Website -Name $NomSite
Ok "Site démarré"

# ---------------------------------------------------------------------------
Etape "Vérification"
$hote = if ($NomsServeur) { $NomsServeur[0] } else { $env:COMPUTERNAME }
$suffixePort = if (($SansHttps -and $Port -eq 80) -or (-not $SansHttps -and $Port -eq 443)) { '' } else { ":$Port" }
$adresse = "${protocole}://$hote$suffixePort"
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $sante = Invoke-RestMethod "$adresse/api/sante" -UseBasicParsing -TimeoutSec 30
    Ok "$adresse/api/sante : source $($sante.source), base disponible : $($sante.baseDisponible)"
} catch {
    Write-Warning "Le site ne répond pas encore sur $adresse/api/sante : $($_.Exception.Message)"
    Write-Warning "Voir le journal d'événements Windows « Application » (source IIS AspNetCore Module V2)."
}

Write-Host ""
Write-Host "Adresse des bornes : $adresse/?borne=SEICHAUFF01  (ou SEICHAUFF02)" -ForegroundColor Yellow
if ($cert -and -not $Empreinte) {
    Write-Host "Sur chaque borne, installer le certificat : installer-certificat-borne.ps1 avec certificat-borne-chauffeur.cer" -ForegroundColor Yellow
}
if ($SansHttps) {
    Write-Host "Sans HTTPS, chaque borne doit avoir la stratégie Edge OverrideSecurityRestrictionsOnInsecureOrigin = $adresse" -ForegroundColor Yellow
}
if (-not (Test-Path (Join-Path $Dossier 'appsettings.Local.json'))) {
    Write-Host "Le site tourne en mode démonstration tant que appsettings.Local.json n'est pas créé (installation.md, §2)." -ForegroundColor Yellow
}
