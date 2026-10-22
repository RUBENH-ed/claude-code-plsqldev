# Actualizacion automatica del plug-in "Claude Code para PL/SQL Developer" y de su paquete de skills.
# La lanza el plug-in en segundo plano al arrancar PL/SQL Developer (y el instalador, la primera vez).
#
#   1. Baja la ultima version publicada (rama "publicado" del repositorio, en %APPDATA%\ClaudeCodePlsql\repo).
#   2. Si la DLL ha cambiado, la deja en su sitio: la que esta cargada se renombra a .old (Windows lo permite) y la
#      nueva se aplica al reiniciar PL/SQL Developer.
#   3. Pone al dia las skills elegidas al instalar en ~/.claude/skills. Nunca pisa una skill que no haya puesto el
#      plug-in (las marca con el fichero .claudecodeplsql); si coincide el nombre, lo apunta como conflicto.
#   4. Solo en el equipo de quien mantiene las skills (PublicarSkills=si): las copia al repositorio y las publica.
#
# Deja el resultado en %APPDATA%\ClaudeCodePlsql\actualizacion.json, que el plug-in lee para avisar.
param([switch]$Instalando)

$ErrorActionPreference = 'Stop'
$dirPlugin = Join-Path $env:APPDATA 'ClaudeCodePlsql'
$repo = Join-Path $dirPlugin 'repo'
$ini = Join-Path $dirPlugin 'ClaudeCode.ini'
$fEstado = Join-Path $dirPlugin 'actualizacion.json'
$skillsUsuario = Join-Path $env:USERPROFILE '.claude\skills'
$marca = '.claudecodeplsql'
$utf8 = New-Object System.Text.UTF8Encoding $false
$estado = [ordered]@{ fecha = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'); version = ''; dll_nueva = $false; skills = @(); conflictos = @(); errores = @() }

function Git([string[]]$a) {
    $git = (Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (-not $git) { $git = Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe' }
    if (-not (Test-Path -LiteralPath $git)) { throw 'No esta instalado Git.' }
    $env:GIT_TERMINAL_PROMPT = '0'   # nunca esperar a que alguien escriba: si pide credenciales, falla
    $salida = & $git @a 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($a -join ' '): $(($salida | Out-String).Trim())" }
    return $salida
}

# Ajustes de [Actualizaciones] en ClaudeCode.ini
function Ajuste([string]$clave, [string]$defecto = '') {
    if (-not (Test-Path -LiteralPath $ini)) { return $defecto }
    foreach ($l in [System.IO.File]::ReadAllLines($ini, $utf8)) {
        if ($l -match "^\s*$([regex]::Escape($clave))\s*=(.*)$") { $v = [Environment]::ExpandEnvironmentVariables($Matches[1].Trim()); if ($v) { return $v } }
    }
    return $defecto
}

function Hash([string]$f) { if (Test-Path -LiteralPath $f) { (Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash } else { '' } }

# Copia una carpeta en otra dejandola igual (borra lo que sobra) solo si algo cambia
function Espejo([string]$de, [string]$a) {
    $cambio = $false
    New-Item -ItemType Directory -Force -Path $a | Out-Null
    $origen = @{}
    foreach ($f in Get-ChildItem -LiteralPath $de -Recurse -File) {
        $rel = $f.FullName.Substring($de.Length + 1); $origen[$rel.ToLowerInvariant()] = $true
        $dst = Join-Path $a $rel
        if ((Hash $f.FullName) -ne (Hash $dst)) {
            New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
            Copy-Item -LiteralPath $f.FullName -Destination $dst -Force; $cambio = $true
        }
    }
    foreach ($f in Get-ChildItem -LiteralPath $a -Recurse -File) {
        $rel = $f.FullName.Substring($a.Length + 1)
        if ($rel -ne $marca -and -not $origen.ContainsKey($rel.ToLowerInvariant())) { Remove-Item -LiteralPath $f.FullName -Force; $cambio = $true }
    }
    return $cambio
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $repo '.git'))) { throw "No hay copia del repositorio en ${repo}: instala el plug-in con instalar.ps1." }

    # Sin actualizaciones automaticas (ActualizacionesAutomaticas=no) solo se baja al instalar
    $recibir = $Instalando -or ((Ajuste 'ActualizacionesAutomaticas' 'si') -notmatch '^(no|n|false|0)$')
    if ($recibir) {
    # ------------------------------------------------------------------------ 1. ultima version publicada
    Git @('-C', $repo, 'fetch', '-q', '--depth', '1', 'origin', 'publicado') | Out-Null
    Git @('-C', $repo, 'reset', '-q', '--hard', 'origin/publicado') | Out-Null
    $fVersion = Join-Path $repo 'VERSION'
    if (Test-Path -LiteralPath $fVersion) { $estado.version = ([System.IO.File]::ReadAllText($fVersion)).Trim() }

    # ------------------------------------------------------------------------ 2. la DLL
    $dll = Ajuste 'DllInstalada'
    $nueva = Join-Path $repo 'ClaudeCodePlsql.dll'
    if ($dll -and (Test-Path -LiteralPath $nueva) -and (Hash $nueva) -ne (Hash $dll)) {
        try {
            if (Test-Path -LiteralPath $dll) {
                # La cargada no se puede sobrescribir, pero si renombrar; la .old de la vez anterior ya no esta cargada
                $old = "$dll.old"
                if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue }
                if (Test-Path -LiteralPath $old) { $old = "$dll.$(Get-Date -Format 'yyyyMMddHHmmss').old" }
                Rename-Item -LiteralPath $dll -NewName (Split-Path $old -Leaf)
            }
            Copy-Item -LiteralPath $nueva -Destination $dll -Force
            $estado.dll_nueva = $true
        } catch {
            $estado.errores += "No he podido dejar la DLL nueva en $dll ($($_.Exception.Message)). Puede que haga falta ser administrador para escribir en esa carpeta."
        }
    }

    # ------------------------------------------------------------------------ 3. las skills elegidas
    $elegidas = @((Ajuste 'Skills') -split '[,; ]+' | Where-Object { $_ })
    $disponibles = Join-Path $repo 'paquete\skills'
    if (Test-Path -LiteralPath $disponibles) {
        foreach ($d in Get-ChildItem -LiteralPath $disponibles -Directory) {
            $dst = Join-Path $skillsUsuario $d.Name
            $nuestra = Test-Path -LiteralPath (Join-Path $dst $marca)
            if ($elegidas -contains $d.Name) {
                if ((Test-Path -LiteralPath $dst) -and -not $nuestra) {
                    $estado.conflictos += "Ya tienes una skill $($d.Name) en $dst que no es del plug-in: no la toco."
                    continue
                }
                if (Espejo $d.FullName $dst) { $estado.skills += $d.Name }
                [System.IO.File]::WriteAllText((Join-Path $dst $marca), "Instalada y actualizada por el plug-in Claude Code para PL/SQL Developer.`r`n", $utf8)
            } elseif ($nuestra) {
                # Ya no la quiere: se quita solo si la puso el plug-in
                Remove-Item -LiteralPath $dst -Recurse -Force
            }
        }
    }
    # Ajustes que lee la skill de expedientes
    if ($elegidas -contains 'proyecto-expediente') {
        $tarea = (Ajuste 'TareaProgramada' 'si') -notmatch '^(no|n|false|0)$'
        $json = ConvertTo-Json -InputObject ([ordered]@{ tarea_programada = $tarea }) -Compress
        [System.IO.File]::WriteAllText((Join-Path $env:USERPROFILE '.claude\expedientes.json'), $json, $utf8)
    }
    }   # fin de if ($recibir)

    # ------------------------------------------------------------------------ 4. publicar las skills (quien las mantiene)
    if ((Ajuste 'PublicarSkills' 'no') -match '^(si|sí|s|true|1)$' -and -not $Instalando) {
        try {
            # En una copia propia de la rama principal, para no mezclarlo con lo que tenga a medias en la suya
            $fuentes = Join-Path $dirPlugin 'fuentes'
            if (-not (Test-Path -LiteralPath (Join-Path $fuentes '.git'))) {
                $url = (Git @('-C', $repo, 'remote', 'get-url', 'origin') | Select-Object -First 1).ToString().Trim()
                Git @('clone', '-q', '--branch', 'main', $url, $fuentes) | Out-Null
            }
            Git @('-C', $fuentes, 'fetch', '-q', 'origin', 'main') | Out-Null
            Git @('-C', $fuentes, 'reset', '-q', '--hard', 'origin/main') | Out-Null
            $s = Join-Path $fuentes 'herramientas\sincronizar-skills.ps1'
            $salida = & powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $s -Publicar 2>&1
            if ($LASTEXITCODE -ne 0) { throw (($salida | Out-String).Trim()) }
            $estado['publicacion'] = (($salida | Out-String).Trim())
        } catch {
            $estado.errores += "No he podido publicar tus skills: $($_.Exception.Message)"
        }
    }
}
catch {
    $estado.errores += $_.Exception.Message
}

[System.IO.File]::WriteAllText($fEstado, (ConvertTo-Json -InputObject $estado -Depth 4), $utf8)
if ($Instalando) { $estado }
if ($estado.errores.Count -gt 0) { exit 1 }
exit 0
