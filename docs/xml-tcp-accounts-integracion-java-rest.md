# XmlTcpAccountsLab — Guía de integración desde Java REST mediante FreeMarker y TCP/XML

> **Propósito:** documentar el contrato y el consumo del servidor TCP desarrollado en .NET 10 desde un microservicio Java 21 / Spring Boot que recibe HTTP/JSON, genera XML con FreeMarker, intercambia mensajes mediante sockets TCP y devuelve HTTP/JSON.
>
> **Estado de la evidencia:** la comunicación TCP, el framing y `CONSULT_ACCOUNT` con `All=true` se comprobaron en el laboratorio. El cliente interactivo funciona según la confirmación del laboratorio. La desactivación, la consulta individual y todos los códigos de error deben contrastarse mediante pruebas específicas antes de considerarse verificados de extremo a extremo.

## 1. Arquitectura y responsabilidades

```text
Consumidor HTTP
    |
    | POST /api/cuentas/consulta o /api/cuentas/desactivacion (propuesta Java)
    | JSON
    v
Microservicio Java / Spring Boot
    | Controller REST: validación HTTP
    | Application Service: orquestación
    | FreeMarker: datos -> plantilla XML
    | TcpXmlClient: framing + socket + timeout
    v
TCP 127.0.0.1:9000 (laboratorio local)
    | [longitud de 4 bytes, big-endian][XML UTF-8]
    v
XmlTcpAccounts.Server (.NET 10)
    | TcpServerWorker -> TcpMessageFraming
    | XmlRequestDispatcher -> casos de uso
    | EF Core -> PostgreSQL
    v
Respuesta XML con mismo framing
    |
    v
Java: parseo seguro XML -> DTO -> respuesta HTTP/JSON
```

**Regla fundamental:** el servidor .NET **no es REST**. No se le envía una petición HTTP ni un `POST` directo. Java es responsable de convertir HTTP/JSON a XML/TCP y de convertir XML/TCP a HTTP/JSON.

### Componentes existentes en .NET

| Componente | Responsabilidad |
|---|---|
| `TcpServerWorker` | Escucha TCP, acepta clientes y coordina cada solicitud |
| `TcpMessageFraming` | Lee/escribe cabecera de longitud y cuerpo UTF-8 |
| `XmlRequestDispatcher` | Identifica `Operation`, valida XML y llama a casos de uso |
| `ConsultAccountUseCase` | Consulta cuentas del DNI |
| `DeactivateAccountUseCase` | Desactiva una cuenta |
| EF Core / PostgreSQL | Persistencia de personas y cuentas |

## 2. Configuración y conectividad

| Parámetro | Valor actual en el laboratorio |
|---|---|
| Host | `127.0.0.1` |
| Puerto | `9000` |
| Transporte | TCP sobre IPv4 |
| Codificación | UTF-8 |
| Cabecera | Entero con signo de 32 bits, **big-endian**, longitud en **bytes** del payload |
| Tamaño máximo implementado | `1_048_576` bytes (1 MiB) |
| Protocolo de aplicación | XML, `Version=1.0` |

**Importante para Java en otro proceso/contenedor/servidor:** `127.0.0.1` se refiere a la máquina o contenedor donde corre **Java**, no necesariamente al servidor .NET. El listener actual usa `IPAddress.Loopback`, por lo que solo acepta conexiones locales. Para desplegarlo en máquinas separadas será necesario diseñar explícitamente la exposición de red, controles de acceso y seguridad del transporte. No basta con cambiar el host en Java.

Configuración **propuesta** para Java:

```yaml
accounts:
  tcp:
    host: 127.0.0.1
    port: 9000
    connect-timeout: 3s
    read-timeout: 10s
    max-message-bytes: 1048576
```

Estos timeouts son recomendaciones para el cliente Java; **no son valores confirmados del servidor**.

## 3. Contrato de transporte: framing obligatorio

Cada solicitud y cada respuesta tiene exactamente este formato:

```text
+-------------------------------+----------------------------+
| 4 bytes: N (int32 big-endian) | N bytes: documento XML     |
+-------------------------------+----------------------------+
```

