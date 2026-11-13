# Manual: publicar el servidor MCP en App Service y conectarlo a Microsoft Foundry

Este manual documenta la configuracion que se consiguio hacer funcionar el 13 de noviembre de 2026. Incluye la preparacion de Microsoft Entra ID, el despliegue, la asignacion del permiso a la identidad del proyecto Foundry, la prueba manual y el diagnostico del error 403.

Los identificadores incluidos son referencias del entorno utilizado, no credenciales. Para repetirlo en otro entorno, sustituirlos por los de ese entorno. No copiar perfiles de publicacion, tokens, cookies ni secretos en este documento o en el repositorio.

## 1. Resultado y arquitectura

El flujo final es:

```text
Agente de Foundry, proyecto proj-default
    |
    | Solicita a Entra un token mediante la identidad administrada del proyecto
    | Recurso solicitado: client ID de la API MCP
    v
Microsoft Entra ID
    |
    | Token para la API MCP, con roles = ["Mcp.Tools.Invoke"]
    v
App Service Windows -> ASP.NET Core -> /mcp -> get_current_time / echo
```

El servidor usa .NET 10, el SDK oficial MCP y Streamable HTTP sin estado. Valida el token en el propio codigo ASP.NET Core y exige un scope delegado **o** un rol de aplicacion.

La comprobacion funcional final se hizo desde Foundry con `get current time`. El agente invoco `get_current_time` y respondio con la hora UTC. No se dio por validada la integracion solo porque la conexion apareciera creada.

### Dos formas de acceder, con permisos diferentes

| Cliente | Tipo de acceso | Claim requerido por esta configuracion |
| --- | --- | --- |
| MCP Inspector con token de usuario | Delegado | `scp` contiene `mcp.access` |
| Foundry con Project Managed Identity | Aplicacion, sin usuario | `roles` contiene `Mcp.Tools.Invoke` |

Que Inspector funcione no demuestra que Foundry tenga el rol. Una identidad administrada no usa el scope delegado para sustituir al rol.

## 2. Inventario del entorno que funciono

| Dato | Valor |
| --- | --- |
| Tenant Entra | `01a46e9c-3fa4-4359-8466-ecf0acf053a8` |
| Nombre del registro de la API | `MCPServerTmp` |
| Application / client ID de la API | `65e1d4aa-c5d0-4441-bec3-a9a450700b93` |
| Object ID del app registration | `a5b4159f-5e3e-43e7-8d51-3848bf2fb3f2` |
| Object ID del service principal de la API | `fdee1e73-e293-4a49-b067-470d5d69f1e5` |
| Application ID URI | `api://65e1d4aa-c5d0-4441-bec3-a9a450700b93` |
| Scope delegado | `mcp.access` |
| Scope completo | `api://65e1d4aa-c5d0-4441-bec3-a9a450700b93/mcp.access` |
| Nombre visible del app role | `MCP Access` |
| Valor del app role | `Mcp.Tools.Invoke` |
| ID del app role | `a11aa0ee-3525-4f59-8215-cd3e9e389308` |
| App Service | `mcpservertmp` |
| Grupo del App Service | `GerbotCoach` |
| Sistema / runtime | Windows / .NET 10 |
| Region / plan utilizados | West Europe / F1, para pruebas |
| URL base | `https://mcpservertmp-dhh8drdqe7fmhba4.westeurope-01.azurewebsites.net` |
| Endpoint MCP | `https://mcpservertmp-dhh8drdqe7fmhba4.westeurope-01.azurewebsites.net/mcp` |
| Recurso Foundry | `gerbotFoundryDevInstance` |
| Proyecto Foundry | `proj-default` |
| Nombre de la identidad del proyecto | `gerbotFoundryDevInstance/projects/proj-default` |
| Principal / Object ID de la identidad del proyecto | `01e59302-dd80-43ed-9c4b-0dc60cdd184e` |
| Client ID de la identidad del proyecto | `57e0a85a-b6f8-4d61-b74f-8760542ade0d` |
| Audience final en la conexion Foundry | `65e1d4aa-c5d0-4441-bec3-a9a450700b93` |

**No confundir estos identificadores:**

