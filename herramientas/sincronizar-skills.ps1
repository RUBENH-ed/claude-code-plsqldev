# Copia las skills del paquete de expedientes desde el equipo de quien las mantiene al repositorio del plug-in,
# para que les lleguen a los demas con la actualizacion automatica.
#
#   a18-acta-entrega y manual-desarrollo: tal cual.
#   proyecto-expediente: version generica (sin el nombre de quien la mantiene ni el enlace a su panel; cada uno usa
#   el suyo, que la skill busca o crea a partir de assets/panel.html).
#
# Las skills originales no se tocan. Si queda algo personal que estas reglas no conocen, no copia nada y lo dice.
#
# Uso (en el clon del repositorio):
#   powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\sincronizar-skills.ps1 [-Origen <carpeta>] [-Publicar]
#   -Origen   carpeta con las skills (por defecto, la ultima carpeta sincronizada de ~/.claude/skills que las tenga)
#   -Panel    HTML del panel «Mis expedientes» para la plantilla (si no, se deja la que ya hay)
#   -Publicar ademas hace commit y push si algo ha cambiado
param(
    [string]$Origen,
    [string]$Panel,
    [switch]$Publicar
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $repo 'paquete\skills'
$utf8 = New-Object System.Text.UTF8Encoding $false
$skills = 'proyecto-expediente', 'a18-acta-entrega', 'manual-desarrollo'

# ---------------------------------------------------------------------------- de donde se copian
if (-not $Origen) {
    $Origen = Get-ChildItem (Join-Path $env:USERPROFILE '.claude\skills') -Recurse -Filter SKILL.md -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'proyecto-expediente' } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
        ForEach-Object { $_.Directory.Parent.FullName }
}
if (-not $Origen -or -not (Test-Path (Join-Path $Origen 'proyecto-expediente\SKILL.md'))) {
    throw "No encuentro las skills (proyecto-expediente, a18-acta-entrega, manual-desarrollo). Indica la carpeta con -Origen."
}
foreach ($s in $skills) {
    if (-not (Test-Path (Join-Path $Origen "$s\SKILL.md"))) { throw "Falta la skill $s en $Origen." }
}

# ---------------------------------------------------------------------------- version generica de proyecto-expediente
# Reglas en orden: primero las frases concretas (para que la gramatica quede bien) y al final las generales
$reglas = @(
    @('de LIBRA de Rubén', 'de LIBRA'),
    @('Revisión de expedientes de Rubén (', 'Revisión de expedientes ('),
    @('| Técnico | Rubén Hernando |', '| Técnico | Ana Pérez |'),
    @('(CLIENTE DEMO) -> Rubén', '(CLIENTE DEMO) -> Ana Pérez'),
    @('("yo" es Rubén;', '("yo" es el usuario;'),
    @('Solo lo ven Rubén y quien él haga editor', 'Solo lo ven el usuario y quien él haga editor'),
    @('(la de la plantilla «EXPEDIENTE - NOMBRE» de su Escritorio, con el nombre del caso en la carpeta superior)',
      '(la estándar de los expedientes, con el nombre del caso en la carpeta superior)'),
    @('https://claude.ai/artifact/7ZaB7AJ9CfEeHtqgfgAn3G', '{PANEL}'),
    @('De Rubén', 'Del usuario'),
    @('de Rubén', 'del usuario'),
    @(' a Rubén', ' al usuario'),
    @('(Rubén ', '(el usuario '),
    @('Rubén', 'el usuario')
)
$aviso = @'

> **Versión compartida (llega con el plug-in de PL/SQL Developer).** Donde pone `{PANEL}` va la dirección del panel
> «Mis expedientes» de quien usa la skill, no la de otra persona. Búscalo la primera vez de cada conversación con
> `Artifact`, `action: "list"`, por su título. Si no existe, publícalo con la plantilla `assets/panel.html` de esta skill:
> título «Mis expedientes» y `capabilities` `{"db":{"rules":[{"path":"","read":"admin","write":"admin"}]}}`, para que
> solo lo vea su dueño. Después usa siempre esa dirección.
> Si `~/.claude/expedientes.json` tiene `"tarea_programada": false`, no crees la tarea programada: las novedades de
> correo y Teams se revisan cuando el usuario lo pida.
> En Claude Code no hay `device_bash` ni hace falta pedir acceso a las carpetas: usa la consola y las rutas directamente.
'@

