# Procedimiento de gestión de accesos

_Sistema de Inventario de Material Promocional — Marketing, Nestlé Bolivia_

- **Versión:** 1.0 (borrador para revisión de TI)
- **Fecha:** 06/10/2026
- **Dueño del procedimiento:** Responsable de la aplicación (Marketing)
- **Revisión de este documento:** Una vez al año, o ante un cambio del sistema de accesos

## 1. Objetivo y alcance

Definir cómo se dan de alta, modifican, revisan y dan de baja las cuentas de la aplicación de Inventario de Material Promocional, para que cada persona tenga solo el acceso que su trabajo necesita y que ese acceso se pueda demostrar y auditar.

Aplica a todas las cuentas de la aplicación en cualquier país (hoy Bolivia y Perú). Cada país tiene sus propias cuentas y roles; una cuenta no puede operar en otro país.

## 2. Roles y responsabilidades

| Rol | Responsabilidad |
|---|---|
| Dueño de la aplicación | Aprueba qué rol recibe cada persona, designa a los revisores de accesos y archiva las actas. |
| Administrador de usuarios | Crea, modifica y desactiva cuentas en la aplicación (permiso «usuarios.gestionar»). Solo ejecuta lo aprobado. |
| Revisor de accesos | Abre y resuelve las revisiones periódicas (permiso «accesos.revisar»). No puede revisar su propia cuenta: la revisa otra persona. |
| Jefe / gerente del solicitante | Pide el alta, el cambio de rol o la baja de su personal y confirma qué necesita hacer en el sistema. |
| RR. HH. | Avisa las salidas y los cambios de área para dar de baja o ajustar la cuenta. |
| TI / Seguridad | Recibe las actas de revisión como evidencia, define los requisitos de contraseña, MFA y SSO corporativos. |

## 3. Alta de una cuenta

- El jefe del solicitante pide el alta por correo al dueño de la aplicación, indicando nombre, correo corporativo, país y qué necesita hacer (ver la lista de roles más abajo).
- El dueño aprueba el rol. Se asigna siempre el rol con los permisos mínimos necesarios para esa función.
- El administrador de usuarios crea la cuenta en «Usuarios» con el rol aprobado. El sistema valida el correo y registra quién creó la cuenta y cuándo. Hoy el acceso es con contraseña local (8 a 72 caracteres, letras y números). Cuando se habilite el inicio de sesión con la cuenta corporativa Microsoft, la autenticación (contraseña, MFA, bloqueo) pasa a ser responsabilidad de TI y la aplicación solo conserva el rol y el país de la cuenta.
- Mientras exista contraseña local, la contraseña inicial se entrega al usuario por un canal distinto al del pedido. Ninguna cuenta se comparte entre personas: cada cuenta es personal.
El correo del pedido y la aprobación del dueño se guardan como evidencia del alta.

| Rol | Qué permite |
|---|---|
| Solicitante | Crear y ver sus propias solicitudes de material. |
| Consulta | Ver productos, existencias, movimientos y solicitudes. No modifica nada. |
| Operador | Registrar entradas, salidas, ajustes y conteos físicos. No aprueba solicitudes ni administra catálogos o usuarios. |
| Administrador | Todo lo anterior, más aprobar solicitudes, administrar catálogos, usuarios y roles, ver la auditoría y revisar accesos. |

## 4. Cambio de rol

- El jefe pide el cambio indicando el motivo (por ejemplo, cambio de funciones). El dueño lo aprueba.
- El administrador de usuarios edita la cuenta en «Usuarios». El cambio vale de inmediato: el sistema valida cada petición contra el estado actual de la cuenta, no contra lo que tenía al iniciar sesión.
- El cambio queda en la auditoría con el rol anterior y el nuevo, quién lo hizo y cuándo.

## 5. Baja de una cuenta

- RR. HH. o el jefe avisan la salida o el cambio de área de la persona. El plazo para desactivar la cuenta es de un día hábil desde el aviso.
- El administrador de usuarios desactiva la cuenta en «Usuarios» (desmarcar «Activo»). La cuenta no se borra, para conservar el historial de lo que hizo.
- La baja es inmediata: una sesión abierta de esa persona deja de funcionar en la siguiente petición. Con inicio de sesión Microsoft, la cuenta también se deshabilita en Microsoft, pero la cuenta de la aplicación se desactiva igual: deshabilitarla solo en Microsoft deja un rol activo sin dueño.
- Nadie puede desactivar su propia cuenta.

## 6. Revisión periódica de accesos

