#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Sur une borne : approuve le certificat du serveur de la borne chauffeur.

.DESCRIPTION
    Copier sur la borne ce script et le fichier certificat-borne-chauffeur.cer produit par installer-serveur.ps1,
    puis, dans une fenêtre PowerShell ouverte en tant qu'administrateur :

        powershell -ExecutionPolicy Bypass -File .\installer-certificat-borne.ps1

    Le certificat est placé dans « Ordinateur local > Autorités de certification racines de confiance » :
    Edge ouvre alors l'adresse https du serveur sans avertissement, condition pour lire le lecteur de badge.
    Inutile avec un certificat délivré par l'autorité de certification de l'entreprise.
#>
[CmdletBinding()]
param(
    [string]$Certificat,
    [string]$Adresse
)

$ErrorActionPreference = 'Stop'
# Dossier du script. $PSScriptRoot est vide dans les valeurs par défaut des paramètres sous Windows PowerShell 5.1 :
# on le calcule ici, dans le corps du script.
$dossierScript = if ($PSScriptRoot) { $PSScriptRoot } elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { (Get-Location).Path }
if (-not $Certificat) { $Certificat = Join-Path $dossierScript 'certificat-borne-chauffeur.cer' }
if (-not (Test-Path $Certificat)) { Write-Host "Fichier introuvable : $Certificat" -ForegroundColor Red; exit 1 }

$cert = Import-Certificate -FilePath $Certificat -CertStoreLocation Cert:\LocalMachine\Root
Write-Host "Certificat approuvé : $($cert.Subject)" -ForegroundColor Green
Write-Host "Noms couverts : $($cert.DnsNameList.Unicode -join ', ')"
Write-Host "Valable jusqu'au $($cert.NotAfter.ToString('dd/MM/yyyy'))"

if ($Adresse) {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $sante = Invoke-RestMethod "$Adresse/api/sante" -UseBasicParsing -TimeoutSec 15
        Write-Host "Serveur joint : version $($sante.version), base disponible : $($sante.baseDisponible)" -ForegroundColor Green
    } catch {
        Write-Warning "Serveur injoignable sur $Adresse : $($_.Exception.Message)"
    }
}
Write-Host "Fermer puis rouvrir Edge pour qu'il prenne en compte le certificat."