function Generica([string]$texto) {
    foreach ($r in $reglas) { $texto = $texto.Replace($r[0], $r[1]) }
    # El aviso, justo despues del titulo
    $i = $texto.IndexOf("`n# ")
    if ($i -lt 0) { throw 'proyecto-expediente no tiene titulo (# ...): no se donde poner el aviso.' }
    $fin = $texto.IndexOf("`n", $i + 1)
    $texto = $texto.Substring(0, $fin + 1) + ($aviso -replace "`r`n", "`n") + $texto.Substring($fin + 1)
    # Lo personal que quede sin regla para el
    $resto = [regex]::Matches($texto, '(?i)rub[eé]n|hernando|claude\.ai/artifact/[A-Za-z0-9]{8,}|@edisa\.com') | ForEach-Object { $_.Value } | Select-Object -Unique
    if ($resto) { throw "La version generica aun tiene datos personales sin regla: $($resto -join ', '). Añade una regla en este script." }
    return $texto
}

# ---------------------------------------------------------------------------- copia
$cambios = @()
foreach ($s in $skills) {
    $de = Join-Path $Origen $s
    $a = Join-Path $destino $s
    New-Item -ItemType Directory -Force -Path $a | Out-Null
    foreach ($f in Get-ChildItem $de -Recurse -File) {
        $rel = $f.FullName.Substring($de.Length + 1)
        $dst = Join-Path $a $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
        if ($s -eq 'proyecto-expediente' -and $rel -eq 'SKILL.md') {
            $nuevo = $utf8.GetBytes((Generica ([System.IO.File]::ReadAllText($f.FullName, $utf8))))
        } else {
            $nuevo = [System.IO.File]::ReadAllBytes($f.FullName)   # tal cual, byte a byte
        }
        $viejo = if (Test-Path -LiteralPath $dst) { [System.IO.File]::ReadAllBytes($dst) } else { $null }
        if (-not $viejo -or [Convert]::ToBase64String($viejo) -ne [Convert]::ToBase64String($nuevo)) {
            [System.IO.File]::WriteAllBytes($dst, $nuevo)
            $cambios += "$s\$rel"
        }
    }
}
if ($Panel) {
    $dst = Join-Path $destino 'proyecto-expediente\assets\panel.html'
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    $nuevo = [System.IO.File]::ReadAllBytes($Panel)
    $viejo = if (Test-Path -LiteralPath $dst) { [System.IO.File]::ReadAllBytes($dst) } else { $null }
    if (-not $viejo -or [Convert]::ToBase64String($viejo) -ne [Convert]::ToBase64String($nuevo)) {
        [System.IO.File]::WriteAllBytes($dst, $nuevo); $cambios += 'proyecto-expediente\assets\panel.html'
    }
}

if (-not $cambios) { Write-Output 'Sin cambios en las skills.'; exit 0 }
Write-Output "Actualizado: $($cambios -join ', ')"

if ($Publicar) {
    $git = (Get-Command git -ErrorAction SilentlyContinue).Source
    if (-not $git) { $git = Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe' }
    & $git -C $repo add -- paquete/skills
    & $git -C $repo diff --cached --quiet -- paquete/skills
    if ($LASTEXITCODE -eq 0) { Write-Output 'Sin cambios que publicar.'; exit 0 }
    # Solo las skills, aunque hubiera otras cosas preparadas
    & $git -C $repo commit -q -m "Skills del paquete de expedientes actualizadas desde el equipo de quien las mantiene" -- paquete/skills
    if ($LASTEXITCODE -ne 0) { throw 'No se ha podido hacer el commit.' }
    & $git -C $repo push -q origin HEAD
    if ($LASTEXITCODE -ne 0) { throw 'No se ha podido subir a GitHub (git push).' }
    Write-Output 'Publicado: les llegara a los demas con la proxima actualizacion.'
}