- El **client ID de la API** identifica al destinatario del token.
- El **Object ID del service principal de la API** es el `resourceId` de la asignacion del app role.
- El **Object ID del app registration** no es el `resourceId` de esa asignacion.
- El **Principal / Object ID de la identidad del proyecto** es el `principalId` de la asignacion.
- El **client ID de la identidad del proyecto** no debe usarse como `principalId`.
- El **app role ID** es un GUID; su **value** es el texto que debe aparecer en `roles`.

Los recursos Foundry y App Service estaban en suscripciones diferentes, pero en el mismo tenant. No es necesario moverlos a la misma suscripcion para reproducir este flujo; si se cambian tenant o modelo de identidad, hay que revisar el diseno de autenticacion.

## 3. Requisitos y orden recomendado

Se necesita:

1. El codigo de este servidor en un repositorio GitHub con Actions habilitado.
2. .NET SDK 10 si se desea compilar o ejecutar localmente.
3. Azure CLI y acceso al tenant.
4. Permisos para crear/configurar el App Service y el proyecto Foundry.
5. Permisos de directorio adecuados para crear el app role y asignarlo al service principal. Ser Contributor de una suscripcion no concede por si solo esos permisos de Entra.
6. Un modelo desplegado en Foundry que soporte herramientas.

**Orden para evitar el problema que encontramos:** crear la API y su rol, identificar la identidad del proyecto, asignarle el rol y comprobar la asignacion **antes de la primera llamada de Foundry a esta API**.

Los comandos siguientes son para Bash o Zsh, como el terminal de macOS. Ejecutarlos desde la raiz del repositorio cuando usen rutas de archivos.

## 4. Preparar el registro de la API en Entra

### 4.1. Registrar la aplicacion

1. Abrir **Microsoft Entra ID -> App registrations -> New registration**.
2. Nombre: `MCPServerTmp`, o el del nuevo entorno.
3. Elegir **Accounts in this organizational directory only**.
4. Para esta API no hace falta un redirect URI para el flujo de identidad administrada.
5. Crear el registro y anotar el **Directory / tenant ID**, el **Application / client ID** y el **Object ID**.

Foundry con identidad administrada no necesita un client secret de esta API.

### 4.2. Configurar el identificador de recurso y el scope

1. En el registro, abrir **Expose an API**.
2. Configurar **Application ID URI** como `api://<API_CLIENT_ID>`.
3. Crear el scope `mcp.access`, habilitado, con nombre y descripcion del consentimiento adecuados a la organizacion.
4. Para limitar el consentimiento, se puede seleccionar que solo los administradores lo concedan.
5. Anotar el scope completo: `api://<API_CLIENT_ID>/mcp.access`.

Este scope sirve para clientes delegados, por ejemplo Inspector con un usuario. No concede permisos a la identidad administrada de Foundry.

### 4.3. Usar access tokens v2

En **Manifest**, dentro de `api`, configurar `requestedAccessTokenVersion` a `2`, conservando el resto de la definicion:

```json
{
  "api": {
    "requestedAccessTokenVersion": 2
  }
}
```

El bloque anterior es un fragmento, no un manifest completo para reemplazar el existente.

El servidor usa una authority especifica del tenant con `/v2.0`. En esta configuracion, el claim `aud` del access token v2 es el GUID del client ID de la API.

### 4.4. Crear el rol de aplicacion

Abrir **App roles -> Create app role**:

| Campo | Valor |
| --- | --- |
| Display name | `MCP Access` |
| Allowed member types | `Applications` |
| Value | `Mcp.Tools.Invoke` |
| Description | Permite invocar las herramientas de la API MCP |
| Enable this app role | Activado |

Guardar y anotar el GUID del rol. En este entorno fue `a11aa0ee-3525-4f59-8215-cd3e9e389308`.

Usar este valor distinto de `mcp.access`. Durante la configuracion hubo un error de valor duplicado al intentar otro nombre de rol; la configuracion que finalmente se verifico usa `Mcp.Tools.Invoke`.

### 4.5. Identificar y comprobar el service principal de la API

```bash
TENANT_ID="01a46e9c-3fa4-4359-8466-ecf0acf053a8"
API_CLIENT_ID="65e1d4aa-c5d0-4441-bec3-a9a450700b93"

az login --tenant "$TENANT_ID"

az ad sp show --id "$API_CLIENT_ID" \
  --query "{name:displayName,clientId:appId,objectId:id}" \
  --output table
```