- `N` es la cantidad de **bytes UTF-8**, no la cantidad de caracteres Java (`String.length()`).
- No se envían delimitadores `\n`, terminadores `\0`, HTTP headers ni Base64.
- Una operación `read()` de socket **no garantiza** entregar el mensaje completo; usar `DataInputStream.readFully()` o un bucle equivalente.
- En Java, `DataOutputStream.writeInt(int)` y `DataInputStream.readInt()` usan big-endian, compatible con `BinaryPrimitives.WriteInt32BigEndian` y `ReadInt32BigEndian` de .NET.
- Rechazar longitudes `<= 0` o mayores al límite configurado **antes** de reservar memoria.
- La conexión puede atender mensajes secuenciales, pero el ejemplo Java abre una conexión por solicitud REST para simplificar el ciclo de vida.

Ejemplo: XML de 150 bytes -> cabecera hexadecimal `00 00 00 96`, seguida de los 150 bytes.

## 4. Contrato XML observado

### 4.1 CONSULT_ACCOUNT — Todas las cuentas de un DNI

```xml
<?xml version="1.0" encoding="UTF-8"?>
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

**Respuesta exitosa observada en el laboratorio:**

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

### 4.2 CONSULT_ACCOUNT — Cuenta individual

Solicitud definida por el dispatcher (pendiente de evidencia individual de ejecución):

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

La respuesta exitosa mantiene `Body/Accounts/Account`, incluso cuando solo existe una cuenta. No asumir que el nodo de salida cambia a `Body/Account`.

### 4.3 DEACTIVATE_ACCOUNT — Desactivar una cuenta

Solicitud definida por el dispatcher (requiere verificación de ejecución y persistencia):

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

Respuesta exitosa **esperada según la implementación propuesta**:

```xml
<Response>
  <Header>
    <Version>1.0</Version>
    <Operation>DEACTIVATE_ACCOUNT</Operation>
    <CorrelationId>REQ-000003</CorrelationId>
    <Status>SUCCESS</Status>
  </Header>
  <Body>
    <Account>
      <AccountNumber>00123456789</AccountNumber>
      <Status>INACTIVE</Status>
    </Account>
  </Body>
</Response>
```

**Advertencia:** la desactivación cambia datos reales. No ejecutar contra cuentas productivas durante las pruebas de integración.

### 4.4 Errores

Estructura esperada del dispatcher:

```xml
<Response>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>REQ-000004</CorrelationId>
    <Status>ERROR</Status>
  </Header>
  <Body>
    <Error>
      <Code>ACCOUNT_NOT_FOUND</Code>
    </Error>
  </Body>
</Response>
```

Códigos contemplados en la propuesta de dispatcher: `INVALID_XML`, `INVALID_REQUEST`, `UNSUPPORTED_VERSION`, `UNKNOWN_OPERATION`, `ACCOUNT_NOT_FOUND`, `ACCOUNT_ALREADY_INACTIVE`. El worker también contempla `INTERNAL_ERROR` para errores inesperados. **Confirmar cada código y su estructura contra el código desplegado y las pruebas**; la respuesta genérica del worker puede omitir `Operation` y `CorrelationId`.

## 5. Diseño sugerido del adaptador Java

```text
src/main/java/com/example/accounts/
  api/
    AccountsController.java
    dto/ConsultAccountsRequest.java
    dto/DeactivateAccountRequest.java
  application/
    AccountsGatewayService.java
  infrastructure/
    tcp/TcpXmlClient.java
    xml/XmlTemplateRenderer.java
    xml/XmlResponseParser.java
  config/
    AccountsTcpProperties.java
src/main/resources/
  templates/tcp/
    consult-all.ftl
    consult-one.ftl
    deactivate.ftl
```

La estructura anterior es una **propuesta para el consumidor Java**, no parte del repositorio .NET actual.

## 6. FreeMarker: XML seguro desde datos JSON

### 6.1 Dependencia Maven (Spring Boot)

```xml
<dependency>
    <groupId>org.springframework.boot</groupId>
    <artifactId>spring-boot-starter-freemarker</artifactId>
</dependency>
```

La versión debe administrarse mediante el BOM de la versión de Spring Boot elegida. FreeMarker se utiliza **solo para generar XML**; no realiza la comunicación TCP.

### 6.2 Plantilla `templates/tcp/consult-all.ftl`

```ftl
<#ftl output_format="XML" auto_esc=true>
<?xml version="1.0" encoding="UTF-8"?>
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>${correlationId}</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>${documentNumber}</DocumentNumber>
    <All>true</All>
  </Body>
</Request>
```

### 6.3 Plantilla `templates/tcp/consult-one.ftl`

```ftl
<#ftl output_format="XML" auto_esc=true>
<?xml version="1.0" encoding="UTF-8"?>
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>CONSULT_ACCOUNT</Operation>
    <CorrelationId>${correlationId}</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>${documentNumber}</DocumentNumber>
    <AccountNumber>${accountNumber}</AccountNumber>
  </Body>
