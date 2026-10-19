# Estado de la Refactorización de MercuryGO

Este documento registra el progreso, ramas, decisiones y estado de cada fase según la [`GUIA_REFACTOR_GEMINI.md`](../GUIA_REFACTOR_GEMINI.md).

---

## 📌 Resumen General
- **Rama principal de refactorización:** `refactor/final`
- **Fase actual:** Fase 0 — Auditoría (solo lectura)
- **Rama activa:** `refactor/fase0-auditoria`
- **Última actualización:** 2026-10-18 22:05 ART

---

## 🚦 Matriz de Fases

| Fase | Nombre | Rama | Estado | Commits | Aprobación Humana |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **0** | **Auditoría (solo lectura)** | `refactor/fase0-auditoria` | 🟢 Completada | `b507f3d` | Aprobada por usuario |
| **1** | **Modelo de datos y migraciones** | `refactor/fase1-modelo` | 🟢 Completada | `8cc8499` | Aprobada por usuario |
| **2** | **Reglas de estado y endpoints** | `refactor/fase2-estados` | 🟢 Completada | `8243f92` | Aprobada por usuario |
| **3** | **Seguridad de sesión (app)** | `refactor/fase3-seguridad` | 🟢 Completada | `ab45e5a` | Aprobada por usuario |
| **4** | **Vistas y flujos (app)** | `refactor/fase4-vistas` | 🟢 Completada | Pendiente commit | En verificación |
| **5** | **Modo offline con cola e idempotencia** | `refactor/fase5-offline` | ⚪ No iniciada | — | — |
| **6** | **Pruebas y cierre** | `refactor/fase6-cierre` | ⚪ No iniciada | — | — |

---

## 📝 Registro de Decisiones y Hallazgos Clave

### Línea Base Verificada (Paso 0)
- `dotnet build MercuryGo.Api`: 0 errores, 0 advertencias.
- `npm run build` en `MercuryGo.App`: 0 errores (Vite/Rolldown OK).
- `npx cap sync android`: Sincronización limpia (14 plugins activos).
- CodeGraph verificado: MCP tool y CLI funcionales en `c:\Users\SamsungGB3\Documents\GitHub\mercury-go`.
- Commit de arranque: `376cb6d` (*fix(offline): robustecer deteccion de red, recuperacion local y fallback en workflow*).