Guardar el `objectId` como `API_SP_OBJECT_ID`. Si se esta creando una aplicacion nueva y todavia no existe su service principal, crearlo una sola vez:

```bash
az ad sp create --id "$API_CLIENT_ID"
```

No ejecutar ese ultimo comando si el service principal ya existe.

## 5. Identificar la identidad administrada del proyecto Foundry

1. Crear o abrir el proyecto Foundry que usara el agente.
2. Localizar la identidad administrada **del proyecto**, no solo la del recurso Foundry padre.
3. Si no esta habilitada, habilitarla en la configuracion de identidad del proyecto disponible en el portal.
4. Copiar su Principal / Object ID y comprobarlo en Entra.

Para el entorno documentado:

```bash
PROJECT_PRINCIPAL_ID="01e59302-dd80-43ed-9c4b-0dc60cdd184e"

az ad sp show --id "$PROJECT_PRINCIPAL_ID" \
  --query "{name:displayName,objectId:id,clientId:appId,type:servicePrincipalType}" \
  --output json
```

Resultado esperado:

```json
{
  "name": "gerbotFoundryDevInstance/projects/proj-default",
  "objectId": "01e59302-dd80-43ed-9c4b-0dc60cdd184e",
  "clientId": "57e0a85a-b6f8-4d61-b74f-8760542ade0d",
  "type": "ManagedIdentity"
}
```

Para consultar tambien el recurso Azure asociado:

```bash
az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/servicePrincipals/$PROJECT_PRINCIPAL_ID?%24select=id,appId,displayName,servicePrincipalType,alternativeNames" \
  --output json
```

En este caso, `alternativeNames` incluyo la ruta:

```text
/subscriptions/c16077a6-37d0-43d2-8eeb-20c5f6d3995c/resourcegroups/gerbotFoundry/providers/Microsoft.CognitiveServices/accounts/gerbotFoundryDevInstance/projects/proj-default
```

### Error importante que se cometio

Se asigno inicialmente el rol a `gerbotFoundryDevInstance`, con principal ID `74315315-a0f9-4fb6-9bd6-ee5241072e70`. Sin embargo, las llamadas llegaban desde `gerbotFoundryDevInstance/projects/proj-default`, con principal ID `01e59302-dd80-43ed-9c4b-0dc60cdd184e`.

Ambas identidades existen, pero no son intercambiables. Si ya hay llamadas fallidas, el campo `ObjectId` del log de autorizacion permite identificar al principal que realmente esta llamando.

## 6. Asignar el app role desde el terminal

La asignacion se hizo mediante Microsoft Graph porque no se pudo completar desde el selector del portal.

No usar `az role assignment create` para esta operacion: ese comando asigna roles Azure RBAC, no app roles de Entra.

### 6.1. Variables y comprobacion del rol

Mantener estas variables en la misma sesion del terminal:

```bash
TENANT_ID="01a46e9c-3fa4-4359-8466-ecf0acf053a8"
API_CLIENT_ID="65e1d4aa-c5d0-4441-bec3-a9a450700b93"
API_SP_OBJECT_ID="fdee1e73-e293-4a49-b067-470d5d69f1e5"
PROJECT_PRINCIPAL_ID="01e59302-dd80-43ed-9c4b-0dc60cdd184e"
APP_ROLE_ID="a11aa0ee-3525-4f59-8215-cd3e9e389308"

az login --tenant "$TENANT_ID"

az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/servicePrincipals/$API_SP_OBJECT_ID?%24select=id,appId,displayName,appRoles" \
  --query "{name:displayName,clientId:appId,roles:appRoles[?id=='$APP_ROLE_ID'].{id:id,value:value,enabled:isEnabled,types:allowedMemberTypes}}" \
  --output json
```

Verificar que el `clientId` coincide con la API y que el rol tiene:

```json
{
  "value": "Mcp.Tools.Invoke",
  "enabled": true,
  "types": ["Application"]
}
```

### 6.2. Comprobar si la asignacion ya existe

```bash
az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/servicePrincipals/$PROJECT_PRINCIPAL_ID/appRoleAssignments" \
  --query "value[?resourceId=='$API_SP_OBJECT_ID' && appRoleId=='$APP_ROLE_ID'].{principal:principalId,resource:resourceId,role:appRoleId,created:createdDateTime}" \
  --output json
```

