$ErrorActionPreference = "Stop"

$mods = Join-Path $env:USERPROFILE "Documents\MySummerCar\Mods"
$destino = Join-Path $env:USERPROFILE "Desktop\MscDuo-amigo.zip"
$pasta = Join-Path $env:TEMP "MscDuo-amigo"

if (Test-Path $pasta) { Remove-Item $pasta -Recurse -Force }
New-Item -ItemType Directory -Path $pasta | Out-Null

$linhas = @("Mods do pacote MscDuo-amigo (gerado em $(Get-Date -Format 'dd/MM/yyyy HH:mm'))", "Descompacte tudo dentro de Documents\MySummerCar\Mods", "")

foreach ($dll in Get-ChildItem $mods -Filter *.dll -File | Sort-Object Name) {
    Copy-Item $dll.FullName $pasta
    $versao = $dll.VersionInfo.FileVersion
    if (-not $versao) { $versao = "?" }
    $hash = (Get-FileHash $dll.FullName -Algorithm SHA256).Hash.Substring(0, 12)
    $linhas += "{0,-28} versão {1,-12} {2,8} KB  {3}  {4}" -f $dll.Name, $versao, [math]::Ceiling($dll.Length / 1KB), $dll.LastWriteTime.ToString("dd/MM/yyyy HH:mm"), $hash
}

$assets = Join-Path $mods "Assets"
if (Test-Path $assets) {
    foreach ($item in Get-ChildItem $assets -Directory | Where-Object { $_.Name -notlike "MSCLoader_*" }) {
        $alvo = Join-Path $pasta "Assets"
        New-Item -ItemType Directory -Path $alvo -Force | Out-Null
        Copy-Item $item.FullName $alvo -Recurse
        $linhas += "Assets\$($item.Name)"
    }
}

$referencias = Join-Path $mods "References"
if ((Test-Path $referencias) -and (Get-ChildItem $referencias -File)) {
    Copy-Item $referencias $pasta -Recurse
    foreach ($ref in Get-ChildItem $referencias -File) { $linhas += "References\$($ref.Name)" }
}

$linhas | Set-Content (Join-Path $pasta "mods.txt") -Encoding UTF8

Compress-Archive -Path (Join-Path $pasta "*") -DestinationPath $destino -Force
Remove-Item $pasta -Recurse -Force

$linhas | Select-Object -Skip 3 | ForEach-Object { Write-Output $_ }
Write-Output ("Pacote: {0} ({1:N1} MB)" -f $destino, ((Get-Item $destino).Length / 1MB))
