# Instalador del plug-in "Claude Code para PL/SQL Developer".
#
# Antes, una sola vez, la copia del repositorio (con tu usuario de GitHub de la organizacion):
#   git clone --branch publicado <url del repositorio> "%APPDATA%\ClaudeCodePlsql\repo"
# Y despues:
#   powershell -NoProfile -ExecutionPolicy Bypass -File "%APPDATA%\ClaudeCodePlsql\repo\instalar.ps1"
#
# Revisa lo que hace falta y los conflictos antes de tocar nada, pregunta que quieres instalar, deja la DLL en la
# carpeta de plug-ins de PL/SQL Developer y activa la actualizacion automatica. Se puede volver a ejecutar para
# cambiar lo elegido.
#
# Quien mantiene las skills del paquete (las tiene originales en su cuenta de claude.ai) lo ejecuta una vez con
# -Publicar para que, al arrancar PL/SQL Developer, sus cambios se publiquen para los demas.
param([switch]$Publicar)

$ErrorActionPreference = 'Stop'
$dirPlugin = Join-Path $env:APPDATA 'ClaudeCodePlsql'
$repo = Join-Path $dirPlugin 'repo'
$ini = Join-Path $dirPlugin 'ClaudeCode.ini'
$utf8 = New-Object System.Text.UTF8Encoding $false
$skillsUsuario = Join-Path $env:USERPROFILE '.claude\skills'

function Titulo([string]$t) { Write-Host ''; Write-Host $t -ForegroundColor Cyan }
function Bien([string]$t) { Write-Host "  [ok] $t" -ForegroundColor Green }
function Ojo([string]$t) { Write-Host "  [aviso] $t" -ForegroundColor Yellow }
function Mal([string]$t) { Write-Host "  [falta] $t" -ForegroundColor Red }
function SiNo([string]$pregunta, [bool]$defecto) {
    $op = if ($defecto) { 'S/n' } else { 's/N' }
    $r = Read-Host "  $pregunta ($op)"
    if (-not $r) { return $defecto }
    return $r -match '^(s|si|sí|y|yes)$'
}

# Ajustes que ya tuviera (para proponerlos de nuevo)
function Ajuste([string]$clave, [string]$defecto = '') {
    if (-not (Test-Path -LiteralPath $ini)) { return $defecto }
    foreach ($l in [System.IO.File]::ReadAllLines($ini, $utf8)) {
        if ($l -match "^\s*$([regex]::Escape($clave))\s*=(.*)$") { return $Matches[1].Trim() }
    }
    return $defecto
}

Write-Host 'Claude Code para PL/SQL Developer: instalacion' -ForegroundColor White
if (-not (Test-Path -LiteralPath (Join-Path $repo '.git'))) {
    Write-Host "Primero clona el repositorio en $repo (rama publicado) y vuelve a ejecutar este script desde alli." -ForegroundColor Red
    exit 1
}
$version = if (Test-Path (Join-Path $repo 'VERSION')) { ([System.IO.File]::ReadAllText((Join-Path $repo 'VERSION'))).Trim() } else { '' }
Write-Host "Version publicada: $version"

# ============================================================================== 1. requisitos y conflictos
Titulo '1. Comprobaciones (no se toca nada todavia)'
$bloqueos = 0

