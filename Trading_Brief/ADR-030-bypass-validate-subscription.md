# ADR-030 — Bypass de ValidateSubscription para operación en live local sin suscripción QuantConnect

**Fecha:** 2026-05-29
**Estado:** Aceptado
**Hito:** C (paper trading)

## Contexto

Lean local en `LiveMode == true` ejecuta `ValidateSubscription()` durante el
arranque del brokerage de Binance. Es una rutina que llama a los servidores de
QuantConnect para validar identidad y suscripción. Al intentar lanzar paper
trading el sistema falló con
`Invalid api user id or token, cannot authenticate subscription`, y la
verificación en el portal de QC confirmó: "To request an access token, you
must belong to a paid organization."

**Ubicación de la validación.** La rutina `ValidateSubscription()` NO está en
el motor de Lean. Está dentro del código del **plugin** de Binance
(`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`),
en la copia vendored del repo `Lean.Brokerages.Binance`. Es el plugin el que
se autentica contra QC al iniciar, no el motor. El motor de Lean en sí
permanece sin ningún gate de licencia y compila/ejecuta sin contactar a QC.

Esto es relevante para entender el alcance real del fork:

- **El motor de Lean (`Engine`, `Common`, `Algorithm`, etc.) NO ha sido
  modificado** y puede actualizarse libremente a versiones futuras sin
  trabajo adicional.
- **Solo el plugin de Binance es un fork con parche.** El parche se va a
  tener que re-aplicar solamente cuando se actualice el plugin de Binance,
  no cuando se actualicen otros componentes del sistema.

QuantConnect publicita LEAN como open source (Apache 2.0). El motor lo es
genuinamente. Los plugins oficiales que QC mantiene incluyen validaciones de
suscripción hardcodeadas: patrón "open core" focalizado en los conectores a
brokers comerciales, no en el motor. Legal con Apache 2.0, ambiguo éticamente.

Tres caminos evaluados:

1. **Pagar QC (≈ USD 20-60/mes según fuentes externas).** Comodidad máxima,
   mantenimiento delegado de los plugins oficiales, costo recurrente
   indefinido.
2. **Binance Testnet completo.** Sin costo, pero feed sintético del testnet
   (no replica fielmente liquidez ni microestructura del mercado real).
3. **Parchar `ValidateSubscription` localmente en el plugin de Binance.** Sin
   costo, feed real de producción, fills ficticios vía PaperBrokerage. Convierte
   solamente al plugin vendored de Binance en un fork con parche puntual que
   el operador asume mantener.

## Decisión

Camino 3: parchar `ValidateSubscription` en el plugin de Binance para retorno
inmediato sin contactar a QC.

Razón: el sistema está en validación, sin ingresos. Un costo recurrente de
USD 240-720/año no es defendible mientras la rentabilidad no esté demostrada.
La opción Testnet, aunque gratis, degrada el valor de la validación de
infraestructura del Hito C al introducir un feed sintético; el camino 3
preserva la calidad del feed para los objetivos del Hito C.

## Trade-offs aceptados explícitamente

- **Mantenimiento delegado a uno mismo, acotado al plugin de Binance.** Cada
  actualización del plugin de Binance va a requerir re-aplicar el parche. El
  motor de Lean y cualquier otro componente del sistema se mantienen al día
  sin trabajo adicional.
- **Riesgo de drift, también acotado al plugin.** QC puede endurecer la
  validación en versiones futuras del plugin de Binance o moverla a más sitios
  dentro de ese mismo plugin. El parche actual podría dejar de ser suficiente
  cuando llegue ese momento.
- **Aislamiento del ecosistema comercial QC.** Sin soporte oficial, sin foro
  para mostrar el código modificado.
- **Si en el futuro se agregan otros plugins oficiales de QC** (Coinbase, IB,
  etc.), cada uno traerá su propia `ValidateSubscription` y será una decisión
  separada si parchar también esos.
- **Zona gris ética (no legal).** Apache 2.0 lo permite, pero elude el modelo
  de negocio del vendor.

## Trigger de revisión

Esta decisión se revisa cuando se cumpla CUALQUIERA de:

- **Primer trade rentable real** (no paper, no testnet) operado por el sistema.
- **6 meses corridos** de sistema operando estable en VPS sin caídas
  significativas.
- **Una actualización del plugin de Binance falla** porque el parche no se
  puede re-aplicar limpio sobre la versión nueva.

En la revisión, evaluar nuevamente los tres caminos con el contexto financiero
y operativo de ese momento.

## Reversibilidad

Alta. Quitar el `return` temprano y la línea `_ = _adr030BinaryMarker;`, y
eliminar el campo `_adr030BinaryMarker`, restaura el comportamiento original.
Sin efectos colaterales sobre backtest, sobre la estrategia, ni sobre el
motor de Lean (que nunca fue tocado).

## Implementación

Ver commit `fix(engine): bypass ValidateSubscription for local live mode (ADR-030)`:
modificación puntual en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`.

Verificación física del binario por búsqueda UTF-16 LE de la cadena
`ADR-030-BYPASS-VALIDATE-SUBSCRIPTION` en el .dll desplegado
(`QuantConnect.Brokerages.Binance.dll`). Encontrada en offset 67586.

## Validación funcional

El 2026-05-29 a las 16:51 (wall-clock) el sistema arrancó en `live-paper` sin
disparar `ValidateSubscription`. El log de arranque no contiene la línea de
error de autenticación de QC. El WebSocket de Binance Futures (producción)
conectó correctamente y el warmup completó. Esto confirma que el parche cumple
su propósito: el sistema opera en modo live local con datos reales de Binance
y fills ficticios, sin requerir suscripción QC.

## Referencias

- POLICY.md sección 7.1 (veto de estrategias no validadas en live real).
- ADR-029 (lección de verificación de binario).
- Conversación de decisión: chat Opus del 2026-05-29 (Hito C).

## Pendiente operativo

Este ADR no está integrado en `DECISIONS.md` (el consolidado del proyecto se
encuentra actualizado solo hasta ADR-028). Igual situación que ADR-029. La
integración al consolidado debe agendarse en algún momento del cierre del
Hito C; no es urgente.

---

## Nota de actualización — 2026-05-29

La versión original de este ADR (commit `61b7e45`) describía el parche como
una modificación al "motor de Lean". La verificación de Claude Code durante
la implementación reveló que `ValidateSubscription` vive dentro del plugin
de Binance (`BinanceBrokerage.cs`), no del motor.

Esta actualización corrige el alcance del fork, agrega la sección de
Validación funcional con los datos del arranque exitoso, y deja constancia
del pendiente de integración a `DECISIONS.md`. Sin cambios en la decisión ni
en los trade-offs aceptados.
