# Configura el envio de correos del sistema (Gmail) guardando los datos como "secretos de usuario".
# La clave se escribe aqui, en tu propia terminal: no queda en ningun archivo del proyecto ni en git.
#
# Antes: en la cuenta de Gmail que ENVIARA los avisos activa la verificacion en 2 pasos y crea una
# "contrasena de aplicacion" en https://myaccount.google.com/apppasswords (son 16 letras).
$ErrorActionPreference = 'Stop'
$api = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..\src\Inventory.Api'
if (-not (Test-Path $api)) { throw "No encuentro la carpeta de la API en $api" }
Set-Location $api

Write-Host ''
Write-Host 'Configurar el envio de correos del sistema de inventario' -ForegroundColor Cyan
$usuario = Read-Host 'Gmail que ENVIA los avisos (ej. tucuenta@gmail.com)'
$segura = Read-Host 'Contrasena de aplicacion de 16 letras (no se muestra)' -AsSecureString
$clave = [System.Net.NetworkCredential]::new('', $segura).Password
$clave = $clave -replace '\s', ''   # Google la muestra con espacios; sin espacios funciona igual
if ([string]::IsNullOrWhiteSpace($usuario) -or $clave.Length -lt 8) { throw 'Falta el correo o la contrasena de aplicacion.' }

dotnet user-secrets set 'Correo:Habilitado' 'true' | Out-Null
dotnet user-secrets set 'Correo:Usuario' $usuario | Out-Null
dotnet user-secrets set 'Correo:Clave' $clave | Out-Null
# Con cada persona recibiendo en su propio buzon no se redirige nada.
dotnet user-secrets remove 'Correo:DestinatarioDePrueba' 2>$null | Out-Null

Write-Host ''
Write-Host 'Listo. Datos guardados (la clave no se muestra):' -ForegroundColor Green
dotnet user-secrets list | ForEach-Object { if ($_ -like 'Correo:Clave*') { 'Correo:Clave = (oculta)' } else { $_ } }
Write-Host ''
Write-Host 'Siguiente: reinicia la API. En su log debe decir "Correo habilitado: smtp.gmail.com:587 como ...".'
Write-Host 'Para probar sin esperar un aviso real: entra como administrador y llama a POST /api/correo/prueba.'