</Request>
```

### 6.4 Plantilla `templates/tcp/deactivate.ftl`

```ftl
<#ftl output_format="XML" auto_esc=true>
<?xml version="1.0" encoding="UTF-8"?>
<Request>
  <Header>
    <Version>1.0</Version>
    <Operation>DEACTIVATE_ACCOUNT</Operation>
    <CorrelationId>${correlationId}</CorrelationId>
  </Header>
  <Body>
    <DocumentNumber>${documentNumber}</DocumentNumber>
    <AccountNumber>${accountNumber}</AccountNumber>
  </Body>
</Request>
```

**Seguridad:** `output_format="XML" auto_esc=true` evita que los valores interpolados inyecten nodos XML (escapa `&`, `<`, etc.). Aun así, validar DNI y número de cuenta con reglas del negocio, restringir caracteres de control inválidos para XML y no permitir que el consumidor seleccione libremente el nombre de la plantilla.

### 6.5 Renderizador Java

```java
package com.example.accounts.infrastructure.xml;

import freemarker.template.Configuration;
import freemarker.template.TemplateException;
import org.springframework.stereotype.Component;

import java.io.IOException;
import java.io.StringWriter;
import java.util.Map;

@Component
public class XmlTemplateRenderer {
    private final Configuration configuration;

    public XmlTemplateRenderer(Configuration configuration) {
        this.configuration = configuration;
    }

    public String render(String templateName, Map<String, Object> model) {
        try (StringWriter writer = new StringWriter()) {
            configuration.getTemplate("tcp/" + templateName)
                    .process(model, writer);
            return writer.toString();
        } catch (IOException | TemplateException exception) {
            throw new IllegalStateException("Unable to render XML request", exception);
        }
    }
}
```

Ejemplo de uso:

```java
String correlationId = java.util.UUID.randomUUID().toString();
String xml = renderer.render("consult-one.ftl", Map.of(
        "correlationId", correlationId,
        "documentNumber", "40581711",
        "accountNumber", "00123456789"
));
```

## 7. Cliente TCP Java: implementación completa del framing

Este componente es **código de referencia para el futuro consumidor Java**, no código ya probado contra el laboratorio.

```java
package com.example.accounts.infrastructure.tcp;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;

public final class TcpXmlClient {
    private final String host;
    private final int port;
    private final int connectTimeoutMillis;
    private final int readTimeoutMillis;
    private final int maxMessageBytes;

    public TcpXmlClient(String host, int port,
                        int connectTimeoutMillis, int readTimeoutMillis,
                        int maxMessageBytes) {
        this.host = host;
        this.port = port;
        this.connectTimeoutMillis = connectTimeoutMillis;
        this.readTimeoutMillis = readTimeoutMillis;
        this.maxMessageBytes = maxMessageBytes;
    }

    public String exchange(String requestXml) throws IOException {
        byte[] requestBytes = requestXml.getBytes(StandardCharsets.UTF_8);
        if (requestBytes.length == 0 || requestBytes.length > maxMessageBytes) {
            throw new IOException("Invalid request payload length");
        }

        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(host, port), connectTimeoutMillis);
            socket.setSoTimeout(readTimeoutMillis);

            DataOutputStream output = new DataOutputStream(socket.getOutputStream());
            DataInputStream input = new DataInputStream(socket.getInputStream());

            // writeInt/readInt son big-endian en Java.
            output.writeInt(requestBytes.length);
            output.write(requestBytes);
            output.flush();

            int responseLength = input.readInt();
            if (responseLength <= 0 || responseLength > maxMessageBytes) {
                throw new IOException("Invalid response payload length");
            }

            byte[] responseBytes = new byte[responseLength];
            input.readFully(responseBytes);

            try {
                return StandardCharsets.UTF_8.newDecoder()
                        .onMalformedInput(CodingErrorAction.REPORT)
                        .onUnmappableCharacter(CodingErrorAction.REPORT)
                        .decode(ByteBuffer.wrap(responseBytes))
                        .toString();
            } catch (CharacterCodingException exception) {
                throw new IOException("Invalid UTF-8 in TCP response", exception);
            }
        }
    }
}
```

**Notas:** `Socket.setSoTimeout` controla lecturas bloqueantes, no un plazo total de operación; en este ejemplo, `connectTimeoutMillis` y `readTimeoutMillis` deben ser positivos y configurarse externamente. El cliente usa una conexión por solicitud REST. Para producción, definir también límites de concurrencia, presupuesto de tiempo de extremo a extremo y política de cierre.

## 8. Parseo XML seguro en Java

**No devolver el XML bruto como respuesta JSON sin interpretarlo.** El adaptador debe validar `Header/Status`, `CorrelationId`, `Operation` y el contenido del `Body`, y transformarlo en DTOs Java.

Ejemplo mínimo de creación segura del parser DOM:

```java
import javax.xml.XMLConstants;
import javax.xml.parsers.DocumentBuilderFactory;

DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
factory.setFeature(XMLConstants.FEATURE_SECURE_PROCESSING, true);
factory.setXIncludeAware(false);
factory.setExpandEntityReferences(false);
factory.setAttribute(XMLConstants.ACCESS_EXTERNAL_DTD, "");
factory.setAttribute(XMLConstants.ACCESS_EXTERNAL_SCHEMA, "");
```

Después, parsear el `byte[]` XML, comprobar `Response`, `Header`, `Body` y los campos esperados. Evitar búsquedas globales de nodos ambiguas cuando hay elementos `Status` tanto en cabecera como en cuentas. Validar que el `CorrelationId` devuelto coincida con el enviado **cuando esté presente**; si el worker devuelve un error genérico sin ese campo, clasificarlo como respuesta de protocolo incompleta y registrar el incidente.

Para montos usar `BigDecimal`, no `double`; preservar los números de cuenta como `String` para no perder ceros iniciales.

## 9. Exposición REST de entrada (propuesta)

Estas rutas y DTOs son **diseño sugerido**, no endpoints ya existentes:

### Consultar cuentas de un DNI

```http
POST /api/cuentas/consulta
Content-Type: application/json

{"documentNumber":"40581711","all":true}
```

### Consultar una cuenta

```http
POST /api/cuentas/consulta
Content-Type: application/json

{"documentNumber":"40581711","accountNumber":"00123456789","all":false}
```

### Desactivar cuenta

```http
POST /api/cuentas/desactivacion
Content-Type: application/json

