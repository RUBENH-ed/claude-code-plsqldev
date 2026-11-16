# Actualizacion automatica del plug-in "Claude Code para PL/SQL Developer" y de su paquete de skills.
# La lanza el plug-in en segundo plano al arrancar PL/SQL Developer (y el instalador, la primera vez).
#
#   1. Baja la ultima version publicada (rama "publicado" del repositorio, en %APPDATA%\ClaudeCodePlsql\repo), con Git
#      usando los certificados de Windows (ver Git). Con Rama=publicado-pruebas (instalar.ps1 -Pruebas), la version
#      de pruebas que publican las ramas pruebas/...
#   2. Si la DLL ha cambiado, la deja en su sitio: la que esta cargada se renombra a .old (Windows lo permite) y la
#      nueva se aplica al reiniciar PL/SQL Developer.
#   2b. Deja el panel de sesiones en %APPDATA%\ClaudeCodePlsql\panel-app\<version> (y actual.txt apuntando a el).
#   3. Pone al dia las skills elegidas al instalar en ~/.claude/skills. Nunca pisa una skill que no haya puesto el
#      plug-in (las marca con el fichero .claudecodeplsql); si coincide el nombre, lo apunta como conflicto.
#   4. Solo en el equipo de quien mantiene las skills (PublicarSkills=si): las copia al repositorio y las publica.
#   5. Si CAMBIOS.md trae algo que no se habia visto, abre una ventana con las novedades (MostrarNovedades=no lo quita).
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