Si aparece la asignacion correcta, no repetir el POST. Si sale `[]`, continuar.

### 6.3. Crear la asignacion

Este comando concede a la identidad del proyecto el permiso de invocar el MCP:

```bash
az rest --method POST \
  --url "https://graph.microsoft.com/v1.0/servicePrincipals/$PROJECT_PRINCIPAL_ID/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{
    \"principalId\": \"$PROJECT_PRINCIPAL_ID\",
    \"resourceId\": \"$API_SP_OBJECT_ID\",
    \"appRoleId\": \"$APP_ROLE_ID\"
  }"
```

La respuesta debe identificar al proyecto como principal y a `MCPServerTmp` como recurso. Repetir despues la consulta GET del apartado anterior para verificar que quedo guardada.

Si aparece `Authorization_RequestDenied`, revisar los privilegios de directorio y los permisos de Microsoft Graph de la sesion con un administrador. La operacion de escritura requiere autorizacion para asignar app roles, por ejemplo el permiso delegado `AppRoleAssignment.ReadWrite.All` y un rol administrativo adecuado. No se resuelve asignando Contributor sobre el App Service.

## 7. Crear o preparar el App Service

En este entorno se utilizo:

- Publicacion de **codigo**, no un contenedor.
- **Windows**, runtime **.NET 10**, region **West Europe**.
- Nombre `mcpservertmp`.
- Plan **F1** para pruebas. No tomar este plan como recomendacion de disponibilidad para produccion.
- Endpoint HTTPS publico accesible desde Foundry.

El App Service ya existia cuando se preparo el workflow. El workflow del repositorio **no crea la infraestructura**.

### 7.1. Configuracion de autenticacion del servidor

En **App Service -> Settings -> Environment variables -> App settings**, crear:

| Nombre | Valor |
| --- | --- |
| `Authentication__Authority` | `https://login.microsoftonline.com/01a46e9c-3fa4-4359-8466-ecf0acf053a8/v2.0` |
| `Authentication__Audiences__0` | `65e1d4aa-c5d0-4441-bec3-a9a450700b93` |
| `Authentication__Scopes__0` | `api://65e1d4aa-c5d0-4441-bec3-a9a450700b93/mcp.access` |
| `Authentication__Roles__0` | `Mcp.Tools.Invoke` |

Guardar/aplicar los cambios y permitir que la aplicacion reinicie.

Detalles importantes:

- Son dos guiones bajos: `__`. ASP.NET Core los convierte en separadores de configuracion.
- El sufijo `__0` representa el primer elemento de una lista.
- El scope completo termina en `/mcp.access`, **no** en `/Mcp.Tools.Invoke`.
- El rol se configura solo como `Mcp.Tools.Invoke`, sin `api://`.
- No se necesita un secreto Entra para validar los JWT.
- No copiar `ASPNETCORE_URLS=http://localhost:5000` al App Service.
- No configurar el servidor como Development para arreglar la autenticacion.
- [.env.example](./.env.example) no se carga automaticamente; editarlo no cambia el App Service.
- No vaciar scopes y roles para evitar el 403: se eliminaria la exigencia de estos permisos.

El JWT se valida mediante [AuthenticationRegistration.cs](./src/MCPServer/Auth/AuthenticationRegistration.cs). No es necesario anadir Easy Auth para este flujo. Si el App Service ya tiene autenticacion de plataforma o restricciones adicionales, revisarlas por separado: pueden filtrar una llamada antes de que llegue a ASP.NET Core. No desactivar protecciones existentes sin evaluar su proposito.

## 8. Preparar GitHub Actions y desplegar

El workflow existente es [.github/workflows/deploy-app-service.yml](./.github/workflows/deploy-app-service.yml).

### 8.1. Perfil de publicacion

1. En el App Service, abrir **Configuration -> General settings**; la ubicacion puede variar segun la version del portal.
2. Habilitar **SCM Basic Auth Publishing Credentials** y guardar.
3. No hace falta habilitar la autenticacion basica FTP.
4. En **Overview**, descargar el publish profile.
5. En GitHub, abrir **Settings -> Secrets and variables -> Actions -> New repository secret**.
6. Crear `AZUREAPPSERVICE_PUBLISHPROFILE` con el contenido completo del perfil.
7. Eliminar o guardar de forma segura la copia descargada; nunca subirla al repositorio.

