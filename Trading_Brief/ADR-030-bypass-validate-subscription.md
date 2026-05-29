# ADR-030 — Bypass de ValidateSubscription para operación en live local sin suscripción QuantConnect

**Fecha:** 2026-05-29
**Estado:** Aceptado
**Hito:** C (paper trading)

## Contexto

Lean local en `LiveMode == true` ejecuta `ValidateSubscription()`, una rutina
que llama a los servidores de QuantConnect para validar identidad y
suscripción. Al intentar lanzar paper trading el sistema falló con
`Invalid api user id or token, cannot authenticate subscription`, y la
verificación en el portal de QC confirmó: "To request an access token, you
must belong to a paid organization."

QuantConnect publicita LEAN como open source (Apache 2.0) pero la validación
de licencia para live mode está hardcodeada dentro del motor, llamando a sus
servidores. Patrón "open core": legal con Apache 2.0, ambiguo éticamente.

Tres caminos evaluados:

1. **Pagar QC (≈ USD 20-60/mes según fuentes externas).** Comodidad máxima,
   mantenimiento delegado del plugin de Binance y del motor, costo recurrente
   indefinido.
2. **Binance Testnet completo.** Sin costo, pero feed sintético del testnet
   (no replica fielmente liquidez ni microestructura del mercado real).
3. **Parchar `ValidateSubscription` localmente.** Sin costo, feed real de
   producción, fills ficticios vía PaperBrokerage. Convierte la copia vendored
   de Lean en un fork con parche puntual que el operador asume mantener.

## Decisión

Camino 3: parchar `ValidateSubscription` para retorno inmediato sin contactar
a QC.

Razón: el sistema está en validación, sin ingresos. Un costo recurrente de
USD 240-720/año no es defendible mientras la rentabilidad no esté demostrada.
La opción Testnet, aunque gratis, degrada el valor de la validación de
infraestructura del Hito C al introducir un feed sintético; el camino 3
preserva la calidad del feed para los objetivos del Hito C.

## Trade-offs aceptados explícitamente

- **Mantenimiento delegado a uno mismo.** Cada actualización del motor Lean
  o del plugin Binance va a requerir re-aplicar el parche.
- **Riesgo de drift.** QC puede endurecer la validación en versiones futuras
  o moverla a más sitios del código. El parche actual podría dejar de ser
  suficiente.
- **Aislamiento del ecosistema comercial QC.** Sin soporte oficial, sin foro
  para mostrar el código modificado.
- **Zona gris ética (no legal).** Apache 2.0 lo permite, pero elude el modelo
  de negocio del vendor.

## Trigger de revisión

Esta decisión se revisa cuando se cumpla CUALQUIERA de:

- **Primer trade rentable real** (no paper, no testnet) operado por el sistema.
- **6 meses corridos** de sistema operando estable en VPS sin caídas
  significativas.
- **Una actualización de Lean falla** porque el parche no se puede re-aplicar
  limpio sobre la versión nueva.

En la revisión, evaluar nuevamente los tres caminos con el contexto financiero
y operativo de ese momento.

## Reversibilidad

Alta. Quitar el `return` temprano y la línea `_ = _adr030BinaryMarker;`, y
eliminar el campo `_adr030BinaryMarker`, restaura el comportamiento original.
Sin efectos colaterales sobre backtest, sobre la estrategia, ni sobre el
plugin Binance.

## Implementación

Ver commit `fix(engine): bypass ValidateSubscription for local live mode (ADR-030)`:
modificación puntual en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`.

Verificación física del binario por búsqueda UTF-16 LE de la cadena
`ADR-030-BYPASS-VALIDATE-SUBSCRIPTION` en el .dll desplegado
(`QuantConnect.Brokerages.Binance.dll`). Encontrada en offset 67586.

## Referencias

- POLICY.md sección 7.1 (veto de estrategias no validadas en live real).
- ADR-029 (lección de verificación de binario).
- Conversación de decisión: chat Opus del 2026-05-29 (Hito C).
