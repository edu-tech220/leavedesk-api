# LeaveDesk API

API REST para gestionar permisos y vacaciones de empleados: solicitud, aprobacion
multinivel (gerente -> RH), control de saldo por tipo de permiso y limite de
ausencias simultaneas por departamento, todo bajo concurrencia segura.

## Que problema resuelve

Cualquier empresa necesita administrar permisos (vacaciones, incapacidad, dias
personales, sin goce de sueldo) con reglas que un CRUD simple no cubre:

- **Flujo de aprobacion multinivel**: el gerente directo resuelve primero; si el
  permiso supera 5 dias habiles o el tipo lo exige siempre (p. ej. sin goce de
  sueldo), pasa a una segunda instancia de Recursos Humanos.
- **Saldo por tipo y anio**: cada empleado tiene dias acumulados por tipo de
  permiso; las solicitudes pendientes "reservan" dias del saldo para que dos
  solicitudes simultaneas no autoricen mas dias de los disponibles.
- **Sin solapamientos**: un empleado no puede tener dos solicitudes (pendientes
  o aprobadas) con fechas que se crucen.
- **Cupo por departamento bajo concurrencia**: cada departamento define cuantos
  empleados pueden estar fuera al mismo tiempo. La validacion de cupo se hace
  dentro de una transaccion para que dos aprobaciones concurrentes no exceda el
  limite (vease la seccion de concurrencia).
- **Cancelacion controlada**: una solicitud pendiente se cancela libremente; una
  ya aprobada solo se puede cancelar antes de que inicie, y el saldo se
  restaura automaticamente.

## Arquitectura

Solucion de una sola API organizada por capas dentro del mismo proyecto
(`src/LeaveDesk.Api`):

```
Domain/          Entidades, enums y excepciones de negocio. LeaveRequest
                 encapsula su propia maquina de estados (PendingManager ->
                 PendingHr -> Approved / Rejected / Cancelled).
Application/     Casos de uso (servicios), DTOs y utilidades (calculo de dias
                 habiles, paginacion). No depende de ASP.NET Core.
Infrastructure/  EF Core (DbContext, migraciones), seguridad (PBKDF2, JWT) y
                 el seed de datos de demostracion.
Controllers/     Adaptadores HTTP: validan el modelo, delegan en los servicios
                 y dejan que el middleware traduzca excepciones de dominio a
                 ProblemDetails (404/403/409/422/401 segun el caso).
```

`tests/LeaveDesk.Api.Tests` combina:

- Pruebas **unitarias** de la maquina de estados de `LeaveRequest` (sin base de
  datos ni HTTP).
- Pruebas de **integracion** con `WebApplicationFactory<Program>` sobre una
  base SQLite en memoria, ejercitando la API completa (auth, controllers,
  autorizacion, EF Core) igual que en produccion pero sin SQL Server.

## Decisiones tecnicas

- **Token de concurrencia manejado por la aplicacion, no `rowversion` de SQL
  Server.** `LeaveRequest` y `LeaveBalance` implementan `IHasConcurrencyToken`;
  `LeaveDeskDbContext` regenera un GUID en cada `SaveChanges` para las filas
  agregadas o modificadas. Esto da el mismo comportamiento optimista
  (If-Match por `RowVersion` en cada decision) en SQL Server **y** en SQLite,
  que no soporta columnas auto-generadas de tipo rowversion. Las pruebas de
  concurrencia (ej. `Decision_ConRowVersionDesactualizado_Regresa409`) corren
  igual en ambos motores.
- **Cupo de departamento con transaccion serializable.** Antes de aprobar un
  permiso se cuenta, dentro de una transaccion, cuantos permisos aprobados del
  mismo departamento se solapan con el rango de fechas. En SQL Server ese
  conteo usa `WITH (UPDLOCK, HOLDLOCK)` (mismo patron usado en JobFlow para
  reclamos concurrentes) para que dos aprobaciones en curso no lean el mismo
  cupo disponible; en otros proveedores se apoya en la transaccion
  `Serializable`. Si se excede el cupo, la transaccion se revierte completa (ni
  el estado de la solicitud ni el saldo cambian).
- **"Reserva" de saldo en estado pendiente.** El saldo disponible de un tipo de
  permiso se calcula como `acumulado - usado - reservado_en_pendientes`. Asi,
  dos solicitudes pendientes del mismo empleado no pueden sumar mas dias de los
  que existen, aunque ninguna se haya aprobado todavia.