El perfil es una credencial privilegiada de despliegue, no un token para invocar el MCP. Si la politica de la organizacion prohibe este metodo, se necesita otro mecanismo de despliegue; este manual reproduce el metodo utilizado.

### 8.2. Ejecutar el workflow

1. Subir los cambios de codigo y workflow a la rama que se va a desplegar.
2. En GitHub, abrir **Actions -> Deploy MCPServer to App Service -> Run workflow**.
3. Seleccionar la rama, por ejemplo `main`, y ejecutar.
4. Verificar que todas las etapas terminan correctamente.

El workflow:

1. Descarga el codigo.
2. Instala el SDK .NET 10.
3. Restaura la solucion.
4. Ejecuta las pruebas en Release.
5. Publica `src/MCPServer/MCPServer.csproj`.
6. Empaqueta el resultado y lo despliega mediante `azure/webapps-deploy@v3`.

La ejecucion es manual; no se dispara por cada push. No configura Entra, las variables de App Service ni las asignaciones del rol.

Para otro App Service, cambiar el `app-name: mcpservertmp` del workflow y actualizar el secreto con el perfil del sitio correcto.

## 9. Verificar el despliegue antes de usar Foundry

```bash
BASE_URL="https://mcpservertmp-dhh8drdqe7fmhba4.westeurope-01.azurewebsites.net"

curl --max-time 30 -i "$BASE_URL/health/live"
curl --max-time 30 -i "$BASE_URL/health/ready"
curl --max-time 30 -i "$BASE_URL/.well-known/oauth-protected-resource/mcp"

curl --max-time 30 -i "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Resultados esperados:

| Peticion | Resultado |
| --- | --- |
| `/health/live` | `200`, `{"status":"ok"}` |
| `/health/ready` | `200`, `{"status":"ok"}` si la aplicacion esta lista y tiene configuracion requerida |
| Metadata OAuth | `200`, autoridad del tenant y scope anunciado |
| `POST /mcp` sin token | `401`, esperado |

Un readiness 200 no comprueba que la identidad de Foundry tenga el rol ni ejecuta una herramienta. Tampoco es un fallo que la raiz o `/mcp` abiertos sin token en un navegador devuelvan 401.

Este servidor es stateless: un `GET /mcp` autenticado puede devolver 405 por diseno. La prueba funcional principal debe ser una llamada MCP por POST, no esperar una pagina HTML en `/mcp`.

### Comprobacion opcional con MCP Inspector

Para aislar problemas de servidor frente a problemas de Foundry:

1. Ejecutar Inspector localmente:

   ```bash
   npx @modelcontextprotocol/inspector
   ```

2. Abrir la URL local que muestre la herramienta.
3. Elegir **Streamable HTTP** y el endpoint HTTPS terminado en `/mcp`.
4. Para reproducir la prueba realizada, configurar manualmente el bearer token en la opcion de autenticacion/headers de la version de Inspector utilizada.
5. Usar un token valido de esta API, emitido a un cliente autorizado, con `scp=mcp.access` o el rol aceptado.
6. Conectar, listar herramientas e invocar `get_current_time`.

No pegar el token en la conversacion ni guardarlo en el repositorio. La autorizacion delegada del cliente que obtiene el token debe estar configurada en Entra; no cualquier token de `az login` sirve para esta API.

Durante esta sesion el OAuth automatico de Inspector intento Dynamic Client Registration y fallo porque Entra no ofrece ese registro dinamico. La prueba que se completo fue con bearer token manual. Configurar un cliente OAuth estatico para Inspector es un procedimiento diferente y no es necesario para la conexion de identidad administrada de Foundry.

## 10. Crear la conexion MCP en Foundry

Hacerlo despues de verificar el rol y su asignacion.

1. Abrir el proyecto correcto, en este caso `proj-default`.
2. Ir a **Tools -> Connect a tool**, o al agente **Tools -> Add**, segun la version de la interfaz.
3. Elegir una herramienta personalizada de tipo **Model Context Protocol / MCP**.
4. Configurar:

| Campo | Valor final utilizado |
| --- | --- |
| Nombre de conexion | Un nombre unico; la prueba final mostro `mcpservertmp06` |
| Remote MCP server endpoint | `https://mcpservertmp-dhh8drdqe7fmhba4.westeurope-01.azurewebsites.net/mcp` |
| Authentication | `Microsoft Entra` |
| Identity | `Project Managed Identity` |
| Audience | `65e1d4aa-c5d0-4441-bec3-a9a450700b93` |