$git = (Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $git -and (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe'))) { $git = Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe' }
if ($git) { Bien "Git: $git" } else { Mal 'Git no esta instalado (hace falta para las actualizaciones): winget install --id Git.Git -e'; $bloqueos++ }

$claude = @((Join-Path $env:USERPROFILE '.local\bin\claude.exe'), (Get-Command claude -ErrorAction SilentlyContinue).Source) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if ($claude) { Bien "Claude Code: $claude" } else { Ojo 'No encuentro Claude Code (claude.exe). Instalalo antes de usar el plug-in; si esta en otra ruta, ponla en ComandoClaude.' }

# Node.js: solo para el panel de sesiones (opcional). Sin el, Claude se abre como siempre.
$node = (Get-Command node -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $node -and (Test-Path "$env:ProgramFiles\nodejs\node.exe")) { $node = "$env:ProgramFiles\nodejs\node.exe" }
$verNode = if ($node) { (& $node --version 2>$null) -replace '^v', '' } else { '' }
if ($node -and [int](($verNode -split '\.')[0]) -ge 18) {
    Bien "Node.js $verNode (para el panel de sesiones): $node"
} elseif ($node) {
    Ojo "Node.js $verNode es antiguo: el panel de sesiones necesita la 18 o superior (winget install OpenJS.NodeJS.LTS). Sin el, Claude se abre como siempre."
} else {
    Ojo 'No esta Node.js: el panel de sesiones no funcionara hasta que lo instales (winget install OpenJS.NodeJS.LTS). Sin el, Claude se abre como siempre.'
}

$sqlplus = (Get-Command sqlplus -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $sqlplus) { $sqlplus = Get-ChildItem 'C:\Oracle\*\bin\sqlplus.exe', 'C:\app\*\product\*\*\bin\sqlplus.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if ($sqlplus) { Bien "Cliente de Oracle (sqlplus): $sqlplus" } else { Ojo 'No encuentro sqlplus: los botones de base de datos no funcionaran hasta que instales el cliente de Oracle.' }

# Carpeta de plug-ins de PL/SQL Developer
$candidatas = @((Ajuste 'DllInstalada') | Where-Object { $_ } | ForEach-Object { Split-Path $_ })
$candidatas += Get-ChildItem 'C:\Oracle\PLSQLDeveloper*', "$env:ProgramFiles\PLSQL Developer*", "${env:ProgramFiles(x86)}\PLSQL Developer*" -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'plsqldev.exe') } | ForEach-Object { Join-Path $_.FullName 'PlugIns' }
$candidatas = @($candidatas | Select-Object -Unique)
if ($candidatas.Count -eq 0) {
    $r = Read-Host '  No encuentro PL/SQL Developer. Escribe la carpeta donde esta plsqldev.exe'
    if ($r -and (Test-Path (Join-Path $r 'plsqldev.exe'))) { $candidatas = @(Join-Path $r 'PlugIns') }
}
if ($candidatas.Count -gt 1) {
    for ($i = 0; $i -lt $candidatas.Count; $i++) { Write-Host "    $($i + 1). $($candidatas[$i])" }
    $n = Read-Host "  Hay varias instalaciones de PL/SQL Developer. Numero de la que quieres usar (1-$($candidatas.Count))"
    $candidatas = @($candidatas[[int]$n - 1])
}
$dirPlugins = $candidatas | Select-Object -First 1
if ($dirPlugins) {
    New-Item -ItemType Directory -Force -Path $dirPlugins -ErrorAction SilentlyContinue | Out-Null
    $prueba = Join-Path $dirPlugins ".escritura-$PID"
    try { [System.IO.File]::WriteAllText($prueba, 'x'); Remove-Item $prueba -Force; Bien "Carpeta de plug-ins: $dirPlugins" }
    catch { Mal "No puedo escribir en $dirPlugins. Ejecuta este script como administrador una vez, o pon en PL/SQL Developer (Configure > Preferences > Plug-Ins) una carpeta tuya y vuelve a ejecutarlo."; $bloqueos++ }
} else { Mal 'No hay carpeta de plug-ins de PL/SQL Developer.'; $bloqueos++ }

$dll = if ($dirPlugins) { Join-Path $dirPlugins 'ClaudeCodePlsql.dll' } else { '' }
$otras = @(Get-ChildItem $dirPlugins -Filter 'ClaudeCode*.dll' -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'ClaudeCodePlsql.dll' })
if ($otras) { Ojo "Hay otros plug-ins con nombre parecido en la carpeta: $($otras.Name -join ', '). Si son versiones viejas de este, quitalos para que no salgan dos pestañas." }
if ($dll -and (Test-Path $dll)) { Ojo "Ya hay un ClaudeCodePlsql.dll: se sustituira por la version publicada (la anterior queda como .old)." }
if (Get-Process plsqldev -ErrorAction SilentlyContinue) { Ojo 'PL/SQL Developer esta abierto: la version nueva se vera al reiniciarlo.' }