- **Trigger de SQL Server como defensa adicional.** Ademas de la logica de
  aplicacion, la migracion inicial crea un trigger (`TR_LeaveRequests_StatusAudit`)
  que registra en `LeaveRequestAudits` cualquier cambio de estado de una
  solicitud, incluso si alguien la modifica fuera de la API. No se ejecuta
  contra SQLite (las pruebas usan `EnsureCreated`, no migraciones), por lo que
  no afecta la corrida de `dotnet test`.
- **Restriccion `CHECK` entre columnas.** `UsedDays <= AccruedDays` en
  `LeaveBalances` y `StartDate <= EndDate` en `LeaveRequests` se declaran con
  `HasCheckConstraint`, portables entre SQL Server y SQLite.
- **PBKDF2 propio en vez de ASP.NET Core Identity.** El alcance no necesita
  todo el stack de Identity; `Rfc2898DeriveBytes.Pbkdf2` con sal aleatoria y
  100,000 iteraciones es suficiente y mantiene el modelo de datos simple.
- **Rate limiting por IP solo en login**, con una ventana mas permisiva cuando
  `ASPNETCORE_ENVIRONMENT=Testing` (una sola IP de loopback hace decenas de
  logins por minuto en la suite de pruebas).
- **Dias habiles sin calendario de feriados.** Se cuenta lunes a viernes; un
  calendario de feriados por pais queda fuera de alcance para este proyecto.

## Modelo de dominio (resumen)

| Entidad | Rol |
|---|---|
| `Employee` | Usuario del sistema; tiene rol (`Employee`, `Manager`, `Hr`, `Admin`), departamento y gerente directo opcional. |
| `Department` | Agrupa empleados y define `MaxConcurrentLeaves`. |
| `LeaveType` | Catalogo de tipos de permiso (vacaciones, incapacidad, personal, sin goce de sueldo) con dias anuales y si siempre requiere RH. |
| `LeaveBalance` | Dias acumulados/usados de un empleado por tipo y anio. |
| `LeaveRequest` | La solicitud en si, con su maquina de estados y las decisiones de gerente/RH. |
| `LeaveRequestAudit` | Bitacora de cambios de estado (poblada por el trigger en SQL Server). |

## Como correrlo

### Con Docker Compose (API + SQL Server)

```bash
docker compose up --build
```

La API queda en `http://localhost:8080` (Swagger en `/swagger` solo si
`ASPNETCORE_ENVIRONMENT=Development`). Al iniciar aplica las migraciones y
siembra datos de demostracion si la base esta vacia.

### Localmente

```bash
dotnet run --project src/LeaveDesk.Api
```

Requiere una cadena de conexion valida a SQL Server en `ConnectionStrings:DefaultConnection`
(ver `appsettings.json`; los valores por defecto apuntan a un SQL Server local).

### Usuarios de demostracion

Todas las cuentas sembradas usan la contrasena `Passw0rd!`:

| Correo | Rol | Equipo |
|---|---|---|
| admin@leavedesk.test | Admin | Ingenieria |
| hr@leavedesk.test | Hr | Soporte |
| manager@leavedesk.test | Manager | Ingenieria (gerente de eduardo/laura/carlos) |
| manager2@leavedesk.test | Manager | Soporte (sin reportes) |
| eduardo@leavedesk.test | Employee | Ingenieria |
| laura@leavedesk.test | Employee | Ingenieria |
| carlos@leavedesk.test | Employee | Ingenieria |

## Como probarlo

Las pruebas no requieren SQL Server ni Docker: usan SQLite en memoria a traves
de `WebApplicationFactory`.

```bash
dotnet test
```

### Flujo manual rapido (Swagger o curl)

1. `POST /api/auth/login` con `eduardo@leavedesk.test` / `Passw0rd!`.
2. `GET /api/leave-types` para obtener el `id` de "VACATION".
3. `POST /api/leave-requests` con ese `leaveTypeId` y un rango de fechas futuro.
4. Login como `manager@leavedesk.test` y `PUT /api/leave-requests/{id}/manager-decision/approve`
   enviando el `rowVersion` recibido en la solicitud.
5. Si el permiso supera 5 dias habiles, repetir el paso anterior con
   `hr@leavedesk.test` en `/hr-decision/approve`.
6. `GET /api/leave-balances/me` para confirmar que el saldo se desconto.