Es el control que confirma, con una persona responsable y por escrito, que cada cuenta sigue siendo necesaria y con el rol correcto.

| Alcance | Frecuencia | Qué se revisa |
|---|---|---|
| Todas las cuentas activas | Trimestral (cada 90 días) | Que la persona siga necesitando el acceso y que su rol sea el mínimo necesario. |
| Cuentas administradoras | Mensual (cada 30 días) | Las cuentas con permisos de administrar usuarios, roles o revisiones. Son las de mayor riesgo. |

Pasos:

- El revisor abre la revisión en «Revisión de accesos». El sistema toma una foto de las cuentas activas con nombre, correo, rol, fecha de alta y último ingreso, y marca las que merecen atención: las que administran, las que nunca ingresaron y las que no ingresan hace 90 días o más.
- Para cada cuenta el revisor decide: mantener, quitar el acceso o cambiar el rol. Quitar o cambiar de rol exige escribir el motivo. Nada se aplica todavía, y puede deshacer cualquier decisión.
- El revisor no puede decidir sobre su propia cuenta. La revisa otra persona con el mismo permiso (por ejemplo, TI o el jefe del área).
- Cuando todas las cuentas tienen decisión, el revisor cierra la revisión. En ese momento el sistema aplica las decisiones (desactiva cuentas, cambia roles), de forma que o se aplican todas o no se aplica ninguna, y no permite dejar el país sin una cuenta administradora activa.
- La revisión cerrada ya no se puede modificar. El revisor descarga el acta en Excel (cuentas revisadas, decisión de cada una, quién y cuándo) y la archiva con las firmas del revisor y de TI o Seguridad.
Plazo: la revisión se cierra dentro de 10 días hábiles desde que se abre. Además, si pasan más de 90 días desde la última revisión de todas las cuentas, o más de 30 desde la de administradores, la pantalla de Inicio muestra un aviso al revisor hasta que se complete una nueva.

## 7. Cuentas privilegiadas

- Una cuenta es privilegiada si su rol permite administrar usuarios, administrar roles o revisar accesos.
- Cada cuenta privilegiada es nominal y personal. No se comparten ni se usan para el trabajo diario cuando la persona no lo necesita.
- Se mantienen al menos dos personas con capacidad de administrar usuarios por país, para que nadie quede fuera de la aplicación si una cuenta falla.
- Se revisan todos los meses (sección 6) y toda acción que hacen queda en la auditoría.

## 8. Controles técnicos que respaldan este procedimiento

- Hoy: autenticación con usuario y contraseña local (hash BCrypt, nunca en claro) y bloqueo temporal tras 5 intentos fallidos en 10 minutos. Con inicio de sesión Microsoft esto lo gestiona la cuenta corporativa, con la política y el MFA de TI.
- La sesión dura 8 horas. En cada petición el sistema comprueba que la cuenta siga activa y que sus permisos coincidan con los actuales.
- Cada acción del sistema exige un permiso concreto; los permisos se agrupan en roles.
- Aislamiento por país: cada país ve y administra solo sus datos, usuarios y roles.
- Separación de funciones: quien pide un material no lo aprueba ni registra su entrega, y nadie revisa su propio acceso.
- Auditoría de todo cambio de datos, incluidas las cuentas y los roles, con la persona, la fecha y el valor anterior y el nuevo. Nunca se registran contraseñas.

## 9. Evidencia que genera el procedimiento

| Evidencia | Dónde queda |
|---|---|
| Alta, cambio de rol y baja | Correo de pedido y aprobación (archivo del dueño) y registro de auditoría de la aplicación. |
| Revisión periódica | Revisión cerrada en la aplicación (inmutable) y acta en Excel firmada, archivada por el dueño y entregada a TI. |
| Quién tuvo acceso y cuándo | Auditoría de la aplicación, con filtros por entidad, acción, usuario y fecha. |

## 10. Brechas conocidas y plan

Estas limitaciones existen hoy y se declaran para que TI las pueda evaluar; no se presentan como cubiertas.

| Brecha | Plan | Depende de |
|---|---|---|
| Contraseña y MFA gestionados por la propia aplicación (contraseña local, sin MFA, sin cambio de contraseña por el usuario) | Inicio de sesión con la cuenta corporativa (Microsoft Entra ID): contraseña, cambio de contraseña, MFA y bloqueo pasan a TI. No se desarrolla gestión de contraseñas propia. | Registro de la aplicación por TI |
| La revisión de accesos depende de que una persona la abra | El sistema avisa en Inicio cuando vence; TI puede pedir el acta como control. | Disciplina del revisor |