5. Guardar y asociar la conexion al agente.
6. Habilitar las herramientas necesarias.
7. Guardar la version/configuracion del agente que se vaya a probar.

**La audience final es el client ID de la API MCP, sin `api://`, sin `/mcp.access` y sin `/.default`. No es el client ID de la identidad de Foundry ni la URL del App Service.**

### Por que hay un URI en Entra pero un GUID en Foundry y App Service

Son conceptos relacionados, pero no campos intercambiables:

- `api://<API_CLIENT_ID>`: Application ID URI registrado para la API.
- `api://<API_CLIENT_ID>/mcp.access`: scope solicitado por clientes delegados.
- `<API_CLIENT_ID>`: valor `aud` esperado en los access tokens v2 y configurado en App Service.
- `<API_CLIENT_ID>`: recurso/audience usado en la conexion Foundry que finalmente funciono.

En este entorno se probo primero el URI `api://...`. Tras corregir la asignacion del rol, cambiar la audience de Foundry al GUID fue el cambio que precedio a la primera llamada correcta. No fue necesario cambiar la audience del servidor.

## 11. Prueba funcional final

1. Abrir el playground del agente que tiene asociada la conexion.
2. Enviar:

   ```text
   get current time
   ```

3. Si aparece una solicitud de aprobacion, aprobar la llamada.
4. Comprobar en el contexto o en **Traces** que se ha invocado `get_current_time`, no que el modelo haya inventado una hora.
5. Verificar que la herramienta devuelve un valor UTC y que el agente responde sin el error generico.

En la prueba final documentada, Foundry mostro `get_current_time`, la aprobacion de la solicitud y una respuesta con la hora `2026-11-13 01:15 UTC`.

Para una comprobacion adicional se puede invocar `echo` con un texto reconocible y comprobar que lo devuelve.

## 12. Diagnostico: 401, 403 y registros

### 12.1. Guia rapida

| Sintoma | Comprobacion |
| --- | --- |
| 401 sin token | Esperado en las rutas protegidas |
| 401 con token | Token caducado/invalido, authority, issuer, audiencia o autenticacion de plataforma |
| 403 con `TokenRoles` vacio | El token no aporta el rol; comprobar principal, asignacion y cache |
| 403 con otro `ObjectId` | Se esta usando una identidad diferente de la que recibio el rol |
| Readiness 503 | Revisar configuracion requerida e inicio de la aplicacion |
| Error generico de Foundry | Buscar peticion MCP y error real en Traces y logs del servidor |
| 405 en `GET /mcp` autenticado | Puede ser esperado: servidor stateless |
| Inspector funciona y Foundry no | Comparar acceso delegado con acceso de aplicacion; no son el mismo permiso |

### 12.2. Logging de App Service

1. Abrir **Monitoring -> App Service logs**.
2. Habilitar **Application logging (Filesystem)** con nivel que incluya Warning; Information ayuda para peticiones/herramientas.
3. Guardar y abrir **Log stream -> Application logs**.
4. Reproducir una llamada y anotar su hora.

Los logs detallados de IIS pueden aparecer mezclados en el stream. El texto HTML `HTTP Error 403.0 - Forbidden` no contiene los claims y no basta para decidir que ajuste cambiar.

`Logon Method: Anonymous` en IIS no demuestra que ASP.NET Core haya recibido una peticion sin bearer token.

### 12.3. Log de autorizacion del codigo

[ScopeOrRoleAuthorization.cs](./src/MCPServer/Auth/ScopeOrRoleAuthorization.cs) registra al denegar:

```text
MCP authorization denied.
ObjectId=01e59302-dd80-43ed-9c4b-0dc60cdd184e;
Audiences=65e1d4aa-c5d0-4441-bec3-a9a450700b93;
TokenScopes=;
TokenRoles=;
ExpectedScopes=api://65e1d4aa-c5d0-4441-bec3-a9a450700b93/mcp.access;
ExpectedRoles=Mcp.Tools.Invoke
```

Interpretacion:

