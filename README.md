# XmlTcpAccountsLab

**Laboratorio educativo de comunicación TCP/XML con .NET 10, C#, Entity Framework Core y PostgreSQL.**

Este proyecto implementa un servidor TCP para consultar cuentas asociadas a un DNI y desactivar cuentas. Incluye un cliente de consola interactivo para probar las operaciones. **No expone endpoints HTTP/REST**: la integración con un consumidor Java REST se realiza mediante un adaptador que genera XML (por ejemplo, con FreeMarker), abre un socket TCP y procesa la respuesta.

> **Estado:** se verificó el arranque del servidor en `127.0.0.1:9000`, la conexión a PostgreSQL, la lectura de 4 personas y 4 cuentas, el intercambio TCP/XML y una consulta `CONSULT_ACCOUNT` con `All=true` que devolvió dos cuentas. El usuario confirmó el funcionamiento del cliente con menú. Las evidencias de cada caso de desactivación, consulta individual y códigos de error deben registrarse por separado antes de declararlas completamente probadas.

## 1. Objetivos y alcance

- Aprender sockets TCP en C#/.NET 10 sin utilizar REST como transporte interno.
- Implementar un protocolo simple: **4 bytes de longitud (int32 big-endian) + XML UTF-8**.
- Separar transporte, interpretación XML, casos de uso, persistencia y cliente de pruebas.
- Consultar todas las cuentas de un DNI o una cuenta específica.
- Desactivar una cuenta activa y conservar el cambio en PostgreSQL.
- Disponer de un cliente interactivo para pruebas manuales.

**Fuera del alcance del laboratorio:** autenticación, autorización, TLS, alta disponibilidad, gestión productiva de concurrencia y exposición pública del puerto.

## 2. Arquitectura

```text
XmlTcpAccounts.Client (.NET, consola)
  └─ Program: menú / construcción de XML
       └─ Client.Transport.TcpMessageFraming
            │ TCP 127.0.0.1:9000
            │ [4 bytes big-endian][XML UTF-8]
            ▼
XmlTcpAccounts.Server
  └─ Workes.TcpServerWorker
       ├─ Server.Transport.TcpMessageFraming
       └─ Protocol.XmlRequestDispatcher
            ▼
XmlTcpAccounts.Application
  ├─ ConsultAccountUseCase
  ├─ DeactivateAccountUseCase
  └─ Puertos de repositorio
            ▼
XmlTcpAccounts.Infrastructure
  ├─ EF Core / Npgsql
  └─ AccountsDbContext / repositorios
            ▼
PostgreSQL: xmltcp_accounts
```

### Proyectos de la solución

| Proyecto | Responsabilidad |
|---|---|
| `XmlTcpAccounts.Domain` | Entidades y conceptos de dominio (`Person`, `Account`, estados y monedas) |
| `XmlTcpAccounts.Application` | Contratos, puertos y casos de uso |
| `XmlTcpAccounts.Infrastructure` | Persistencia mediante EF Core y Npgsql |
| `XmlTcpAccounts.Server` | Host .NET, listener TCP, framing y dispatcher XML |
| `XmlTcpAccounts.Client` | Consola interactiva para construir, enviar y leer solicitudes |

> El namespace del worker compartido durante el desarrollo es `XmlTcpAccounts.Server.Workes` (sin la `r` de `Workers`). Verificar el nombre efectivo antes de copiar código o ajustar `using`.

## 3. Requisitos

- **.NET SDK 10**.
- **PostgreSQL**, levantado con Podman Compose o Docker Compose según la configuración local del repositorio.
- Rider u otro IDE compatible con .NET; terminal para las verificaciones.
- Puerto TCP `9000` disponible en el equipo donde corre el servidor.

En el laboratorio se utilizó PostgreSQL en `localhost:5432`, base `xmltcp_accounts`. La cadena de conexión se obtiene de `ConnectionStrings:AccountsDatabase`; **no publicar credenciales reales en el repositorio**. Para desarrollo local puede definirse mediante variable de entorno `ConnectionStrings__AccountsDatabase` o configuración de usuario no versionada.

## 4. Arranque local

1. Iniciar PostgreSQL con el `compose` existente del proyecto y comprobar que se ejecutaron los scripts de inicialización.
2. Configurar `ConnectionStrings__AccountsDatabase` en el perfil local de ejecución del servidor.
3. Compilar la solución y ejecutar `XmlTcpAccounts.Server` desde Rider o con `dotnet run --project <ruta-al-csproj-del-servidor>`.
4. Confirmar en los logs la conexión a la base de datos y el mensaje `TCP server listening on 127.0.0.1:9000`.
5. Ejecutar `XmlTcpAccounts.Client` desde Rider o con `dotnet run --project <ruta-al-csproj-del-cliente>`.
6. Seleccionar una opción del menú y completar DNI y, cuando corresponda, número de cuenta.