# Git siempre con los certificados de Windows (schannel). El cortafuegos de la empresa (FortiGate) revisa el HTTPS
# poniendo su propio certificado, que esta en el almacen de Windows pero no en la lista propia de Git (OpenSSL): con
# esa, Git falla con «unable to get local issuer certificate». Si Windows no puede comprobar si el certificado esta
# revocado (pasa con algunos cortafuegos), se reintenta sin esa comprobacion.
function Git([string[]]$a) {
    $git = (Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (-not $git) { $git = Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe' }
    if (-not (Test-Path -LiteralPath $git)) { throw 'No esta instalado Git.' }
    $env:GIT_TERMINAL_PROMPT = '0'   # nunca esperar a que alguien escriba: si pide credenciales, falla
    $tls = @('-c', 'http.sslBackend=schannel')
    $salida = & $git @tls @a 2>&1
    if ($LASTEXITCODE -ne 0 -and ($salida | Out-String) -match 'revocation|revocaci|CRYPT_E_') {
        $salida = & $git @tls -c http.schannelCheckRevoke=false @a 2>&1
    }
    if ($LASTEXITCODE -ne 0) { throw "git $($a -join ' '): $(($salida | Out-String).Trim())" }
    return $salida
}

function Leer([string]$f) { if (Test-Path -LiteralPath $f) { [System.IO.File]::ReadAllText($f) } else { '' } }

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
    $fVersion = Join-Path $repo 'VERSION'
    $cambiosAntes = Leer (Join-Path $repo 'CAMBIOS.md')

    # Sin actualizaciones automaticas (ActualizacionesAutomaticas=no) solo se baja al instalar
    $recibir = $Instalando -or ((Ajuste 'ActualizacionesAutomaticas' 'si') -notmatch '^(no|n|false|0)$')
    if ($recibir) {
    # ------------------------------------------------------------------------ 1. ultima version publicada
    # FETCH_HEAD y no origin/<rama>: la copia se clono solo con una rama y la otra no tiene referencia remota
    $rama = Ajuste 'Rama' 'publicado'
    Git @('-C', $repo, 'fetch', '-q', '--depth', '1', 'origin', $rama) | Out-Null
    Git @('-C', $repo, 'reset', '-q', '--hard', 'FETCH_HEAD') | Out-Null
    if (Test-Path -LiteralPath $fVersion) { $estado.version = (Leer $fVersion).Trim() }

    # ------------------------------------------------------------------------ 2. la DLL
    $dll = Ajuste 'DllInstalada'
    $nueva = Join-Path $repo 'ClaudeCodePlsql.dll'
    $antivirus = 'Seguramente la ha retirado el antivirus del equipo: pide a sistemas una excepcion para ClaudeCodePlsql.dll ' +
                 "(en $env:APPDATA\ClaudeCodePlsql y en la carpeta de plug-ins de PL/SQL Developer). El plug-in sigue con la version anterior."
    if ($dll -and -not (Test-Path -LiteralPath $nueva)) {
        $estado.errores += "La version publicada no tiene la DLL en $repo. $antivirus"
    }
    elseif ($dll -and (Hash $nueva) -ne (Hash $dll)) {
        $prep = "$dll.nueva"
        try {
            # Primero con otro nombre, y se espera un poco: si el antivirus la retira, la DLL que funciona no se ha tocado
            Copy-Item -LiteralPath $nueva -Destination $prep -Force
            Start-Sleep -Seconds 5
            if (-not (Test-Path -LiteralPath $prep) -or (Hash $prep) -ne (Hash $nueva)) { throw "La DLL nueva ha desaparecido al copiarla. $antivirus" }

            $old = $null
            if (Test-Path -LiteralPath $dll) {
                # La cargada no se puede sobrescribir, pero si renombrar; la .old de la vez anterior ya no esta cargada
                $old = "$dll.old"
                if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue }
                if (Test-Path -LiteralPath $old) { $old = "$dll.$(Get-Date -Format 'yyyyMMddHHmmss').old" }
                Rename-Item -LiteralPath $dll -NewName (Split-Path $old -Leaf)
            }
            Rename-Item -LiteralPath $prep -NewName (Split-Path $dll -Leaf)
            Start-Sleep -Seconds 3
            if (-not (Test-Path -LiteralPath $dll)) {
                # Si aun asi desaparece, se vuelve a poner la anterior para no quedarse sin plug-in
                if ($old -and (Test-Path -LiteralPath $old)) { Copy-Item -LiteralPath $old -Destination $dll -Force }
                throw "La DLL nueva ha desaparecido despues de instalarla. $antivirus"
            }
            $estado.dll_nueva = $true
        } catch {
            if (Test-Path -LiteralPath $prep) { Remove-Item -LiteralPath $prep -Force -ErrorAction SilentlyContinue }
            $msg = $_.Exception.Message
            if ($msg -notmatch 'antivirus') { $msg = "No he podido dejar la DLL nueva en $dll ($msg). Puede que haga falta ser administrador para escribir en esa carpeta." }
            $estado.errores += $msg
        }
    }

    # ------------------------------------------------------------------------ 2b. el panel de sesiones
    # Se usa desde una copia por version (panel-app\<version>), no desde el repositorio: los ficheros de un panel en
    # marcha estan en uso y el reset de la proxima actualizacion no podria sustituirlos.
    $panelRepo = Join-Path $repo 'panel'
    $fVerPanel = Join-Path $panelRepo 'VERSION'
    if (Test-Path -LiteralPath $fVerPanel) {
        try {
            $verPanel = ([System.IO.File]::ReadAllText($fVerPanel)).Trim() -replace '[^\w.-]', ''
            if (-not $verPanel) { throw 'la version del panel esta vacia.' }
            $dirApps = Join-Path $dirPlugin 'panel-app'
            $destino = Join-Path $dirApps $verPanel
            if (-not (Test-Path -LiteralPath (Join-Path $destino 'anfitrion.js'))) {
                New-Item -ItemType Directory -Force -Path $dirApps | Out-Null
                $tmp = "$destino.tmp"
                if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
                Copy-Item -LiteralPath $panelRepo -Destination $tmp -Recurse -Force
                if (Test-Path -LiteralPath $destino) { Remove-Item -LiteralPath $destino -Recurse -Force }
                Rename-Item -LiteralPath $tmp -NewName (Split-Path $destino -Leaf)
                $estado['panel'] = $verPanel
            }
            [System.IO.File]::WriteAllText((Join-Path $dirApps 'actual.txt'), $destino, $utf8)
            # Las versiones anteriores se borran cuando ya no hay ningun proceso del panel usandolas
            $enUso = @(Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" -ErrorAction SilentlyContinue | ForEach-Object { [string]$_.CommandLine })
            foreach ($d in Get-ChildItem -LiteralPath $dirApps -Directory) {
                if ($d.FullName -eq $destino) { continue }
                if ($enUso | Where-Object { $_.IndexOf($d.FullName, [StringComparison]::OrdinalIgnoreCase) -ge 0 }) { continue }
                Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        } catch {
            $estado.errores += "No he podido instalar el panel de sesiones: $($_.Exception.Message)"
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

# ============================================================================== 5. novedades
# CAMBIOS.md por secciones («## 1.9 (EDISA)» y lo que va debajo)
function Secciones([string]$md) {
    $lista = New-Object System.Collections.Generic.List[object]
    $actual = $null
    foreach ($l in ($md -replace "`r", '') -split "`n") {
        if ($l -match '^##\s+(.+)$') { $actual = [pscustomobject]@{ titulo = $Matches[1].Trim(); lineas = New-Object System.Collections.Generic.List[string] }; $lista.Add($actual) }
        elseif ($actual) { $actual.lineas.Add($l) }
    }
    foreach ($s in $lista) { $s | Add-Member -NotePropertyName texto -NotePropertyValue (($s.lineas -join "`n").Trim()) }
    return $lista
}

# Lo poco de Markdown que usa CAMBIOS.md: listas (con continuacion sangrada y un nivel anidado), parrafos, ###,
# tablas, **negrita**, *cursiva*, `codigo` y [enlaces](url)
function Enlinea([string]$t) {
    $t = [System.Net.WebUtility]::HtmlEncode($t)
    $t = [regex]::Replace($t, '`([^`]+)`', '<code>$1</code>')
    $t = [regex]::Replace($t, '\*\*(.+?)\*\*', '<strong>$1</strong>')
    $t = [regex]::Replace($t, '(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', '<em>$1</em>')
    $t = [regex]::Replace($t, '\[([^\]]+)\]\((https?://[^)\s]+)\)', '<a href="$2">$1</a>')
    return $t
}
function Html([string]$md) {
    $sb = New-Object System.Text.StringBuilder
    # El estado va en $script: porque los bloques que cierran (& $cerrar) corren en su propio ambito
    $cerrarLi = { if ($null -ne $script:li) { [void]$sb.Append('<li>' + (Enlinea $script:li) + '</li>'); $script:li = $null } }
    $script:li = $null
    $cerrar = {
        & $cerrarLi
        while ($script:niveles -gt 0) { [void]$sb.Append('</ul>'); $script:niveles-- }
        if ($null -ne $script:parrafo) { [void]$sb.Append('<p>' + (Enlinea $script:parrafo) + '</p>'); $script:parrafo = $null }
        if ($script:tabla) { [void]$sb.Append('</table>'); $script:tabla = $false }
    }
    $script:niveles = 0; $script:parrafo = $null; $script:tabla = $false
    foreach ($l in ($md -replace "`r", '') -split "`n") {
        if (-not $l.Trim()) { & $cerrar; continue }
        if ($l -match '^(\s*)[-*]\s+(.*)$') {
            $nivel = if ($Matches[1].Length -ge 2) { 2 } else { 1 }; $texto = $Matches[2]
            if ($null -ne $script:parrafo -or $script:tabla) { & $cerrar }
            & $cerrarLi
            while ($script:niveles -lt $nivel) { [void]$sb.Append('<ul>'); $script:niveles++ }
            while ($script:niveles -gt $nivel) { [void]$sb.Append('</ul>'); $script:niveles-- }
            $script:li = $texto
        }
        elseif ($l -match '^###\s+(.*)$') { & $cerrar; [void]$sb.Append('<h3>' + (Enlinea $Matches[1]) + '</h3>') }
        elseif ($l -match '^\s*\|(.*)\|\s*$') {
            $celdas = $Matches[1] -split '\|'
            if ($celdas -join '' -match '^[\s:-]+$') { continue }   # la linea |---|---|
            if (-not $script:tabla) { & $cerrar; [void]$sb.Append('<table>'); $script:tabla = $true }
            [void]$sb.Append('<tr>' + (($celdas | ForEach-Object { '<td>' + (Enlinea $_.Trim()) + '</td>' }) -join '') + '</tr>')
        }
        elseif ($null -ne $script:li) { $script:li += ' ' + $l.Trim() }
        elseif ($null -ne $script:parrafo) { $script:parrafo += ' ' + $l.Trim() }
        else { & $cerrar; $script:parrafo = $l.Trim() }
    }
    & $cerrar
    return $sb.ToString()
}

function AbrirVentana([string]$fichero) {
    $edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
              "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    $url = ([Uri]$fichero).AbsoluteUri
    if ($edge) { Start-Process -FilePath $edge -ArgumentList "--app=`"$url`"" } else { Start-Process -FilePath $fichero }
}

# Se compara con lo ultimo que se enseño (o, la primera vez, con lo que habia antes de esta actualizacion). En una
# instalacion nueva no hay nada con que comparar: no se enseña nada.
try {
    $cambios = Leer (Join-Path $repo 'CAMBIOS.md')
    $fVistas = Join-Path $dirPlugin 'novedades-vistas.md'
    $base = if (Test-Path -LiteralPath $fVistas) { Leer $fVistas } else { $cambiosAntes }
    if ($cambios -and $base -and $cambios -ne $base) {
        $previas = @{}; foreach ($s in Secciones $base) { $previas[$s.titulo] = $s.texto }
        $todas = @(Secciones $cambios)
        $nuevas = @($todas | Where-Object { -not $previas.ContainsKey($_.titulo) -or $previas[$_.titulo] -ne $_.texto })
        if ($nuevas.Count -gt 0 -and (Ajuste 'MostrarNovedades' 'si') -notmatch '^(no|n|false|0)$') {
            $cuerpo = New-Object System.Text.StringBuilder
            foreach ($s in $nuevas) {
                $etiqueta = if ($previas.ContainsKey($s.titulo)) { '<span class="etq">ampliada</span>' } else { '<span class="etq">nueva</span>' }
                [void]$cuerpo.Append("<section><h2>Versión $(Enlinea $s.titulo) $etiqueta</h2>$(Html $s.texto)</section>")
            }
            $anteriores = @($todas | Where-Object { $nuevas -notcontains $_ })
            if ($anteriores.Count -gt 0) {
                [void]$cuerpo.Append('<details><summary>Versiones anteriores</summary>')
                foreach ($s in $anteriores) { [void]$cuerpo.Append("<section><h2>Versión $(Enlinea $s.titulo)</h2>$(Html $s.texto)</section>") }
                [void]$cuerpo.Append('</details>')
            }
            $aviso = if ($estado.dll_nueva) { '<p class="aviso">Los cambios del plug-in se aplican cuando reinicies PL/SQL Developer. Las skills y el panel de sesiones ya están al día.</p>' } else { '' }
            $html = @"
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Novedades del plug-in</title>
<style>
:root { --fondo:#f7f7f5; --papel:#ffffff; --texto:#1f2328; --suave:#5d646d; --linea:#e2e2de; --acento:#b5562b; --codigo:#f0efeb; }
@media (prefers-color-scheme: dark) { :root { --fondo:#1b1c1e; --papel:#242528; --texto:#e8e6e3; --suave:#a3a7ad; --linea:#36383c; --acento:#e0895f; --codigo:#2f3034; } }
* { box-sizing: border-box; }
body { margin:0; background:var(--fondo); color:var(--texto); font:15px/1.55 "Segoe UI", system-ui, sans-serif; }
main { max-width:860px; margin:0 auto; padding:28px 20px 40px; }
header h1 { font-size:22px; margin:0 0 4px; }
header p { margin:0; color:var(--suave); }
.aviso { margin:16px 0 0; padding:10px 14px; border-left:3px solid var(--acento); background:var(--papel); border-radius:4px; }
section { background:var(--papel); border:1px solid var(--linea); border-radius:8px; padding:6px 22px 14px; margin-top:18px; }
h2 { font-size:17px; margin:14px 0 8px; }
h3 { font-size:15px; margin:14px 0 6px; }
.etq { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.04em; color:var(--acento); border:1px solid var(--acento); border-radius:10px; padding:1px 8px; margin-left:6px; vertical-align:2px; }
ul { padding-left:22px; margin:6px 0; } li { margin:5px 0; }
code { font:13px Consolas, monospace; background:var(--codigo); padding:1px 5px; border-radius:4px; overflow-wrap:anywhere; }
table { border-collapse:collapse; margin:8px 0; width:100%; } td { border:1px solid var(--linea); padding:5px 8px; vertical-align:top; }
details { margin-top:26px; } summary { cursor:pointer; color:var(--suave); font-weight:600; }
a { color:var(--acento); }
</style></head>
<body><main>
<header><h1>Novedades de Claude Code para PL/SQL Developer</h1><p>Versión $([System.Net.WebUtility]::HtmlEncode($estado.version)) · actualizada el $(Get-Date -Format 'dd/MM/yyyy HH:mm')</p>$aviso</header>
$($cuerpo.ToString())
</main></body></html>
"@
            $fNovedades = Join-Path $dirPlugin 'novedades.html'
            [System.IO.File]::WriteAllText($fNovedades, $html, $utf8)
            $estado['novedades'] = $fNovedades
            if (-not $Instalando) { AbrirVentana $fNovedades }
        }
    }
    if ($cambios) { [System.IO.File]::WriteAllText($fVistas, $cambios, $utf8) }
} catch {
    $estado.errores += "No he podido preparar las novedades: $($_.Exception.Message)"
}

[System.IO.File]::WriteAllText($fEstado, (ConvertTo-Json -InputObject $estado -Depth 4), $utf8)
if ($Instalando) { $estado }
if ($estado.errores.Count -gt 0) { exit 1 }
exit 0