- `ObjectId`: principal que presento el token; resolverlo con `az ad sp show --id`.
- `Audiences`: audiencia presente en el token.
- `TokenScopes` y `TokenRoles`: permisos recibidos.
- `ExpectedScopes` y `ExpectedRoles`: configuracion efectiva del servidor.

Para Foundry es normal que `TokenScopes` este vacio. Lo que falta en el ejemplo es el rol.

Este mensaje se emite **solo al denegar**. Tras resolverlo no hay que esperar una linea equivalente con `TokenRoles=Mcp.Tools.Invoke`: el handler no registra los permisos en caso de exito. Usar la llamada funcional, Traces y el log de ejecucion de la herramienta como evidencia de exito.

### 12.4. Si el stream no muestra el aviso: consultar eventlog.xml

En esta sesion, el aviso no era visible en Log stream, pero si estaba en el registro de eventos:

1. Ir a **Development Tools -> Advanced Tools -> Go** para abrir Kudu.
2. Abrir **Debug console -> CMD**.
3. Navegar a `C:\home\LogFiles`.
4. Descargar o consultar `eventlog.xml`.
5. Buscar `MCP authorization denied` y revisar las entradas mas recientes y su `TimeCreated`.

El nombre de una descarga puede cambiar, por ejemplo `eventlog-2.xml`; sigue siendo el registro de eventos, no una traza FREB.

Las fechas terminadas en `Z` son UTC. Durante esta sesion, la hora local era UTC+1. No confundir logs antiguos que reaparecen al conectar el stream con peticiones nuevas.

No publicar el XML completo sin revisarlo: otros eventos pueden incluir informacion sensible.

### 12.5. Failed request tracing, si sigue siendo necesario

1. En **App Service logs**, activar **Failed request tracing** y guardar.
2. Reproducir una llamada fallida.
3. En Kudu, buscar la traza de esa peticion en `C:\home\LogFiles`, normalmente bajo `W3SVC...` o `FailedReqLogFiles\W3SVC...`, segun la configuracion.
4. Abrir el XML de solicitud mas reciente, normalmente `fr....xml`.
5. Buscar `MODULE_SET_RESPONSE_ERROR_STATUS` y sus datos de modulo, notificacion y estado.

Si no se genera un XML, revisar la habilitacion/regla de tracing para ese estado y la instancia atendiendo la peticion. No confundir `eventlog.xml` con una traza de solicitud.

No compartir headers `Authorization`, cookies ni tokens de estas trazas. Desactivar el tracing cuando deje de necesitarse.

## 13. El problema concreto de cache que encontramos

### Hechos verificados

1. La primera asignacion se hizo a la identidad del recurso Foundry padre, no a la del proyecto.
2. Los logs del servidor revelaron el Object ID real del proyecto.
3. Se asigno `Mcp.Tools.Invoke` a esa identidad y se comprobo en Microsoft Graph.
4. Se verifico que el rol estaba habilitado para `Application`.
5. Las llamadas siguientes seguian llegando con `TokenRoles` vacio.
6. Los registros de Entra consultados mostraban una emision/acceso anterior a la asignacion y no mostraban todavia uno posterior para esta API.
7. Al cambiar en Foundry la audience de `api://<API_CLIENT_ID>` a `<API_CLIENT_ID>`, la llamada funciono.

### Cronologia observada, en UTC

| Hora | Hecho |
| --- | --- |
| 2026-11-12 23:24:17 | Registro de inicio de sesion de la identidad del proyecto para `MCPServerTmp` |
| 2026-11-13 00:53:30 | Creacion de la asignacion correcta del app role al proyecto |
| 2026-11-13 01:06:17 | Denegacion en el servidor: misma identidad, `TokenRoles` vacio |
| 2026-11-13 01:15 aprox. | Invocacion correcta de `get_current_time` desde Foundry tras cambiar la audience |

### Interpretacion y limites de la evidencia

El comportamiento es consistente con un token anterior a la asignacion, reutilizado desde la cache de identidad administrada. Microsoft documenta que la infraestructura mantiene cache por URI de recurso, que los cambios de roles pueden tardar varias horas y que no hay un mecanismo general para forzar su renovacion antes de expirar.

Cambiar el recurso solicitado del URI al GUID funciono en este entorno. No debe presentarse como una API oficial para purgar la cache ni como garantia para cualquier recurso o tipo de token.