> Las rutas exactas de `.csproj` dependen de la estructura de la solución local. En el desarrollo se trabajó con proyectos `XmlTcpAccounts.Server` y `XmlTcpAccounts.Client`; no asumir que se encuentran bajo `src/` sin verificar el repositorio.

### Verificación del puerto (macOS)

```bash
lsof -nP -iTCP:9000 -sTCP:LISTEN
```

Salida observada en el laboratorio: un proceso `XmlTcpAcc...` en `TCP 127.0.0.1:9000 (LISTEN)`.

### Comportamiento del cliente

El menú interactivo contempla:

```text
1. Consultar todas las cuentas por DNI
2. Consultar una cuenta por DNI y número
3. Desactivar una cuenta
4. Enviar operación desconocida (prueba)
5. Enviar XML inválido (prueba)
0. Salir
```

Cada opción abre una conexión TCP, envía una solicitud XML, recibe la respuesta y regresa al menú. La opción **3 modifica datos de PostgreSQL** si la desactivación resulta exitosa.

## 5. Protocolo de transporte TCP

El servidor **no acepta XML plano sin framing** y tampoco recibe peticiones HTTP.

```text
┌──────────────────────────┬─────────────────────────────────┐
│ 4 bytes: longitud N      │ N bytes: XML codificado UTF-8   │
│ entero 32-bit big-endian │                                 │
└──────────────────────────┴─────────────────────────────────┘
```

- La longitud corresponde a los **bytes** UTF-8, no a los caracteres.
- El límite implementado en el framing mostrado durante el laboratorio es **1 MiB (1 048 576 bytes)**.
- La lectura debe obtener exactamente 4 bytes de cabecera y luego exactamente `N` bytes de contenido; una sola lectura TCP puede ser parcial.
- `TcpMessageFraming.WriteAsync` envía cabecera y payload; `ReadAsync` los reconstruye.
- El servidor admite mensajes sucesivos en una conexión; el cliente de consola abre una conexión por operación.
- La escucha actual está restringida a **loopback** (`127.0.0.1`). Un proceso en otra máquina o contenedor no podrá acceder sin rediseñar la exposición y los controles de seguridad.

## 6. Contrato XML

### 6.1 Consultar todas las cuentas — `CONSULT_ACCOUNT`

```xml
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>REQ-000001</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>40581711</DocumentNumber>
    <All>true</All>
  </Body>
</Request>
```

**Respuesta exitosa observada:**

```xml
<Response>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>REQ-000001</CorrelationId>
    <Status>SUCCESS</Status>
  </Header>
  <Body>
    <Accounts>
      <Account>
        <AccountNumber>00123456789</AccountNumber>
        <Currency>PEN</Currency>
        <Balance>1500.00</Balance>
        <Status>ACTIVE</Status>
      </Account>
      <Account>
        <AccountNumber>00123456790</AccountNumber>
        <Currency>USD</Currency>
        <Balance>250.00</Balance>
        <Status>INACTIVE</Status>
      </Account>
    </Accounts>
  </Body>
</Response>
```

`All=true` incluye cuentas activas e inactivas.

### 6.2 Consultar una cuenta — `CONSULT_ACCOUNT`

```xml
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>REQ-000002</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>40581711</DocumentNumber>
    <AccountNumber>00123456789</AccountNumber>
  </Body>
</Request>
```

Según el dispatcher propuesto, la respuesta exitosa utiliza `Body/Accounts/Account` también para una única cuenta. **Verificarlo con una ejecución individual** antes de fijarlo como contrato externo.

### 6.3 Desactivar una cuenta — `DEACTIVATE_ACCOUNT`

```xml
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>DEACTIVATE_ACCOUNT</Operation>
    <CorrelationId>REQ-000003</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>40581711</DocumentNumber>
    <AccountNumber>00123456789</AccountNumber>
  </Body>
</Request>
```

**Respuesta prevista por el dispatcher:** `Status=SUCCESS` y `Body/Account` con `AccountNumber` y `Status=INACTIVE`. Repetir la operación sobre una cuenta ya inactiva debería producir `ACCOUNT_ALREADY_INACTIVE`, sujeto a verificación funcional.

### 6.4 Errores

Estructura de error prevista:

```xml
<Response>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>REQ-000004</CorrelationId>
    <Status>ERROR</Status>
  </Header>
  <Body>
    <Error><Code>ACCOUNT_NOT_FOUND</Code></Error>
  </Body>
</Response>
```

Códigos contemplados en el diseño del dispatcher: `INVALID_XML`, `INVALID_REQUEST`, `UNSUPPORTED_VERSION`, `UNKNOWN_OPERATION`, `ACCOUNT_NOT_FOUND`, `ACCOUNT_ALREADY_INACTIVE`. El worker contempla `INTERNAL_ERROR` para errores inesperados. **No todos los códigos han sido contrastados con una salida real**; el formato de `INTERNAL_ERROR` puede omitir `Operation` y `CorrelationId`.

## 7. Datos iniciales observados

La siguiente consulta de datos fue compartida durante el laboratorio:

| DNI | Persona | Cuenta | Moneda | Saldo | Estado inicial |
|---|---|---|---|---:|---|
| `12345678` | ANA TORRES | — | — | — | Sin cuenta |
| `40581711` | JUAN PEREZ | `00123456789` | PEN | 1500.00 | ACTIVE |
| `40581711` | JUAN PEREZ | `00123456790` | USD | 250.00 | INACTIVE |
| `70451236` | MARIA LOPEZ | `00987654321` | PEN | 800.00 | ACTIVE |
| `75841236` | CARLOS RAMIREZ | `00555111222` | PEN | 0.00 | ACTIVE |

Son **datos de ejemplo**, no cuentas reales. La desactivación altera su estado y puede afectar pruebas posteriores. Restaurar datos solo mediante los scripts o mecanismos previstos en el repositorio; eliminar volúmenes de PostgreSQL destruye los datos persistidos del laboratorio.

## 8. Matriz de validación

| Caso | Entrada | Resultado esperado | Evidencia |
|---|---|---|---|
| Inicio servidor | Puerto 9000 | Listener en loopback | **Validado** |
| Acceso PostgreSQL | Base `xmltcp_accounts` | 4 personas / 4 cuentas | **Validado** |
| Consulta todas | DNI `40581711`, `All=true` | 2 cuentas | **Validado** |
| Consulta individual | DNI + cuenta existente | 1 cuenta | Pendiente de evidencia individual |
| Cuenta inexistente | DNI + cuenta inexistente | `ACCOUNT_NOT_FOUND` | Pendiente de evidencia |
| Desactivar activa | `00123456789` | `SUCCESS`, estado `INACTIVE` | Pendiente de evidencia |
| Reconsultar | Misma cuenta | Estado persistido `INACTIVE` | Pendiente de evidencia |
| Desactivar inactiva | `00123456790` | `ACCOUNT_ALREADY_INACTIVE` | Pendiente de evidencia |
| Operación desconocida | Opción 4 del cliente | `UNKNOWN_OPERATION` | Pendiente de evidencia |
| XML inválido | Opción 5 del cliente | `INVALID_XML` | Pendiente de evidencia |

## 9. Integración con un consumidor Java REST

El proyecto puede ser consumido desde un microservicio Java que exponga REST y actúe como adaptador:

```text
HTTP/JSON → Spring Boot Controller → servicio de aplicación
          → FreeMarker (.ftl) → XML UTF-8
          → TCP client (4-byte framing) → .NET Server
          ← TCP/XML response → parser seguro → DTO → HTTP/JSON
```

Recomendaciones esenciales:

1. Generar XML con FreeMarker usando `output_format="XML"` y `auto_esc=true`.
2. Implementar el framing en el cliente Java con `DataOutputStream.writeInt(payload.length)` y `DataInputStream.readInt()` / `readFully()`.
3. Configurar host, puerto, timeouts y tamaño máximo fuera del código.
4. Validar XML de respuesta de manera segura, sin DTD ni entidades externas.
5. Conservar `CorrelationId` para trazabilidad y mapear explícitamente los códigos XML a respuestas HTTP.
6. No reintentar automáticamente una desactivación si se desconoce si el servidor llegó a procesarla.
7. No confundir `localhost` del proceso Java con el del servidor cuando se ejecuten en contenedores o máquinas distintas.

**Documento complementario:** [`docs/xml-tcp-accounts-integracion-java-rest.md`](docs/xml-tcp-accounts-integracion-java-rest.md), con ejemplos de Java 21, Spring Boot, FreeMarker y cliente TCP.

## 10. Decisiones de diseño y límites

- Se evita REST dentro del servidor .NET porque el propósito del laboratorio es aprender sockets TCP.
- El framing se mantiene independiente del contenido XML.
- El dispatcher no manipula sockets: identifica la operación y delega en los casos de uso.
- Se crea un scope de DI por mensaje para no compartir `DbContext` entre solicitudes concurrentes.
- La identidad por DNI y número de cuenta **no constituye autenticación**.
- Antes de producción: añadir autenticación/autorización, TLS o transporte seguro, control de conexiones simultáneas, timeouts de lectura, cierre ordenado de tareas activas, auditoría, observabilidad, pruebas automatizadas y contrato formal versionado (por ejemplo, XSD).

## 11. Estado del proyecto y próximos pasos

**Operativo para el objetivo educativo de transporte y consulta TCP/XML.** Para cerrar la validación funcional completa: ejecutar y registrar los casos de la matriz, especialmente la desactivación y su persistencia; después incorporar pruebas automatizadas para dominio, casos de uso, XML y framing.

---

*README elaborado a partir de las implementaciones y salidas compartidas durante el laboratorio. El archivo ZIP disponible de una etapa anterior muestra una estructura diferente y una implementación con Npgsql directo; este README describe la evolución posterior confirmada en la conversación (cinco proyectos y EF Core). Verificar rutas y nombres concretos contra el repositorio local vigente antes de integrarlo.*