$ajustesClaude = Join-Path $env:USERPROFILE '.claude\settings.json'
if ((Test-Path $ajustesClaude) -and (Select-String -Path $ajustesClaude -Pattern '"disableAllHooks"\s*:\s*true' -Quiet)) {
    Ojo 'Tienes disableAllHooks en los ajustes de Claude Code: el hook de solo consultas no se aplicaria. Quitalo para usar las BBDD.'
}

if ($bloqueos -gt 0) { Write-Host ''; Write-Host "Hay $bloqueos problema(s) que impiden instalar. Corrigelos y vuelve a ejecutar el script." -ForegroundColor Red; exit 1 }

# ============================================================================== 2. que instalar
Titulo '2. Que quieres instalar'
Write-Host '  El plug-in se instala siempre. Ademas, skills opcionales para Claude:'
$previas = @((Ajuste 'Skills') -split '[,; ]+' | Where-Object { $_ })
$catalogo = [ordered]@{
    'proyecto-expediente' = 'Expedientes: carpeta del expediente con su estructura, documentacion en doc/, panel «Mis expedientes» y revision de correo y Teams'
    'a18-acta-entrega'    = 'A18: genera el Acta de Entrega de Desarrollo con la plantilla oficial'
    'manual-desarrollo'   = 'Manual de desarrollo para el cliente con la plantilla oficial'
}
$elegidas = @()
foreach ($s in $catalogo.Keys) {
    Write-Host "  - $($catalogo[$s])"
    if (SiNo "    ¿Instalar ${s}?"($previas.Count -eq 0 -or $previas -contains $s)) { $elegidas += $s }
}
$tarea = 'no'
if ($elegidas -contains 'proyecto-expediente') {
    Write-Host '  La skill de expedientes crea tu panel «Mis expedientes» la primera vez que la uses.'
    if (SiNo '    ¿Quieres tambien la tarea programada que revisa correo y Teams dos veces al dia y lo apunta en el panel?' ((Ajuste 'TareaProgramada' 'si') -ne 'no')) { $tarea = 'si' }
}

# Conflictos con skills que ya tengas
$sincronizadas = Join-Path $skillsUsuario 'synced'
foreach ($s in @($elegidas)) {
    $dst = Join-Path $skillsUsuario $s
    if ((Test-Path $dst) -and -not (Test-Path (Join-Path $dst '.claudecodeplsql'))) {
        Ojo "Ya tienes una skill $s en $dst que no es del plug-in."
        if (SiNo "    ¿Sustituirla por la del plug-in? (si dices que no, sigues con la tuya)" $false) {
            $copia = "$dst.antes-del-plugin-$(Get-Date -Format 'yyyyMMddHHmmss')"
            Move-Item -LiteralPath $dst -Destination $copia
            Bien "Tu skill queda guardada en $copia"
        } else { $elegidas = @($elegidas | Where-Object { $_ -ne $s }) }
    }
    $dup = Get-ChildItem $sincronizadas -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName $s } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($dup) {
        Ojo "Ya tienes $s desde tu cuenta de claude.ai ($dup): tendrias dos skills con el mismo nombre."
        if (-not (SiNo "    ¿Instalar igualmente la del plug-in?" $false)) { $elegidas = @($elegidas | Where-Object { $_ -ne $s }) }
    }
}

Write-Host '  Al arrancar PL/SQL Developer se pueden bajar solas las versiones nuevas del plug-in y de las skills elegidas.'
$recibir = if (SiNo '    ¿Recibir actualizaciones automaticas?' ((Ajuste 'ActualizacionesAutomaticas' 'si') -ne 'no')) { 'si' } else { 'no' }

# Publicar las skills solo lo activa quien las mantiene, con -Publicar; si ya lo tenia, se respeta
$publicarSkills = if ($Publicar) { 'si' } else { Ajuste 'PublicarSkills' 'no' }
if ($Publicar) {
    $originales = Get-ChildItem $sincronizadas -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path (Join-Path $_.FullName 'proyecto-expediente\SKILL.md') } | Select-Object -First 1
    if ($originales) { Bien 'Tus cambios en las skills del paquete se publicaran para los demas al arrancar PL/SQL Developer.' }
    else { Ojo 'Con -Publicar, pero no encuentro las skills originales (proyecto-expediente, a18-acta-entrega, manual-desarrollo) en tu cuenta: no habra nada que publicar.' }
}