No se inspeccionaron los claims `iat` y `exp` del token de Foundry. Los registros de inicio de sesion pueden llevar retraso; no permiten calcular la hora exacta a la que se renovara un token. No dar por hecho que expirara exactamente 24 horas despues del primer registro observado.

Incognito, cerrar sesion del navegador, reiniciar el App Service o recrear una conexion no garantizan vaciar esa cache de infraestructura.

### Como evitarlo al repetir el proceso

1. Preparar la API y el rol.
2. Crear/identificar la identidad del proyecto.
3. Asignar y verificar el rol **antes de la primera peticion de token**.
4. Configurar Foundry desde el principio con la audience final documentada: el GUID de la API.
5. Si vuelve a fallar, consultar los claims resumidos del log antes de cambiar configuraciones.

No crear otro rol, quitar la autorizacion o regenerar la identidad solo para intentar forzar una renovacion.

### Consulta opcional de inicios de sesion

Requiere permisos para leer los registros de Entra; puede necesitar `AuditLog.Read.All` y los requisitos de licencia/rol del tenant. La API usada es beta y puede cambiar:

```bash
az rest --method GET \
  --url "https://graph.microsoft.com/beta/auditLogs/signIns?%24filter=signInEventTypes/any(t:t%20eq%20'managedIdentity')%20and%20servicePrincipalId%20eq%20'$PROJECT_PRINCIPAL_ID'&%24top=10&%24orderby=createdDateTime%20desc" \
  --query "value[].{created:createdDateTime,resource:resourceDisplayName,resourceId:resourceId,errorCode:status.errorCode}" \
  --output table
```

La ausencia de un registro reciente no demuestra por si sola que no se emitio un token. Combinar esa informacion con los logs del servidor y una prueba real.

## 14. Limpieza y mantenimiento

- Conservar la asignacion al proyecto que realmente utiliza el MCP.
- Si el recurso Foundry padre no necesita acceso, quitar solo su asignacion sobrante, despues de comprobar que ningun otro consumidor la usa.
- Mantener o retirar conscientemente el aviso de diagnostico del handler; no registra el bearer token, pero los identificadores/claims requieren un tratamiento adecuado.
- Desactivar Failed request tracing y detailed error logging cuando termine el diagnostico.
- Proteger el publish profile en GitHub Secrets y regenerarlo si se expone.
- No habilitar permisos mas amplios para resolver un error de token.
- Revisar plan, disponibilidad, red, observabilidad y metodo de despliegue antes de usarlo en produccion; F1 se utilizo solo para pruebas.

## 15. Lista final de comprobacion

| Elemento | Valor/condicion que debe verificarse |
| --- | --- |
| API de Entra | Single tenant, access tokens v2 |
| Scope | `mcp.access` |
| Rol | `Mcp.Tools.Invoke`, habilitado para `Application` |
| Principal asignado | Identidad del proyecto, no la del recurso padre |
| Recurso de la asignacion | Service principal de la API MCP |
| App settings | Authority, audience GUID, scope completo y rol |
| Despliegue | Workflow correcto y ultima version subida |
| Salud | Liveness y readiness 200 |
| Proteccion | MCP sin token devuelve 401 |
| Foundry | Microsoft Entra + Project Managed Identity |
| Audience Foundry final | Client ID GUID de la API |
| Endpoint | URL HTTPS terminada en `/mcp` |
| Prueba real | `get_current_time` invocada y resultado correcto |
| Credenciales | Ningun token ni perfil de publicacion en el repositorio |

## 16. Referencias

- [Guia corta del despliegue de este repositorio](./DEPLOYMENT.md).
- [Asignar un app role a una identidad administrada mediante Azure CLI](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/assign-app-role-managed-identity-azure-cli).
- [Limitaciones y cache de autorizacion de identidades administradas](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/managed-identity-best-practice-recommendations#limitation-of-using-managed-identities-for-authorization).
- [Crear y conectar un servidor MCP a Foundry](https://learn.microsoft.com/azure/foundry/mcp/build-your-own-mcp-server#connect-the-mcp-server-to-agent-service). Parte de los ejemplos oficiales usa Azure Functions y autenticacion de plataforma; este repositorio usa App Service y validacion JWT en ASP.NET Core.