{"documentNumber":"40581711","accountNumber":"00123456789"}
```

**Secuencia recomendada del servicio Java:**

1. Validar la solicitud HTTP y la combinación `all` / `accountNumber`.
2. Generar `CorrelationId` único por solicitud.
3. Seleccionar la plantilla FreeMarker **desde un conjunto cerrado**.
4. Renderizar el XML con autoescape.
5. Invocar `TcpXmlClient.exchange(xml)`.
6. Parsear XML de respuesta con entidades externas deshabilitadas.
7. Validar versión, operación, correlación (cuando el contrato la garantice) y `Status`.
8. Transformar `Accounts` o `Error/Code` a DTOs y códigos HTTP.
9. Registrar métricas de latencia, éxito y fallo sin exponer DNI completo ni datos sensibles.

### Mapeo HTTP recomendado (decisión del adaptador Java)

| Resultado TCP/XML | HTTP sugerido | Observación |
|---|---:|---|
| `SUCCESS` de consulta | 200 | Lista de cuentas |
| `SUCCESS` de desactivación | 200 | Estado actualizado |
| `INVALID_XML`, `INVALID_REQUEST` | 400 | Distinguir errores del consumidor HTTP de errores generados por el adaptador |
| `ACCOUNT_NOT_FOUND` | 404 | Política de negocio a confirmar |
| `ACCOUNT_ALREADY_INACTIVE` | 409 | Conflicto de estado; política a confirmar |
| `UNKNOWN_OPERATION`, `UNSUPPORTED_VERSION` | 502 o error interno de integración | En un adaptador bien construido suelen indicar desalineación de contratos |
| `INTERNAL_ERROR` | 502 | Error del servicio TCP remoto |
| Timeout de lectura/conexión | 504 | Dependencia no respondió a tiempo |
| Conexión rechazada o protocolo inválido | 502/503 | Definir política consistente |

**No interpretar códigos HTTP como si los emitiera el servidor TCP**: son decisiones del controlador Java.

## 10. Consideraciones de resiliencia y seguridad

- **No aplicar reintentos automáticos a `DEACTIVATE_ACCOUNT`**: una respuesta perdida después de la actualización podría causar un segundo intento y recibir `ACCOUNT_ALREADY_INACTIVE`. La operación no ofrece aquí un mecanismo de idempotencia confirmado.
- Para consultas, un retry acotado puede ser viable, siempre que el presupuesto de latencia y el fallo sean adecuados.
- El `CorrelationId` permite trazabilidad, pero **no equivale a una clave de idempotencia**.
- El TCP actual no incorpora autenticación ni TLS en el contrato documentado; tratarlo como un servicio de laboratorio local, no como un servicio seguro para exposición pública.
- Validar límites de tamaño en ambos extremos y establecer timeouts; proteger el servicio de clientes que conectan y no terminan de enviar el payload.
- No registrar documentos completos con DNI y números de cuenta en logs de producción.
- Distinguir `SUCCESS` de `ERROR` en el XML; una conexión TCP exitosa **no significa** que la operación de negocio haya tenido éxito.
- No asumir que un `Socket.write()` representa una transacción completa ni que la ausencia de respuesta implica ausencia de efectos en base de datos.
- Evitar compartir un mismo socket entre solicitudes concurrentes sin una estrategia explícita de multiplexación; la implementación simple usa una conexión por operación.

## 11. Matriz de pruebas de integración

| Escenario | Datos de prueba | Resultado esperado | Evidencia |
|---|---|---|---|
| Consultar todas | DNI `40581711`, `All=true` | 2 cuentas (`ACTIVE` e `INACTIVE`) | **Confirmado** mediante respuesta XML |
| Consultar una | DNI `40581711`, cuenta `00123456789` | Una cuenta | Pendiente de evidencia específica |
| Cuenta inexistente | DNI `40581711`, cuenta `99999999999` | `ACCOUNT_NOT_FOUND` | Pendiente |
| Desactivar activa | DNI `40581711`, cuenta `00123456789` | `SUCCESS`, cambia a `INACTIVE` | Pendiente de evidencia de persistencia |
| Desactivar inactiva | DNI `40581711`, cuenta `00123456790` | `ACCOUNT_ALREADY_INACTIVE` | Pendiente |
| Operación desconocida | `UNKNOWN_OPERATION` | `UNKNOWN_OPERATION` | Pendiente |
| XML mal formado | XML con etiquetas no balanceadas | `INVALID_XML` | Pendiente |
| Cabecera incompleta | Menos de 4 bytes | Cierre/error de transporte | Pendiente |
| Longitud inválida | `N=0` o `N>1MiB` | Rechazo del framing | Pendiente |
| Servidor no disponible | Puerto cerrado | Error de conexión Java | Pendiente |
| Timeout | Servidor no completa respuesta | Timeout Java | Pendiente |

**Nota sobre los seeds:** los valores anteriores corresponden a los datos observados en el laboratorio; después de desactivar una cuenta, el estado inicial cambiará. No repetir pruebas que esperan `ACTIVE` sin restaurar explícitamente el fixture.

## 12. Checklist de consumo desde Java

- [ ] Servidor .NET accesible desde el entorno donde corre Java.
- [ ] XML generado con FreeMarker y `output_format="XML" auto_esc=true`.
- [ ] `Version=1.0`, `Operation`, `CorrelationId` y `Body` correctos.
- [ ] Longitud calculada desde `xml.getBytes(StandardCharsets.UTF_8)`.
- [ ] Cabecera de 4 bytes big-endian escrita antes del XML.
- [ ] Respuesta leída con `readInt()` + `readFully()`.
- [ ] Longitud validada antes de asignar el buffer.
- [ ] Parser XML con DTD y entidades externas deshabilitadas.
- [ ] `Header/Status` y `Body/Error/Code` interpretados correctamente.
- [ ] Números de cuenta como `String` y saldos como `BigDecimal`.
- [ ] Timeouts, observabilidad y tratamiento de errores definidos.
- [ ] Reintentos de mutaciones deshabilitados salvo diseño explícito de idempotencia.
- [ ] Pruebas de contrato y escenarios negativos ejecutadas.

## 13. Referencias del laboratorio

- Proyecto servidor: `XmlTcpAccounts.Server` (.NET 10).
- Proyecto cliente de validación: `XmlTcpAccounts.Client` (.NET 10).
- Base de datos: PostgreSQL `xmltcp_accounts`.
- Framing: `TcpMessageFraming` en ambos extremos .NET; en Java se replica con `DataInputStream` / `DataOutputStream`.
- Operaciones: `CONSULT_ACCOUNT` y `DEACTIVATE_ACCOUNT`.
- Protocolo: TCP/XML; **no REST** entre Java y .NET.

---

**Alcance de este documento:** especificación de consumo y propuesta de adaptador Java. Los ejemplos Java requieren implementación, compilación y pruebas de integración en el proyecto consumidor antes de considerarse listos para producción.
