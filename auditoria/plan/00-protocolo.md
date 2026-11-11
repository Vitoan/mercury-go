# PROTOCOLO DE TRABAJO (leelo completo al empezar CADA turno)

Proyecto: MercuryGO (MercuryGo.App: Vue 3 + Ionic 8 + Capacitor 8 + Pinia;
MercuryGo.Api: ASP.NET Core 10 + EF Core + MySQL). Objetivo: corregir lo
decidido de /AUDITORIA_FINAL_DEFENSA.md antes de defender el proyecto. El
alcance es M01 a M09. Nada de M10 en adelante, ni mejoras que no estén en el plan.

## Roles
- AVANZADO: planifica, hace las tareas marcadas "AVANZADO", revisa los commits
  de FLASH y decide si se aprueban.
- FLASH: ejecuta SOLO tareas marcadas "FLASH", en orden, sin improvisar.

## Archivos
- /auditoria/plan/PLAN.md: tareas aprobadas (las escribe AVANZADO).
- /auditoria/plan/ESTADO.md: tablero. Columnas: ID | Modelo | Estado | Commit |
  Revisión. Estados: PENDIENTE, EN CURSO, HECHA, BLOQUEADA, APROBADA, RECHAZADA.
- /auditoria/plan/REVISION.md: notas de revisión del AVANZADO.
Actualizá ESTADO.md en cada turno. Es la única fuente de verdad: no asumas
nada que no esté ahí.

## Seguridad del código
- Trabajá solo en la rama `defensa/auditoria-fixes`. Nunca en main.
- Un commit por tarea, con mensaje "[ID] descripción corta".
- Antes de cada tarea, los tests tienen que estar en verde; después de cada
  tarea, vuelven a correr. Si una tarea los rompe, revertila con `git revert`.
- No modifiques nada fuera de los archivos listados en la tarea.

## Formato de tarea FLASH (lo escribe AVANZADO)
ID | Objetivo | Archivos exactos (máx. 3) | Cambio exacto (antes/después o
pasos literales) | Comando de verificación y resultado esperado | Criterio de
aceptación | Qué NO tocar | Rollback.
Una tarea FLASH debe ser mecánica y de menos de 30 minutos. Todo lo que toque
autenticación, autorización, idempotencia, sincronización offline, push,
migraciones o base de datos es del AVANZADO.

## Reglas de FLASH
1. Hacé solo la próxima tarea PENDIENTE marcada FLASH, en el orden del PLAN.
2. Si el código real no coincide con lo que dice la tarea, o la verificación
   falla dos veces, PARÁ: marcala BLOQUEADA, anotá qué viste y qué probaste, y
   no sigas. No improvises una solución distinta.
3. Hacé como máximo 3 tareas seguidas y detenete para revisión.

## CARTEL DE CAMBIO (obligatorio al final de cada turno)
Terminá SIEMPRE tu respuesta con este bloque, rellenado:

🔁 CAMBIO DE MODELO
Cambiá a: AVANZADO | FLASH | NINGUNO (listo)
Motivo: <una línea>
Pegá este prompt: <el prompt corto exacto para el siguiente turno>
Estado: <cantidad de tareas por estado>

Cuándo cambiar:
- FLASH → AVANZADO: al completar 3 tareas, si una queda BLOQUEADA, o si no hay
  más tareas FLASH pendientes.
- AVANZADO → FLASH: cuando hay tareas FLASH PENDIENTES y no quedan revisiones ni
  tareas AVANZADO por hacer.
- AVANZADO → NINGUNO: cuando todo está APROBADO y se pasó el cierre final.