# ============================================================================== 3. instalar
Titulo '3. Instalando'
New-Item -ItemType Directory -Force -Path $dirPlugin | Out-Null
# Los ajustes de actualizacion van en su seccion de ClaudeCode.ini; el resto del fichero no se toca
# Lista que admite añadir líneas (un if que devuelve la lista la convertiría en un array de tamaño fijo)
$lineas = New-Object 'System.Collections.Generic.List[string]'
if (Test-Path $ini) { $lineas.AddRange([System.IO.File]::ReadAllLines($ini, $utf8)) }
$nuevos = [ordered]@{ DllInstalada = $dll; Skills = ($elegidas -join ','); TareaProgramada = $tarea; ActualizacionesAutomaticas = $recibir; PublicarSkills = $publicarSkills }
foreach ($k in $nuevos.Keys) {
    $i = -1; for ($j = 0; $j -lt $lineas.Count; $j++) { if ($lineas[$j] -match "^\s*$k\s*=") { $i = $j; break } }
    if ($i -ge 0) { $lineas[$i] = "$k=$($nuevos[$k])" }
    else {
        $s = $lineas.IndexOf('[Actualizaciones]')
        if ($s -lt 0) {
            $lineas.Add(''); $lineas.Add('[Actualizaciones]')
            $lineas.Add('; Los escribe instalar.ps1 (vuelve a ejecutarlo para cambiarlos). ActualizacionesAutomaticas=no deja la version')
            $lineas.Add('; instalada. PublicarSkills=si solo en el equipo de quien mantiene las skills (instalar.ps1 -Publicar).')
            $s = $lineas.Count - 1
        }
        # Al final de la seccion (antes de la siguiente y de las lineas en blanco que la separan)
        $fin = $s + 1
        while ($fin -lt $lineas.Count -and -not $lineas[$fin].StartsWith('[')) { $fin++ }
        while ($fin -gt $s + 1 -and -not $lineas[$fin - 1].Trim()) { $fin-- }
        $lineas.Insert($fin, "$k=$($nuevos[$k])")
    }
}
# Si el .ini no existia, el plug-in completa el resto con sus valores por defecto. UTF-8 con BOM, como lo crea el plug-in
[System.IO.File]::WriteAllLines($ini, $lineas, (New-Object System.Text.UTF8Encoding $true))
Bien "Ajustes guardados en $ini"

$r = & powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $repo 'actualizar.ps1') -Instalando 2>&1
$estado = Get-Content (Join-Path $dirPlugin 'actualizacion.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($e in @($estado.errores)) { if ($e) { Mal $e } }
foreach ($c in @($estado.conflictos)) { if ($c) { Ojo $c } }
if ($dll -and (Test-Path $dll)) { Bien "Plug-in en $dll (version $($estado.version))" }
foreach ($s in $elegidas) { if (Test-Path (Join-Path $skillsUsuario "$s\SKILL.md")) { Bien "Skill $s en $(Join-Path $skillsUsuario $s)" } }
$actualPanel = Join-Path $dirPlugin 'panel-app\actual.txt'
if (Test-Path $actualPanel) { Bien "Panel de sesiones en $(([System.IO.File]::ReadAllText($actualPanel)).Trim())$(if (-not $node) { ' (falta Node.js para usarlo)' })" }

Titulo 'Listo'
Write-Host '  Abre (o reinicia) PL/SQL Developer: tendras la pestaña Claude Code.'
Write-Host '  Con Node.js instalado, el boton Panel de sesiones te enseña todas las sesiones de Claude abiertas, en directo.'
if ($recibir -eq 'si') {
    Write-Host '  A partir de ahora, cada vez que arranque PL/SQL Developer se bajan solas las versiones nuevas del plug-in y de las'
    Write-Host '  skills elegidas; la DLL nueva se aplica al reiniciarlo.'
} else {
    Write-Host '  Sin actualizaciones automaticas: para pasar a una version nueva, vuelve a ejecutar este script.'
}
Write-Host '  Para cambiar lo elegido, vuelve a ejecutar este script.'
