# ROADMAP - Sistema de Trading

> **PropÃ³sito de este documento:** mantener visibilidad del plan completo entre sesiones de trabajo. Cualquier sesiÃ³n con Claude Code (o cualquier desarrollador) debe leer este archivo primero para entender en quÃ© punto estÃ¡ el proyecto.
>
> **Reglas:**
> - Cada refactor completado se marca con âœ… y fecha.
> - Cada refactor en curso se marca con ðŸ”„.
> - Cada refactor pendiente se marca con â¬œ.
> - Los refactors abortados o descartados se marcan con âŒ y se anota la razÃ³n.
> - La columna "Bloque" indica el hito al que pertenece (ver secciÃ³n Plan general).
> - Cuando se complete un refactor, mover su descripciÃ³n detallada al final del archivo, secciÃ³n "Historial completado".

---

## Plan general (hitos del proyecto)

El proyecto estÃ¡ organizado en bloques de trabajo. Los refactors tÃ©cnicos estÃ¡n agrupados por bloque segÃºn cuÃ¡ndo es necesario hacerlos.

**Principio de orden (LÃ³pez de Prado):** primero construir el motor (infraestructura + clasificaciÃ³n de rÃ©gimen), luego validar manualmente con segunda estrategia, **despuÃ©s** automatizar el pipeline de research. Invertir este orden produce automatizaciÃ³n de cosas equivocadas.

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ BLOQUE 0 â€” Estado actual (refactors ya completados)         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ BLOQUE 1 â€” Antes del Hito A (Tests de referencia)           â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ Refactor A2 â€” Logging estructurado con placeholders  âœ…     â”‚
â”‚ Refactor B1 â€” Result<T> donde hay magic values       âœ…     â”‚
â”‚ Refactor B3 â€” Eventos de dominio (OrderSubmitted/...) âœ…    â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        âœ… BLOQUE 1 COMPLETO
                            â†“
              âœ… HITO A: Tests de referencia de
                  indicadores y estrategias
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ BLOQUE 2 â€” Antes del Hito B (RegÃ­menes de mercado)          â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ Refactor #4 â€” Separar IRiskMonitor de IRiskAction     âœ…    â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        âœ… BLOQUE 2 COMPLETO â€” Sistema listo para Hito B
                            â†“
                  âœ… HITO B: ClasificaciÃ³n de regÃ­menes
                  de mercado (HMM con Accord.NET)
                  Paso 1: âœ… Pre-requisitos de Domain (OHLCV, CompatibleRegimes)
                  Paso 2: âœ… Abstracciones + filtro + classifier fake
                  Paso 3: âœ… HMM real + trainer offline + modelo entrenado
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ BLOQUE 3 â€” Antes del Hito C (Paper trading)                 â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ INFRA-1 â€” Path absoluto â†’ AppContext.BaseDirectory   âœ…     â”‚
â”‚ INFRA-2 â€” Monitoreo bÃ¡sico (alertas si algo se cae)  âœ…     â”‚
â”‚ OPS-1 â€” Trading Policy Document (POLICY.md)          âœ…     â”‚
â”‚ OPS-2 â€” StrategyHealthMonitor (POLICY 3.1, U1-U4)     âœ…   â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ âœ… HITO C: Paper trading â€” COMPLETADO 2026-06-09            â”‚
â”‚ âœ… Infraestructura verificada (feed, heartbeat, pings)      â”‚
â”‚ âœ… Primer trade real 2026-06-09T00:30 UTC (BTCUSDT 15m)     â”‚
â”‚    Orden enviada 00:30 UTC, posiciÃ³n cerrada 04:36 UTC.     â”‚
â”‚    Ciclo completo U1â†’U4 validado en paper brokerage.        â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ âœ… HITO E: Segunda estrategia manual â€” COMPLETADO 2026-06-11  â”‚
â”‚ Batch 1 (OFI): OfiContrarianStrategy rechazada Hito G.      â”‚
â”‚ Batch 2 (microestructura): 10 hipÃ³tesis, 2 APROBADAS:       â”‚
â”‚   CvdSellExhaustionStrategy (IS=2.178 / OOS=1.718)          â”‚
â”‚   TradeSizeInstitutionalStrategy (IS=3.985 / OOS=4.186)     â”‚
â”‚ ADR-038, ADR-039. Historial: Trading.Research/strategy_experiments  â”‚
â”‚                                                             â”‚
â”‚ Sub-tareas de infraestructura completadas:                  â”‚
â”‚ âœ… E-INFRA-1: Descarga histÃ³rica AggTrades (BTC/ETH/SOL)    â”‚
â”‚    Script: Trading.Research/download_aggtrades.py                   â”‚
â”‚    47,664 barras 1h por sÃ­mbolo (2021-01-01 â†’ 2026-06-09)  â”‚
â”‚    CSVs en: F:\Mis Documentos\...\AggTrades\features\       â”‚
â”‚ âœ… E-INFRA-2: Custom data loader C# para features 1h        â”‚
â”‚    IMicrostructureProvider + MicrostructureRegistry          â”‚
â”‚    Path configurable vÃ­a MicrostructureDataPath en          â”‚
â”‚    strategies.json (sin copia al build output)              â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ âœ… HITO F: Strategy Scaffolder â€” COMPLETADO 2026-06-11      â”‚
â”‚ New-Strategy.ps1 en la raÃ­z del repo.                       â”‚
â”‚ Uso: .\New-Strategy.ps1 -Name RsiMeanReversion              â”‚
â”‚ Genera: clase IStrategy skeleton + tests (3 stubs) +        â”‚
â”‚ snippet JSON para strategies.json + lÃ­nea StrategyFactory.  â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ âœ… HITO G: IS/OOS + Monte Carlo â€” COMPLETADO 2026-06-11      â”‚
â”‚ Trading.Analytics (C#, strategy-agnostic). Lee CSV IS+OOS,  â”‚
â”‚ calcula 9 mÃ©tricas institucionales, MC block bootstrap 10k. â”‚
â”‚ Gate 1: Tradesâ‰¥50, NetProfit>0, Sharpeâ‰¥0.3, PFâ‰¥1.1.        â”‚
â”‚ Gate 2: P(Sharpe<0)â‰¤20%, MedianMaxDDâ‰¤55%, P5 CAGR>-5%.     â”‚
â”‚ Validaciones: OFI rechazada; CvdSellExhaustion APROBADA     â”‚
â”‚ (OOS Sharpe=1.718, CAGR=30.4%, P(Sharpe<0)=1%);            â”‚
â”‚ TradeSizeInstitutional APROBADA (OOS Sharpe=4.186,          â”‚
â”‚ CAGR=97%, P(Sharpe<0)=0%). 2 candidatas activas.            â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ HITO H: OptimizaciÃ³n de HiperparÃ¡metros                     â”‚
â”‚ BÃºsqueda automatizada con cross-validation por rÃ©gimen:     â”‚
â”‚ - Grid search / optimizaciÃ³n bayesiana.                     â”‚
â”‚ - Criterio de selecciÃ³n robusto (no maximizar Sharpe puro). â”‚
â”‚ - ValidaciÃ³n purged k-fold (LÃ³pez de Prado) para evitar     â”‚
â”‚   leakage temporal.                                         â”‚
â”‚ El rango de bÃºsqueda y el criterio los define el operador,  â”‚
â”‚ NO se automatizan (sobreajuste).                            â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ HITO D-prev: ValidaciÃ³n de broker real (sin estrategia)     â”‚
â”‚ Tareas que no requieren una estrategia corriendo y que      â”‚
â”‚ valen la pena hacer una sola vez, contra Binance live:      â”‚
â”‚ - ConexiÃ³n, API keys, scopes/permissions, withdrawal lock.  â”‚
â”‚ - Ã“rdenes manuales de tamaÃ±o mÃ­nimo: confirmar fill,        â”‚
â”‚   comisiones reales, slippage real medible.                 â”‚
â”‚ - Funding fees en perpetuals (si aplica).                   â”‚
â”‚ - ReconciliaciÃ³n portfolio interno vs portfolio del broker. â”‚
â”‚ Separado de Hito D para evitar que "live con capital chico" â”‚
â”‚ arranque solo porque el broker ya estÃ¡ conectado.           â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ HITO D: Live trading con capital chico                      â”‚
â”‚ REQUISITO: al menos UNA estrategia con walk-forward         â”‚
â”‚ aprobado en Hito G. La EmaCrossStrategy NO opera live       â”‚
â”‚ (ver POLICY 7.1).                                           â”‚
â”‚ Capital chico = orden de magnitud del riesgo psicolÃ³gico    â”‚
â”‚ que el operador puede absorber sin que distorsione su       â”‚
â”‚ juicio operativo. NO es "tan poco que no importa" â€”         â”‚
â”‚ esa racionalizaciÃ³n es exactamente lo que POLICY P4         â”‚
â”‚ busca prevenir.                                             â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                            â†“
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ BLOQUE 4 â€” Cuando el sistema crezca (no urgente)            â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ Value Objects Money/Price/Quantity (cuando haya 2do asset)  â”‚
â”‚ OrderNormalizer separado (cuando haya mÃºltiples callers)    â”‚
â”‚ JerarquÃ­a DomainException                                   â”‚
â”‚ Trading.TestSupport proyecto separado                       â”‚
â”‚ Auditor independiente en Python con TA-Lib (pre-live serio) â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

**Resumen del flujo:** Bloques 1-3 + Hito A-C te llevan al sistema operando en paper sobre la estrategia de desarrollo. Hitos E-F-G-H son la fÃ¡brica de estrategias del futuro â€” se construyen sobre el sistema ya validado operativamente en paper. ReciÃ©n despuÃ©s, Hito D-prev (broker real) y Hito D (live con capital chico, requiere estrategia con walk-forward aprobado).

**Cambio respecto a versiones anteriores del ROADMAP (2026-05-23):** el orden original tenÃ­a Hito D (live con capital chico) inmediatamente despuÃ©s de Hito C (paper), antes de Hitos E-H. Se invirtiÃ³ porque la EmaCrossStrategy es estrategia de desarrollo sin walk-forward, y operar capital real â€” aunque chico â€” sobre una estrategia no validada cuantitativamente contradice el principio P1 de POLICY y el orden institucional (LÃ³pez de Prado: walk-forward antes de capital). Hito C se redefiniÃ³ como validaciÃ³n operativa del sistema, no de la estrategia. Ver entrada de DECISIONS.md correspondiente.

---

## Refactors pendientes

### âœ… BLOQUE 1 â€” Completo

*(Todos los refactors del Bloque 1 estÃ¡n completados. Ver Historial completado.)*

### âœ… BLOQUE 2 â€” Completo

*(Refactor #4 completado. Ver Historial completado.)*

### âœ… BLOQUE 3 â€” Completo

| Estado | ID | Refactor | Bloquea | Comentario |
|---|---|---|---|---|
| âœ… | INFRA-1 | Path absoluto de `strategies.json` a configuraciÃ³n inyectable | Hito C | **Completado 2026-05-17.** Ver historial. Adelantado del orden original del ROADMAP por causar fricciÃ³n operativa en sesiones de Hito B. |
| âœ… | INFRA-2 | Monitoreo bÃ¡sico del sistema en producciÃ³n | Hito C | **Completado 2026-05-20.** Ver historial completado. Tres piezas: persistencia de logs JSONL, heartbeat local, ping a Healthchecks.io. Ver ADR-021. ValidaciÃ³n end-to-end del ping real pendiente para Hito C. |
| âœ… | OPS-1 | Trading Policy Document (`POLICY.md`) | Hito C, OPS-2 | **Completado 2026-05-21.** Documento operativo versionado en la raÃ­z del repo. Define principios inquebrantables, umbrales a nivel sistema, umbrales por estrategia (2 niveles OK/Apagar, calibraciÃ³n absoluta no derivada de backtest), cadencia de revisiÃ³n humana, runbooks de emergencia, polÃ­tica de cambios al sistema en operaciÃ³n, y estado actual por estrategia. Ver ADR-022 para racional de las decisiones operativas tomadas. |
| âœ… | OPS-2 | `StrategyHealthMonitor` â€” implementaciÃ³n runtime de `POLICY.md` secciÃ³n 3 | Hito C | **Completado 2026-05-21.** Ver historial completado. Componente autÃ³nomo (NO implementa `IRiskMonitor`) que consume `OrderFilledEvent` del bus, mantiene mÃ©tricas rolling por `ExecutorIdentifier` y evalÃºa U1-U4 de POLICY 3.1. Al disparar: liquida posiciÃ³n abierta + flag `degraded` + `RiskLimitBreachedEvent(StrategyDegradation)` + log `Critical`. Guard en `BarProcessingService` consulta `IStrategyHealthMonitor.IsExcluded`. Ver ADR-023. |
| âœ… | DEUDA-1 | DiagnÃ³stico del test `AccordHmmClassifierReferenceTests` actualmente skipeado | Hito C | **Completado 2026-05-22.** Ver historial completado. Bug en `SemanticStateMapper` (cuartiles no adaptivos a K) + convergencia de Baum-Welch a Ã³ptimo local. Fix adaptativo a K + multi-seed. ValidaciÃ³n cruzada del modelo de producciÃ³n OK. Ver ADR-024. |
| âœ… | DEUDA-2 | `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest | Hito C (validar si aplica tambiÃ©n en live) | **Cerrada 2026-05-22 como NO reproducible.** Ver historial completado. El diagnÃ³stico instrumentado revelÃ³ que `Initialize()` se ejecuta una sola vez (consola Lean reporta `llamada #1` una vez; JSONL muestra cada mensaje de arranque una vez). NO se aplicÃ³ guard de idempotencia. ValidaciÃ³n pendiente en Hito C: confirmar comportamiento tambiÃ©n en modo Live. |
| âœ… | DEUDA-3 | Logs durante `Initialize()` tienen timestamp del epoch de QC (1997-12-31) | No bloquea Hito C | **Completado 2026-06-03.** `LeanClock.UtcNow` retorna `_algorithm.UtcTime` con fallback a `DateTime.UtcNow` cuando el valor es anterior al aÃ±o 2000. Elimina los timestamps `1997-12-31T19:00:00` en `ProcessStartedUtc` y primeros logs del JSONL. Ver commit `2671694`. |

### âœ… HITO B â€” Completado

| Estado | Paso | DescripciÃ³n | Comentario |
|---|---|---|---|
| âœ… | Paso 1 | Pre-requisitos arquitectÃ³nicos del Domain (OHLCV, CompatibleRegimes, RegimeIncompatibility, validaciÃ³n del loader) | Completado 2026-05-14. Ver historial completado. |
| âœ… | Paso 2 | Abstracciones de rÃ©gimen (`IMarketRegimeClassifier`, `RegimeLabel`, `RegimeClassification`, `MarketRegimeRegistry`, `StrategyRegimeCompatibility`), classifier fake (`ConfigurableMarketRegimeClassifier`), filtro pre-orden en `BarProcessingService`, wiring en `TradingAlgorithmHost` con consolidator 4h dedicado | Completado 2026-05-15. Ver historial completado. |
| âœ… | Paso 3 | HMM real con Accord.NET, trainer offline standalone, modelo entrenado de BTCUSDT perpetual de Binance (ventana 2020-2024, Kâˆˆ{2,3,4} por BIC, mapeo semÃ¡ntico de estados) | Completado 2026-05-19. K=4 elegido por BIC con margen amplio (57644 vs 65912 vs 72556). Mapeo: {0:HighVolatility, 1:Squeeze, 2:Trend, 3:Trend}. Ver ADR-019 y historial completado. |

### â¬œ Post-ADR-028 â€” Validaciones y deudas pendientes

#### âœ… ValidaciÃ³n multi-sÃ­mbolo + multi-timeframe simultÃ¡neos
**Bloque:** continuaciÃ³n de la validaciÃ³n del subsistema.
**Estado:** completado (fecha exacta no registrada â€” documentado retroactivamente).
**DescripciÃ³n:** ValidaciÃ³n de que el subsistema sigue agnÃ³stico cuando los
tres sÃ­mbolos operan en TFs distintos simultÃ¡neamente
(BTC-15m / ETH-1h / TRB-4h). Resultado: backtest OK, cero violaciones del
invariante OPS-2, wiring de consolidators independientes por sÃ­mbolo + TF
sin interacciones inesperadas bajo concurrencia mixta.

#### â¬œ Allocator multi-estrategia
**Bloque:** hito propio.
**Estado:** pendiente.
**DescripciÃ³n:** Hoy cada executor ve `InitialAccountCashUsdt = 100_000`
como suyo para calcular DD, cuando la cuenta es realmente compartida
entre todos los executors. Esto distorsiona las mÃ©tricas de DD
per-monitor en operaciÃ³n multi-estrategia y multi-sÃ­mbolo. El hito
introduce un allocator que asigne capital nominal a cada executor con
visiÃ³n coherente de la cuenta total. Trabajo arquitectÃ³nico no trivial.
**Bloqueantes:** ninguno. DecisiÃ³n del operador sobre cuÃ¡ndo abordarlo.

#### â¬œ DEUDA-2 â€” `OrderListHash` no determinista
**Estado:** pendiente, no bloqueante.
**DescripciÃ³n:** Ver detalle en `DECISIONS.md` (DEUDA-2). El campo
`OrderListHash` del summary del backtest no es bit-idÃ©ntico entre
corridas del mismo modelo con la misma configuraciÃ³n, aunque los order
events sean idÃ©nticos. Workaround actual: validar no-regresiÃ³n por
comparaciÃ³n de `transaction-log.csv`. Fix consiste en identificar quÃ©
campos no-deterministas entran al hash y excluirlos.

#### â¬œ Fix POLICY 7.1 tÃ­tulo "1h" vs config "1h"
**Estado:** pendiente, deuda documental.
**DescripciÃ³n:** Hallazgo de ADR-026 re-anotado en ADR-027 y ADR-028. La
entrada de la estrategia de referencia en `POLICY.md` secciÃ³n 7.1 estÃ¡
titulada como TF "1h" pero el sistema corre actualmente la
configuraciÃ³n Config A que estÃ¡ en 1h (con baselines de los runs
post-fix de ADR-028). Trabajo: alinear tÃ­tulo de la entrada con la
config real, o reescribir la entrada para no aludir a un TF especÃ­fico
dado que la estrategia se ha validado en 15m, 1h y 4h.

### â¬œ HITOS POSTERIORES â€” Planificados

| Estado | ID | Hito | Pre-requisito | Comentario |
|---|---|---|---|---|
| âœ… | HITO-C | Paper trading (validaciÃ³n operativa del sistema) | Bloque 3 âœ… | **Completado 2026-06-09.** Primer trade 2026-06-09T00:30 UTC (BTCUSDT 15m), posiciÃ³n cerrada 04:36 UTC. Ciclo completo U1â†’U4 validado. Ver historial completado. |
| âœ… | HITO-E | Segunda estrategia manual â€” COMPLETADO 2026-06-11 | Hito C âœ… | **Batch 1 (OFI):** 13 candidatas evaluadas, OfiContrarianStrategy aprobada IS (Sharpe=0.503) pero rechazada Hito G (OOS Sharpe=-0.703). ADR-038, ADR-039. **Batch 2 (microestructura, 2026-06-11):** 10 hipÃ³tesis evaluadas (H1-H10). 5 pasaron M4. 2 APROBADAS Hito G: `CvdSellExhaustionStrategy` (IS=2.178 / OOS=1.718) y `TradeSizeInstitutionalStrategy` (IS=3.985 / OOS=4.186). 3 RECHAZADAS IS (H1 VwapDeviation=-0.369, H2 TradeCountSpike=-1.553, H10 SellingClimax=-5.128). Ver `Trading.Research/strategy_experiments.md`. |
| âœ… | HITO-F | Strategy Scaffolder | Hito E | **Completado 2026-06-11.** `New-Strategy.ps1` en raÃ­z del repo. Genera clase `IStrategy` + tests skeleton. Imprime snippet JSON y entrada `StrategyFactory`. Ver historial completado. |
| âœ… | HITO-G | IS/OOS Validation + Monte Carlo + MÃ©tricas | Hito F | **Completado 2026-06-11.** `Trading.Analytics` (C#, strategy-agnostic): lee transaction-log.csv IS+OOS, calcula 9 mÃ©tricas institucionales, block bootstrap MC 10k sims. Gate 1: Tradesâ‰¥50, NetProfit>0, Sharpeâ‰¥0.3, PFâ‰¥1.1. Gate 2: P(Sharpe<0)â‰¤20%, MedianMaxDDâ‰¤55%, P5 CAGR>-5%. Validaciones: OfiContrarianStrategy RECHAZADA; CvdSellExhaustion APROBADA (OOS Sharpe=1.718, P(Sharpe<0)=1%); TradeSizeInstitutional APROBADA (OOS Sharpe=4.186, P(Sharpe<0)=0%). Estado: **2 candidatas activas** listas para Hito D-prev / Hito D. |
| â¬œ | HITO-H | OptimizaciÃ³n de HiperparÃ¡metros | Hito G | Grid search / bayesiana con purged k-fold cross-validation (LÃ³pez de Prado) para evitar leakage temporal. El rango de bÃºsqueda y el criterio los define el operador. |
| â¬œ | HITO-D-prev | ValidaciÃ³n de broker real (sin estrategia) | Hito H (o paralelo a Hito G/H si tiempo lo permite) | Tareas one-shot contra Binance live que no requieren estrategia corriendo: API keys + scopes + withdrawal locks; Ã³rdenes manuales de tamaÃ±o mÃ­nimo para medir comisiones reales y slippage real; funding fees en perpetuals; reconciliaciÃ³n portfolio interno vs broker. Separado de Hito D para que "live con capital chico" no arranque solo porque el broker ya estÃ¡ conectado. |
| â¬œ | HITO-D | Live trading con capital chico | Hito D-prev + al menos una estrategia con walk-forward aprobado en Hito G | Requisito de entrada **inquebrantable**: estrategia con walk-forward aprobado. EmaCrossStrategy NO opera live (POLICY 7.1). El tamaÃ±o "chico" es el riesgo psicolÃ³gico que el operador puede absorber sin distorsionar su juicio operativo; no es "tan poco que no importa" (POLICY P4). |

### â¬œ BLOQUE 4 â€” Postergado (no urgente)

| Estado | ID | Refactor | Comentario |
|---|---|---|---|
| â¬œ | A4/A5 | Value Objects `Money`, `Price`, `Quantity`, `Notional` | Hacer cuando se agregue un segundo asset class o cuando aparezca un bug por confusiÃ³n `decimal` â†’ `decimal`. |
| â¬œ | A6 | `OrderNormalizer` separado del `PositionSizer` | Hacer cuando exista un segundo caller del `IOrderRouter` que no pase por el `PositionSizer`. |
| â¬œ | B2 | JerarquÃ­a `DomainException` base | Mejora ergonomÃ­a, no previene bugs. Hacer cuando la cantidad de excepciones de dominio justifique la base comÃºn. |
| â¬œ | B5 | Proyecto separado `Trading.TestSupport` para fakes compartidos | Hacer cuando exista una segunda suite de tests que necesite los fakes. |
| â¬œ | A3 | `IOrderIdGenerator` inyectable | Purismo: testabilidad determinista del registry. `Guid.NewGuid()` funciona y no afecta dinero. |
| â¬œ | AUDIT-1 | Auditor independiente en Python con TA-Lib | Para live trading serio: auditorÃ­a verdaderamente independiente del runtime de QC. El auditor actual en C# detecta bugs de flujo de control y estado interno, pero comparte motor de cÃ¡lculo con QC. Python + TA-Lib provee independencia plena. |
| â¬œ | SYSREG-1 | RÃ©gimen sistÃ©mico (segunda capa de clasificaciÃ³n, agregada al mercado) | ExtensiÃ³n natural de Hito B. Hito B clasifica rÃ©gimen **por activo** (cÃ³mo se comporta BTC ahora, cÃ³mo se comporta SOL ahora). El rÃ©gimen sistÃ©mico clasifica el estado del mercado cripto **en agregado** (risk-on/risk-off, alta correlaciÃ³n entre activos vs baja, dominancia de BTC vs alts). Vive en una capa superior y se compone con el rÃ©gimen por activo: el sistÃ©mico responde "Â¿hoy es dÃ­a para operar?", el especÃ­fico responde "Â¿quÃ© estrategia opera en este activo ahora?". DecisiÃ³n tÃ©cnica abierta cuando se implemente: quÃ© Ã­ndice usar (BTC dominance, market-cap-weighted top N, equal-weighted top N), cÃ³mo componer las dos seÃ±ales cuando discrepan, quÃ© hacer cuando el sistÃ©mico cambia a hostil mientras hay posiciones abiertas. Trigger sugerido: operar despuÃ©s de Hito E (segunda estrategia) y antes de escalar a >3 activos, cuando la diversificaciÃ³n cross-asset empieza a tener peso real en la curva de equity. Requiere ADR al implementarlo. |
| â¬œ | NEURAL-1 | InvestigaciÃ³n de redes neuronales para clasificaciÃ³n de rÃ©gimen (candidata a justificar extracciÃ³n de `Trading.Regimes` como proyecto separado) | ExploraciÃ³n futura: evaluar si modelos neuronales (LSTM, Transformer, change-point detection con redes) agregan valor sobre el HMM de Hito B para clasificaciÃ³n de rÃ©gimen. La abstracciÃ³n `IMarketRegimeClassifier` se diseÃ±Ã³ desde Hito B para ser agnÃ³stica del algoritmo: una implementaciÃ³n neuronal se enchufa al lado de `AccordHmmClassifier` sin tocar nada del contrato ni del orquestador (open-closed). Trigger sugerido para activar este hito: cuando aparezca al menos una de tres seÃ±ales â€” (1) dependencias pesadas o conflictivas (TorchSharp/ONNX runtime ~500MB pesa demasiado para meter directo en `Trading.Strategies`); (2) el pipeline de entrenamiento se vuelve un sub-sistema en sÃ­ mismo (data loaders, purged k-fold, hyperparameter tuning, experiment tracking); (3) mÃºltiples clasificadores en producciÃ³n que requieren orquestaciÃ³n de ensemble. Cuando una de las tres aplique, la decisiÃ³n de extraer `Trading.Regimes` como proyecto separado se toma con evidencia concreta. Requiere ADR al implementarlo. |
| â¬œ | OPS-3 | Persistencia del estado de `StrategyHealthMonitor` entre reinicios del proceso | Hoy las mÃ©tricas viven in-memory desde el arranque del proceso (ADR-014 + ADR-023). Si el proceso reinicia, se pierde historial reciente; el monitor entra en warm-up y los rolling se re-arman tras los prÃ³ximos 50 trades. Aceptable para paper. En live serio, una caÃ­da del proceso seguida de restart resetea la detecciÃ³n U3/U4 silenciosamente: si la estrategia venÃ­a generando alertas sostenidas que aÃºn no llegaban a 10 trades consecutivos, el contador se borra. Fix esperado: serializar `HealthSnapshot`-equivalente por estrategia a `health/strategy-health-{executorIdentifier}.json` con flush atÃ³mico cada N trades cerrados; cargar al boot. DecisiÃ³n tÃ©cnica abierta: Â¿quÃ© pasa si el archivo estÃ¡ corrupto al cargar? (fail loud vs. arrancar warm). Requiere ADR propio. Trigger: antes de migrar a live serio (post Hito D). |
| â¬œ | EVCAL-1 | `EventCalendarMonitor` â€” automatizaciÃ³n de la pausa Â±30min en eventos macro programados | AutomatizaciÃ³n de la regla operativa documentada en `POLICY.md` secciÃ³n 2.3 (FOMC, CPI USA, NFP, halvings de BTC, anuncios regulatorios). Hoy se cumple manualmente: el operador consulta calendario econÃ³mico semanalmente y desactiva la(s) estrategia(s) en `strategies.json` antes del evento. ImplementaciÃ³n futura: consultar un proveedor de calendario econÃ³mico (ForexFactory scraping, Trading Economics API, FRED, o equivalente), exponer `IEventCalendar` en Domain, instrumentar `BarProcessingService` para consultar el calendario antes de generar seÃ±ales nuevas, y bloquear entradas (no salidas, no gestiÃ³n de abiertas) durante la ventana Â±30min. Trigger sugerido para activarlo: cuando aparezca al menos una de tres seÃ±ales â€” (1) segunda estrategia activa simultÃ¡neamente en producciÃ³n (coordinar pausa manual de varias estrategias se vuelve error-prone); (2) sistema operando >7 dÃ­as seguidos sin supervisiÃ³n humana diaria; (3) un incidente concreto registrado en `DECISIONS.md/incidents/` de "se pasÃ³ pausar antes del evento y entrÃ³ posiciÃ³n en mal momento". DecisiÃ³n tÃ©cnica abierta cuando se implemente: quÃ© proveedor de calendario, cÃ³mo manejar caÃ­da del proveedor (fail-safe: Â¿pausar todo o continuar?), cÃ³mo manejar timezone (eventos publicados en horarios locales del paÃ­s emisor). Requiere ADR al implementarlo. |

---

## Historial completado

> Los refactors completados se mueven acÃ¡ con su fecha y un resumen de quÃ© cambiÃ³. Orden cronolÃ³gico: mÃ¡s antiguo arriba.

### âœ… HITO G â€” IS/OOS Validation + Monte Carlo
**Fecha:** 2026-06-11
**Resumen:** Pipeline reproducible de validaciÃ³n de estrategias implementada como herramienta C# standalone `Trading.Analytics` (proyecto console, net10.0, strategy-agnostic). Lee `transaction-log.csv` generado por Lean para IS y OOS, reconstruye trades completos (FIFO pairing), calcula 9 mÃ©tricas institucionales (Sharpe, Sortino, Calmar, Profit Factor, Expectancy, Win Rate, Max DD, CAGR, Recovery Factor), corre Monte Carlo con block bootstrap (bloque=5, overlapping, 10k simulaciones, seed=42) sobre trades OOS, y evalÃºa dos gates de aprobaciÃ³n. Gate 1 (mÃ©tricas deterministas OOS): Tradesâ‰¥50, NetProfit>0, Sharpeâ‰¥0.3, PFâ‰¥1.1, Expectancy>0. Gate 2 (distribuciÃ³n MC): P(Sharpe<0)â‰¤20%, MedianMaxDDâ‰¤55%, P5 CAGR>-5%. Exit code 0 si pasa, 1 si falla. Genera reporte markdown con tabla comparativa IS vs OOS. Primera estrategia validada: `OfiContrarianStrategy` â€” IS Sharpe=0.564 (Gate 1: PASA), OOS Sharpe=-0.703 (Gate 1+2: FALLA). DiagnÃ³stico: Win rate colapsÃ³ 44%â†’36%, P(Sharpe<0)=77%. Edge ligado a bull market 2021-2024, no generaliza. Estrategia eliminada del repo. Ver ADR-039.

### âœ… HITO F â€” Strategy Scaffolder
**Fecha:** 2026-06-11
**Resumen:** Script PowerShell `New-Strategy.ps1` en la raÃ­z del repo. Uso: `.\New-Strategy.ps1 -Name RsiMeanReversion`. Genera dos archivos: (1) `Trading.Strategies/Implementations/{Name}Strategy.cs` con clase `sealed`, `IStrategy`, `WarmUpBars`, `EvaluateSignal` con `Dictionary<string, object>` por ticker y TODO comments; (2) `Trading.Application.Tests/Strategies/{Name}StrategyTests.cs` con tres tests stubs: `WarmUpBars_ReturnsExpectedValue`, `EvaluateSignal_DuringWarmUp_ReturnsFlat` y `EvaluateSignal_TODO_DescribeScenario`. Imprime en consola: lÃ­nea de registro en `StrategyFactory.cs` y snippet JSON para `strategies.json`. Guard fail-loud si alguno de los dos archivos ya existe. NormalizaciÃ³n del nombre: acepta con o sin sufijo "Strategy". Implementado en PowerShell puro (sin dependencias externas), ASCII-only para compatibilidad con PS 5.1 sin BOM. MotivaciÃ³n: con objetivo de cartera multi-estrategia/multi-activo, el scaffolder reduce el costo marginal de evaluar cada candidata en fase M4 y garantiza que todas las estrategias arrancan con la estructura y convenciones correctas.

### âœ… HITO C â€” Paper trading: validaciÃ³n operativa del sistema
**Fecha:** 2026-06-03 (inicio) â†’ 2026-06-09 (cierre)
**Resumen:** ValidaciÃ³n operativa completa del sistema bajo wall-clock real con paper brokerage de Lean y data feed real de Binance Futures USDM. El propÃ³sito del hito fue validar la infraestructura, no la estrategia (EmaCrossStrategy es estrategia de desarrollo sin walk-forward). **Infraestructura verificada** desde 2026-06-03: feed sano (`BarStalenessSeconds` ~143-320s en mercado activo), heartbeat actualizÃ¡ndose cada 60s, pings a Healthchecks.io cada 5 min, JSONL escribiendo correctamente. **Tres bugs corregidos durante el hito** (ADR-031): (1) LeanClock UTC offset (+4h) â€” `_algorithm.Time` â†’ `_algorithm.UtcTime`; (2) auto-restart vÃ­a `Environment.Exit(1)` cuando staleness > 1200s (parche operativo para race condition del plugin Binance); (3) epoch QC 1997-12-31 en Initialize() â€” fallback a `DateTime.UtcNow` cuando `UtcTime < aÃ±o 2000`. **Fix adicional durante el hito** (ADR-032): warm-up dinÃ¡mico de indicadores internos de estrategia â€” `WarmUpBars` en `IStrategy`, `isWarmingUp` flag en `BarProcessingService`, cÃ¡lculo dinÃ¡mico de `SetWarmUp` como `max(HMM mÃ­nimo, max estrategias Ã— timeframe)`. **Primer trade real** 2026-06-09T00:30 UTC (BTCUSDT 15m, seÃ±al de cruce de EMA), posiciÃ³n cerrada 2026-06-09T04:36 UTC (SL o TP). Ciclo completo U1â†’U4 validado con equity en movimiento. `KillSwitchActive: false` â€” riesgo dentro de parÃ¡metros en todo momento. Tests al cierre: 146 verdes (143 Application + 3 nuevos de warm-up). Ver ADR-031, ADR-032.

### âœ… ValidaciÃ³n multi-sÃ­mbolo + fix estructural OPS-2 (ADR-028)
**Fecha:** 2026-05-26 / 2026-05-27
**Resumen:** Cierre de la validaciÃ³n de agnosticismo del subsistema de
ejecuciÃ³n/monitoreo bajo operaciÃ³n concurrente real con tres sÃ­mbolos
(BTCUSDT, ETHUSDT, TRBUSDT) en mismo TF (1h), con sus propios
clasificadores HMM independientes (OpciÃ³n A â€” un HMM por sÃ­mbolo).
Cambios principales: `HmmTrainer` parametrizado por instrumento vÃ­a
CLI (`--instrument`, `--data-dir`, `--output`), `MinimumRequiredBars`
ajustado de 10000 a 5000 (piso tÃ©cnico defendible para HMM-GMM K=4
multi-seed), output default del trainer pasa a `Trading.Models/regime/staging/`
con promociÃ³n manual gateada por criterios uniformes (Kâˆˆ{3,4}, al menos
un estado Trend, ningÃºn estado <5% ni >70%, ningÃºn label agregado >85%).
ETHUSDT entrenado (K=4, BIC 56707.84, Trend 52% / Squeeze 31% / HighVol
17%) y TRBUSDT entrenado (K=4, BIC 49814.19, Trend 47% / Squeeze 40% /
HighVol 13%) â€” ambos pasan los 6 criterios y promovidos a producciÃ³n.
Modelo BTC re-entrenado con multi-seed en ADR-027 antes de esta
validaciÃ³n. **Hallazgo crÃ­tico durante el primer backtest paralelo:**
dos violaciones del invariante OPS-2 producidas por un bug estructural
latente del flujo `LiquidateAll` del kill switch global, que existÃ­a
desde antes pero no se manifestaba en single-symbol. Causa raÃ­z:
`LeanBrokerageAdapter.LiquidateAll` llamaba `_algorithm.Liquidate()`
(helper de Lean) produciendo Ã³rdenes con `Tag = "Liquidated"` no
registradas en `OrderRegistry`, que `OrderEventMapper` descartaba y
dejaban a `StrategyHealthMonitor` desincronizado de Lean. Fix
estructural: `IOrderRouter.LiquidateAll()` eliminado del contrato,
`LeanBrokerageAdapter.LiquidateAll()` eliminado de la implementaciÃ³n,
`LiquidateAllRiskAction` refactorizado para recibir lista de
instrumentos activos por inyecciÃ³n e iterar con
`LiquidateInstrument(instrumentId, OrderPurpose.Liquidate,
"RiskOrchestrator_KillSwitch")` solo para los efectivamente invertidos.
`OrderPurpose.Liquidate` agregado al enum del dominio. Cambios
colaterales aceptados (con nota de proceso por desviaciÃ³n del brief
original): `OrderLifecycleService` broadcast del fill al executor del
instrumento cuando el `ExecutorIdentifier` sintÃ©tico del kill switch no
matcha, condicionado a `Purpose==Liquidate && Status==Filled`;
`StrategyHealthMonitor` case nuevo `Liquidate` con guard de posiciÃ³n
abierta. **Backtest post-fix 2025-01-01 â†’ 2026-03-31:** cero OPS-2
invariante violado, cero `OrderEventMapper: evento sin tag` durante
liquidaciÃ³n dirigida, 5/5 criterios cualitativos verdes en los 3
executors, 3 kill switches activados sin ejercitar el path del
broadcast por estado real (cobertura del path por 8 tests unitarios
nuevos). Test suite final: 132 verdes (11 nuevos: 4
`LiquidateAllRiskActionTests`, 5 `OrderLifecycleServiceLiquidateTests`,
3 `StrategyHealthMonitorTests`). Deudas que quedan abiertas: DEUDA-2
(`OrderListHash` no determinista), allocator multi-estrategia, POLICY 7.1
tÃ­tulo "1h" vs "15m" actual, varianza numÃ©rica del trainer en dÃ­gitos
12+. EmaCrossStrategy sigue VETADA para live por POLICY P1. Ver ADR-028.

### âœ… ADR-027 â€” Re-entrenamiento de BTC con trainer multi-seed (alineaciÃ³n post-DEUDA-1)
**Fecha:** 2026-05-26
**Resumen:** Al abrir sesiÃ³n multi-sÃ­mbolo, la verificaciÃ³n de no-regresiÃ³n del `HmmTrainer` parametrizado revelÃ³ que el modelo de producciÃ³n BTC fue generado antes del commit `6f72dcc` (DEUDA-1, multi-seed Baum-Welch). DecisiÃ³n del operador: re-entrenar BTC para tener flota consistente con los futuros modelos de ETH y TRB. Modelo preDEUDA1 conservado como `BTCUSDT-perp-binance.hmm.json.preDEUDA1`. Nuevo modelo: K=4, BIC=57643.8833, mapping `{0:Trend, 1:Trend, 2:Squeeze, 3:HighVolatility}`. Test granular ventana 3 (crash feb 2025): crash de Feb 3 sigue clasificado como `Trend` âœ“. Backtest BTC-15m post-reentrenamiento: resultados bit-idÃ©nticos a ADR-026 (147 Ã³rdenes, End Equity 87148.16, DD 21.5%, Sharpe -1.288, U2 dispara 2025-02-06, OPS-2 invariante violado 0, evento sin tag 0). La invarianza semÃ¡ntica del modelo explica la identidad de resultados. Ver ADR-027.

### âœ… DEUDA-1 â€” SemanticStateMapper adaptativo a K + multi-seed Baum-Welch + validaciÃ³n cruzada del modelo HMM
**Fecha:** 2026-05-22
**Resumen:** Cierre de la deuda tÃ©cnica documentada en ADR-020: el test `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` estaba marcado `[Fact(Skip = "...")]` por convergencia degenerada del pipeline HMM con K=3 sobre serie sintÃ©tica. **Fase 1 (diagnÃ³stico):** ejecuciÃ³n del test instrumentado con `ITestOutputHelper` confirmÃ³ dos hipÃ³tesis simultÃ¡neas: (A) Baum-Welch convergÃ­a a Ã³ptimo local malo con seed=42 (dos estados colapsados a parÃ¡metros casi idÃ©nticos); (B) `SemanticStateMapper.Build` calculaba `topQuartileThreshold = Ceiling(K * 0.75)`, que con K=3 da 3, haciendo `positionInSorted >= 3` insatisfacible en array de 3 elementos â€” ningÃºn estado se mapeaba a `HighVolatility`. **Fase 2 (fix):** (1) `SemanticStateMapper` refactorizado con umbrales adaptativos a K: K=2 binario, K=3 tercios, Kâ‰¥4 cuartiles tradicionales; (2) `HmmTrainer/Program.cs` extendido con multi-seed Baum-Welch (10 seeds `42*i+17`, conserva el de mayor log-likelihood); (3) mismo fix de multi-seed aplicado al mÃ©todo auxiliar del test de referencia. **Fase 3 (revalidaciÃ³n):** test pasa verde. `SemanticStateMapperTests` extendido con 5 tests adicionales cubriendo K=2, K=3 (caso bug), K=3 con Squeeze, K=4, K=5. **Fase 4 (validaciÃ³n cruzada):** 5 ventanas histÃ³ricas de BTCUSDT (2025-2026) inspeccionadas visualmente por el operador contra las clasificaciones del modelo de producciÃ³n (K=4). Todas OK o AMBIGUAS (ninguna contradicciÃ³n frontal). Ventana 3 (2025-01-26 â†’ 2025-02-10) resuelta con consulta granular barra a barra vÃ­a `ProductionHmmGranularQueryTests.cs`: el crash de Feb 3 (caÃ­da direccional ~8%) fue clasificado como `Trend`, no `HighVolatility` â€” coherente con la definiciÃ³n del modelo (crashes direccionales = Trend; caos bidireccional = HighVolatility). **Fase 5:** NO re-entrenamiento. Modelo de producciÃ³n vÃ¡lido, baseline de 6 Ã³rdenes (ADR-023) preservado. **Fase 6 (cierre):** instrumentaciÃ³n TEMP DEUDA-1 removida; `[Fact(Skip)]` â†’ `[Fact]`; ADR-024 nuevo en DECISIONS.md; ADR-020 pasa a "Resuelta". `ProductionHmmGranularQueryTests.cs` y `briefs/DEUDA_1_ventana3_granular.md` commiteados como evidencia durable. Ver ADR-024.

### âœ… DEUDA-2 â€” `Initialize()` doble en backtest: NO reproducible al ejecutar diagnÃ³stico
**Fecha:** 2026-05-22
**Resumen:** DiagnÃ³stico ejecutado segÃºn brief `DEUDA_2_BRIEF.md` (Fase 1: instrumentaciÃ³n con contador atÃ³mico de invocaciones y log con hash de instancia). Resultado: `Initialize()` se ejecuta **UNA sola vez** en backtest. La consola de Lean reporta `llamada #1` una vez y el JSONL del run (`trading-2026-05-22.jsonl`, 6 lÃ­neas totales) muestra cada uno de los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) exactamente una vez. La deuda documentada al cierre de INFRA-2 (ADR-021) no es reproducible con el cÃ³digo actual. Causa probable: el diagnÃ³stico original fue por inferencia (logs duplicados â†’ ergo doble invocaciÃ³n), no por instrumentaciÃ³n directa; los duplicados observados al cierre de INFRA-2 pudieron tener otra causa que se resolviÃ³ incidentalmente con los cambios de OPS-1/OPS-2 al wiring del host (no se conserva el JSONL del cierre de INFRA-2 para confrontaciÃ³n directa). **NO se aplica guard de idempotencia:** fixes solo a problemas reproducidos (regla institucional, consistente con Riesgo 2 del brief `DEUDA_2_BRIEF.md`). La instrumentaciÃ³n temporal de Fase 1 (`_initializeCallCount` + logs de hash de instancia) fue revertida; el cÃ³digo de `TradingAlgorithmHost.cs` queda idÃ©ntico al estado pre-Fase 1. **ValidaciÃ³n pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el sÃ­ntoma tampoco aparece en modo Live; si aparece, abrir nueva deuda con diagnÃ³stico fresco. Sin cambios de cÃ³digo de producciÃ³n. Sin ADR nuevo (decisiÃ³n documentada en esta entrada del historial y nota al ADR-021).

### âœ… Refactor inicial â€” Naming consistente
**Fecha:** sesiÃ³n 1
**Resumen:** todos los identificadores en inglÃ©s, campos privados con `_`, eliminaciÃ³n de abreviaturas. Comportamiento preservado.

### âœ… Refactor de RiskParameters como Value Object
**Fecha:** sesiÃ³n 1
**Resumen:** creaciÃ³n de `RiskParameters` value object con invariantes (stop, take profit, riesgo por trade) verificadas en construcciÃ³n. Eliminado fallback silencioso `if (stopLossPercentage <= 0) ...= 0.03m` del `PositionSizer`. ConversiÃ³n `/100m` centralizada en `FromPercentages`. 21 tests xUnit.

### âœ… Refactor de desacople de QuantConnect
**Fecha:** sesiÃ³n 1
**Resumen:** creaciÃ³n de `Trading.Application` como proyecto separado. IntroducciÃ³n de abstracciones del dominio: `IPortfolioState`, `IInstrumentMetadata`, `IOrderRouter`, `IOrderHandle`, `IClock`, `ITradingLogger`, `IPriceRounder`. `MarketBar` reemplaza `MarketData`. `InstrumentId` reemplaza `Symbol` de QC en el dominio. Adaptadores Lean creados en `Trading.Strategies/Adapters`. 12 tests adicionales del `KillSwitchManager` con fakes. Invariante: `Trading.Domain` y `Trading.Application` cero `using QuantConnect`.

### âœ… UnificaciÃ³n de carpetas `Interfaces/` â†’ `Abstractions/`
**Fecha:** sesiÃ³n 1
**Resumen:** eliminada la carpeta `Trading.Domain/Interfaces/`. Movidos `IStrategy` e `IStrategyConfigLoader` a `Trading.Domain/Abstractions/`.

### âœ… RiskPerTradePercentage por estrategia en JSON
**Fecha:** sesiÃ³n 1
**Resumen:** campo `RiskPerTradePercentage` obligatorio en `strategies.json`. `StrategyConfigLoader` falla loud si estÃ¡ ausente (decimal nullable para distinguir "ausente" de "presente con valor 0"). Eliminado el default 2% hardcodeado en `TradingAlgorithmHost`.

### âœ… EliminaciÃ³n de stringly-typed tags
**Fecha:** sesiÃ³n 1
**Resumen:** enum `OrderPurpose { Entry, StopLoss, TakeProfit, TimeExit }` reemplaza strings ENTRY/SL/TP/TIME. `OrderRegistry` central en `Trading.Application` mapea tags opacos (`ord_xxxxxxxx`) a registraciones estructuradas. `IOrderRouter` cambia firma para recibir `OrderPurpose` + `executorIdentifier`. `OrderLifecycleEvent` expone `Purpose` y `ExecutorIdentifier` resueltos. Cleanup automÃ¡tico del registry tras eventos terminales. 9 tests adicionales del `OrderRegistry`.

### âœ… Fix eventos huÃ©rfanos y sobreescritura de tags
**Fecha:** sesiÃ³n 2
**Resumen:** descubierto en log de operaciones que `OrderTicket.Cancel(reason)` de Lean sobreescribe el `Tag` del ticket. Fix en `LeanOrderHandle.Cancel`: ya no propaga el reason a Lean. `OrderEventMapper` distingue tag con nuestro prefijo (residual esperado, Debug) de tag externo (liquidaciÃ³n global, Debug con mensaje distinto). `OrderLifecycleService` loguea Info con motivo de cancelaciÃ³n antes de invocar `Cancel`. Logs limpios: 0 mensajes anÃ³malos en backtest posterior.

### âœ… Habilitar Long y Short en estrategias
**Fecha:** sesiÃ³n 2
**Resumen:** enum `SignalDirection { Flat, Long, Short }` reemplaza el `bool` de `IStrategy.EvaluateSignal`. `EmaCrossStrategy` ahora produce `Short` en cruces bajistas (antes los ignoraba). `BarProcessingService` aplica signo a la cantidad segÃºn direcciÃ³n. `PositionSizer` sigue devolviendo magnitud positiva (sin cambios). Sin tests nuevos por decisiÃ³n de minimalismo.

### âœ… Refactor B1 â€” Result<T> donde habÃ­a magic values (alcance: PositionSizer)
**Fecha:** 2026-05-12
**Resumen:** Se crearon dos tipos genÃ©ricos en `Trading.Domain/Abstractions/`: `Result<TValue, TFailureReason>` y `Result<TFailureReason>` como `readonly record struct` para evitar allocations en el hot path. Se creÃ³ el enum `SizingFailureReason` con tres valores: `InvalidPrice`, `QuantityRoundsToZero`, `BelowMinimumNotional`. `PositionSizer.CalculateQuantity` cambiÃ³ de retornar `decimal` (magic value `0m` ante error) a `Result<decimal, SizingFailureReason>`: ahora distingue explÃ­citamente Ã©xito, precio invÃ¡lido y cantidad que redondea a cero. `PositionSizer.IsValidNotional` fue renombrado a `ValidateNotional` y retorna `Result<SizingFailureReason>`. `BarProcessingService` fue adaptado como caller: agrega `ITradingLogger` al constructor (tambiÃ©n wireado en `TradingAlgorithmHost`) y loguea en Debug el motivo de skip al recibir un `Failure`. Se crearon `FakeInstrumentMetadata`, `FakeStrategy` en el proyecto de tests. Se aÃ±adieron 7 tests nuevos en `PositionSizerTests`. Total: 29 tests verdes (0 errores). Invariante arquitectÃ³nica preservada: cero `using QuantConnect` en Domain/Application/Tests.

### âœ… Refactor B3 â€” Eventos de dominio tipados
**Fecha:** 2026-05-12
**Resumen:** Se creÃ³ la marker interface `IDomainEvent` y cuatro eventos tipados en `Trading.Domain/Events/`: `OrderSubmittedEvent`, `OrderFilledEvent`, `OrderCanceledEvent` y `RiskLimitBreachedEvent` (con enum `RiskLimitBreachReason`). Se definiÃ³ la interfaz `IDomainEventBus` en `Trading.Domain/Abstractions/` y se implementÃ³ `DomainEventBus` en `Trading.Application/Eventing/`: bus sÃ­ncrono in-memory con snapshot de suscriptores bajo lock, aislamiento de fallos (un suscriptor que lanza loguea Error y el bus continÃºa). `KillSwitchManager`, `BarProcessingService` y `OrderLifecycleService` reciben `IDomainEventBus` e `IClock` por constructor y emiten el evento correspondiente en cada transiciÃ³n crÃ­tica. `TradingAlgorithmHost` construye el bus y lo inyecta en todos los servicios. Se agregaron `CapturingEventSubscriber<TEvent>` para tests, 7 tests nuevos en `DomainEventBusTests` y 1 test nuevo en `KillSwitchManagerTests`. Total: 37 tests verdes (0 errores). Bloque 1 completo; sistema listo para Hito A.

### âœ… Refactor A2 â€” Logging estructurado con placeholders nombrados
**Fecha:** sesiÃ³n 3 (2026-05-11)
**Resumen:** `ITradingLogger` extendido a 5 niveles (`Debug`, `Info`, `Warning`, `Error`, `Critical`) con firma `(string messageTemplate, params object[] arguments)`. `LeanLogger` convierte placeholders nombrados a posicionales via regex antes de `string.Format`. `FakeTradingLogger` reemplaza las tres `List<string>` por `List<CapturedLogEntry>` con `Level`, `MessageTemplate` y `Arguments`. Migrados 10 call sites en `OrderLifecycleService`, `KillSwitchManager`, `PositionSizer` y `OrderEventMapper`: eliminada toda interpolaciÃ³n `$"..."`. `ActivateKillSwitch` sube de `Error` a `Critical`; `EvaluateCoolingOffPeriod` sube de `Debug` a `Info`. Eliminados prefijos manuales de timestamp. Test `ActivateKillSwitch_LiquidatesAndLogsError` actualizado a `CriticalEntries`. Logs parseables por herramientas de observabilidad (Seq, Datadog). Sin cambios de comportamiento funcional. 21 tests Domain + 20 Application = 41 verde.

### âŒ Fix â€” SignalAuditor: tolerancia relativa en lugar de absoluta (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversiÃ³n:** 2026-05-13
**RazÃ³n:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### âŒ Fix â€” SignalAuditor: eliminar falsos positivos en recÃ¡lculo de EMA (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversiÃ³n:** 2026-05-13
**RazÃ³n:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### âŒ Hito A â€” Auditor de fidelidad de seÃ±ales en backtest (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversiÃ³n:** 2026-05-13
**RazÃ³n:** diseÃ±o equivocado. Recalcular indicadores en vivo durante el backtest dentro del mismo proceso es duplicaciÃ³n, no auditorÃ­a. Tras cuatro fixes iterativos (buffer, warm-up, tolerancia, algoritmo) persistÃ­an ~33% de discrepancias sin causa raÃ­z clara. Reemplazado por tests unitarios estÃ¡ticos contra valores de referencia (baseline QC), que es el estÃ¡ndar institucional documentado por la propia QuantConnect. Ver ADR-014.

### âœ… Hito A (versiÃ³n 2) â€” Tests de referencia de indicadores y estrategias
**Fecha:** 2026-05-13
**Resumen:** eliminado completamente el SignalAuditor y todo el cÃ³digo del enfoque anterior (9 archivos borrados, 4 modificados). Reemplazado por dos tipos de tests unitarios estÃ¡ndares institucionales: (1) tests de referencia que verifican que ExponentialMovingAverage de QC produce valores equivalentes al baseline QC sobre serie sintÃ©tica conocida (QC valida internamente contra TA-Lib), (2) tests de comportamiento de EmaCrossStrategy con datos sintÃ©ticos diseÃ±ados para forzar cruces alcistas y bajistas. Cobertura institucional sin overhead runtime. 6 tests nuevos. Total verde: 43 tests. Sanity check final humano (verificaciÃ³n de 3-5 seÃ±ales en TradingView antes de pasar a paper trading) queda como prÃ¡ctica recomendada, no automatizada.

### âœ… Refactor #4 â€” Separar IRiskMonitor de IRiskAction
**Fecha:** 2026-05-13
**Resumen:** `KillSwitchManager` (que mezclaba detecciÃ³n y acciÃ³n) descompuesto en componentes con responsabilidad Ãºnica: `IRiskMonitor` (detecciÃ³n) + `IRiskAction` (mitigaciÃ³n) + `RiskOrchestrator` (coordinaciÃ³n). Tres componentes de risk: `DrawdownMonitor`, `ConsecutiveLossesMonitor` (ambos `IRiskMonitor`) y `CoolingOffTracker` (componente separado porque seÃ±ala desactivaciÃ³n, no activaciÃ³n). `LiquidateAllRiskAction` como Ãºnica implementaciÃ³n de `IRiskAction`. El sistema queda preparado para Hito B: agregar `RegimeIncompatibilityMonitor` serÃ¡ crear una clase nueva sin modificar nada existente (open-closed). 14 tests nuevos. Backtest produce operaciones idÃ©nticas (162). Bloque 2 completo.

### âœ… Hito B â€” Paso 1: Pre-requisitos arquitectÃ³nicos del Domain
**Fecha:** 2026-05-14
**Resumen:** Tres extensiones al Domain para habilitar el resto del Hito B sin acoplamientos prematuros. (1) `MarketBar` extendido a OHLCV completo (`Open`, `High`, `Low`, `Close`, `Volume` como `decimal`), con constructor primario `(InstrumentId, decimal open, decimal high, decimal low, decimal close, decimal volume, DateTime)` y constructor legado `(InstrumentId, decimal close, DateTime)` marcado `[Obsolete]` para retrocompatibilidad temporal (delega al nuevo poblando OHL con close y volumen en 0). (2) `StrategyDefinition` gana propiedad `List<string>? CompatibleRegimes` nullable, modelada como `List<string>` concreto (no `IReadOnlyList`) por consistencia con `RootConfig.Timeframes` y para evitar fricciÃ³n con la deserializaciÃ³n de Newtonsoft.Json. (3) `RiskLimitBreachReason` extendido con `RegimeIncompatibility` (no se emite todavÃ­a, queda definido en el vocabulario del dominio para uso futuro). El `MarketBarMapper` en `Trading.Strategies/Adapters/` actualizado para construir `MarketBar` con OHLCV completo desde el `TradeBar` de Lean. El `StrategyConfigLoader` valida que `CompatibleRegimes`, si estÃ¡ presente, no sea lista vacÃ­a (mensaje explÃ­cito: ausencia = compatible con todo, lista vacÃ­a = invÃ¡lido). Tests nuevos: `MarketBarTests` (3 tests del constructor nuevo y el obsoleto) y tres tests del loader (`Load_FallaSiCompatibleRegimesEstaPresenteVacio`, `Load_AceptaSiCompatibleRegimesEstaAusente`, `Load_AceptaSiCompatibleRegimesTieneValores`). CompilaciÃ³n verde. Backtest sin cambios funcionales: produce los mismos resultados que pre-Paso-1. Ver ADR-017.

### âœ… Hito B â€” Paso 2: Abstracciones de rÃ©gimen + filtro pre-orden con classifier fake
**Fecha:** 2026-05-15
**Resumen:** ImplementaciÃ³n completa de la infraestructura de rÃ©gimen sin acoplarse a ningÃºn algoritmo concreto. **Domain (`Trading.Domain/Abstractions/Regimes/`):** `RegimeLabel` enum (`Unknown`, `Trend`, `MeanReverting`, `HighVolatility`, `Squeeze`) + `RegimeLabelParser.Parse(string)` con mensajes de error explÃ­citos (rechaza `Unknown` como configuraciÃ³n explÃ­cita); `RegimeClassification` record con `Label`, `Probabilities` (distribuciÃ³n completa por `double`), `ClassifiedAtUtc` y constructor estÃ¡tico `UnknownFor`; `IMarketRegimeClassifier` contrato agnÃ³stico del algoritmo (ningÃºn mÃ©todo ni propiedad delata HMM o cualquier otro). **Application (`Trading.Application/Regimes/`):** `MarketRegimeRegistry` con mapa instrumento â†’ classifier y cache de Ãºltima clasificaciÃ³n (instrumento sin classifier â†’ fail-safe a `Unknown`); `ConfigurableMarketRegimeClassifier` que devuelve siempre una etiqueta fija (Ãºtil para tests y validaciÃ³n de wiring); `StrategyRegimeCompatibility` con tres reglas fail-safe (null â†’ compatible con todo, vacÃ­o â†’ compatible con todo, `Unknown` siempre compatible). **IntegraciÃ³n:** `BarProcessingService` recibe `MarketRegimeRegistry` y `IReadOnlyDictionary<string, StrategyRegimeCompatibility>` por constructor; el filtro se inserta como guard `continue` despuÃ©s del check de `KillSwitchActivated` y `SignalDirection.Flat`, antes de los checks de `IsInvested` y `HasOpenOrders`. **Wiring en `TradingAlgorithmHost`:** construcciÃ³n del registry con `ConfigurableMarketRegimeClassifier(BTCUSDT, Trend)`, parseo de `CompatibleRegimes` de cada `StrategyDefinition` a `RegimeLabel`, **consolidator 4h dedicado e independiente** que alimenta al registry (separado de los consolidators de estrategias por separation of concerns â€” el rÃ©gimen es un concepto ortogonal a las estrategias y vive en timeframe propio). Tests nuevos (~30): `RegimeLabelTests`, `RegimeClassificationTests`, `ConfigurableMarketRegimeClassifierTests`, `MarketRegimeRegistryTests`, `StrategyRegimeCompatibilityTests`, `BarProcessingServiceRegimeFilterTests` (6 escenarios end-to-end del filtro). Hallazgo arquitectÃ³nico: el filtro NO va por `RiskOrchestrator` (el patrÃ³n de guards `continue` en `BarProcessingService` es la abstracciÃ³n correcta para un filtro pre-orden por contexto; el `RiskOrchestrator` queda para condiciones catastrÃ³ficas que justifican liquidar todo). Pendiente: agregar `"CompatibleRegimes": ["Trend"]` al `strategies.json` para activar el filtro en runtime. Ver ADR-017.

### âœ… Hito B â€” Paso 3: HMM real con Accord.NET, trainer offline y modelo entrenado de BTCUSDT
**Fecha:** 2026-05-19
**Resumen:** Cierre del Hito B. Reemplazo del `ConfigurableMarketRegimeClassifier` (fake) por `AccordHmmClassifier` (HMM real con emisiones Multivariate Gaussian, topologÃ­a ergÃ³dica, decodificaciÃ³n Viterbi + forward filtering). **Trading.Strategies/Regimes/** gana 7 archivos nuevos: `AccordHmmClassifier`, `AccordHmmClassifierFactory`, `PersistedHmmModel`, `HmmModelSerializer`, `SemanticStateMapper`, `BinanceKlinesParser`, `FeatureExtractor`, `FeatureScaler`. **Trading.Strategies/Tools/HmmTrainer/** es un proyecto de consola standalone (`net10.0`, Exe) que entrena el HMM offline con datos histÃ³ricos de Binance Klines (ventana 2020-01-01 a 2024-12-31 UTC, 10912 features tras descarte de 50 barras de warm-up de SMA50), prueba K âˆˆ {2, 3, 4} y elige por BIC mÃ­nimo. InicializaciÃ³n canÃ³nica HMM-GMM por k-means clustering de las observaciones para romper simetrÃ­a inicial (sin esta inicializaciÃ³n BaumWelch no convergÃ­a). **K elegido: 4** con BIC=57643.94 (margen 12.5% sobre K=3, 20% sobre K=2). Mapeo semÃ¡ntico resultante: estado 0â†’HighVolatility, estado 1â†’Squeeze, estado 2â†’Trend, estado 3â†’Trend (dos estados Trend con bias positivo y negativo respectivamente; permitido por el brief y manejado por el classifier sumando probabilidades por etiqueta). El modelo se serializa a `Trading.Models/regime/BTCUSDT-perp-binance.hmm.json` (JSON indentado por System.Text.Json, legible en code review) y se commitea al repo como artefacto versionado. MSBuild lo copia a `{OutputDir}/Trading.Models/regime/` en cada build. **Refactor del wiring de TradingAlgorithmHost:** extracciÃ³n dinÃ¡mica de instrumentos Ãºnicos del `strategies.json` que tienen estrategias con `CompatibleRegimes`, carga del modelo correspondiente por convenciÃ³n de naming, fail-loud al boot si una estrategia depende del rÃ©gimen pero el modelo no existe. EliminaciÃ³n del hardcoding previo de `btcInstrumentId`. **Fix crÃ­tico del consolidator de rÃ©gimen:** quitado el `if (IsWarmingUp) return;` del handler (irrelevante con el fake del Paso 2, bug con el HMM real que necesita procesar barras durante el warm-up para calentar su buffer interno de 100 features). **SetWarmUp** extendido de 1 dÃ­a a 20 dÃ­as de calendario para cubrir las 100 barras 4h del warm-up del HMM con margen. **Nuevo mÃ©todo** `MarketRegimeRegistry.GetRegisteredInstruments()` para wiring agnÃ³stico. **Nuevo proyecto** `Trading.Strategies.Tests` con tres test fixtures: `AccordHmmClassifierReferenceTests` (pipeline completo sobre serie sintÃ©tica con 3 regÃ­menes â€” Trend alcista, HighVolatility, MeanReverting â€” verificando que K=3 minimiza el BIC, que las clasificaciones discriminan los tres segmentos y que IsWarmedUp toggea correctamente), `SemanticStateMapperTests` (5 escenarios de las reglas de mapeo: cuartil superior, cuartil inferior + alta persistencia, media significativa + persistencia, default MeanReverting, caso degenerado), `BinanceKlinesParserTests` (6 escenarios: fila vÃ¡lida, detecciÃ³n de header, timestamp msâ†’UTC, descompresiÃ³n de zip mensual, fail loud ante datos invÃ¡lidos, filtro por rango de fechas). Tests previos (Paso 1 y Paso 2, ~82 tests) sin cambios. ADR-017 pasa a estado "Aceptada". ADR-019 nuevo documenta los parÃ¡metros especÃ­ficos del HMM, los BICs por candidato, el mapeo resultante y las alternativas consideradas durante la ejecuciÃ³n. Ver ADR-017 y ADR-019.

### âœ… INFRA-1 â€” Path absoluto del strategies.json eliminado y reconciliado con MSBuild
**Fecha:** 2026-05-17
**Resumen:** El `TradingAlgorithmHost.cs` hardcodeaba `F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json` para cargar la configuraciÃ³n de estrategias, generando dos problemas: (a) no portable a otras mÃ¡quinas, (b) dos copias paralelas del JSON sin sincronizar (una en `Trading.Strategies\strategies.json` versionada, otra en `bin\Debug\` que era la que el cÃ³digo leÃ­a efectivamente y que MSBuild no actualizaba al recompilar). El refactor reemplaza el path absoluto por `System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json")`, agrega `<Content Include="strategies.json" CopyToOutputDirectory="PreserveNewest" />` al `Trading.Strategies.csproj` para que MSBuild sincronice automÃ¡ticamente fuente â†’ bin en cada build, y reconcilia el contenido (la fuente versionada quedÃ³ con el contenido correcto `EmaCrossStrategy / BTCUSDT / 1h / RiskPerTradePercentage: 2.0`, la copia del bin eliminada para que MSBuild la regenere). Adelantamiento del refactor INFRA-1 del Bloque 3, que el ROADMAP planificaba antes del Hito C; se adelantÃ³ por causar fricciÃ³n operativa concreta en dos sesiones de trabajo sobre Hito B (confusiÃ³n sobre quÃ© archivo era la fuente de verdad). Backtest sin cambios funcionales: el sistema sigue cargando la configuraciÃ³n correcta, ahora desde una sola ubicaciÃ³n clara. Ver ADR-018.

### âœ… INFRA-2 â€” Monitoreo bÃ¡sico del sistema en producciÃ³n
**Fecha:** 2026-05-20
**Resumen:** Tres piezas que dotan al sistema de observabilidad mÃ­nima para paper trading, ejecutadas y validadas en orden estricto A â†’ B â†’ C con tres fixes correctivos durante el camino. **Pieza A â€” Persistencia de logs JSONL:** nueva interfaz `IStructuredLogSink` (en `Trading.Domain.Abstractions`) y enum `LogLevel` (espejo de los mÃ©todos de `ITradingLogger`, cero dependencias externas en Domain). ImplementaciÃ³n `JsonlFileLogSink` (en `Trading.Strategies.Adapters`) que escribe una lÃ­nea JSON por evento a `logs/trading-{wall-clock-date}.jsonl` con rotaciÃ³n diaria y retenciÃ³n de 30 dÃ­as. Helper estÃ¡tico `LogTemplateRenderer` extrae la lÃ³gica de parseo de placeholders nombrados que estaba embebida en `LeanLogger`. El `LeanLogger` se refactorizÃ³ para recibir el sink por constructor e invocarlo en paralelo al `QCAlgorithm.Log/Debug/Error` sin cambiar firmas pÃºblicas de `ITradingLogger`. Sink thread-safe (lock interno), traga excepciones de I/O para no romper trading. **Pieza B â€” Heartbeat local:** nuevo evento `BarProcessedEvent` (emitido por `BarProcessingService` solo en el camino exitoso, no en early-returns); nuevo componente `HealthHeartbeatTracker` (en `Trading.Application.Health`) suscripto a `BarProcessedEvent`, `OrderSubmittedEvent`, `OrderFilledEvent` y `RiskLimitBreachedEvent`, manteniendo estado in-memory con lock; `HealthSnapshot` record inmutable; `HeartbeatFileWriter` (en `Trading.Strategies.Adapters`) serializa el snapshot a `health/heartbeat.json` con escritura atÃ³mica (`.tmp` + `File.Move` overwrite). Flush periÃ³dico vÃ­a `System.Threading.Timer` cada 60s **solo en `LiveMode`**. **Pieza C â€” Ping externo a Healthchecks.io:** `HealthchecksIoPinger` (en `Trading.Strategies.Adapters`) hace HTTP GET a una URL leÃ­da de la variable de entorno `HEALTHCHECKS_PING_URL`. Throttle interno de 5 minutos. Modo no-op con Warning una sola vez al arranque si la variable no estÃ¡ definida o el formato no matchea (graceful degradation). `HttpClient` long-lived, dispose en `OnEndOfAlgorithm`. Nunca propaga excepciones al caller. **Tres fixes correctivos durante la implementaciÃ³n, todos por el mismo error de fondo (confundir `IClock` con wall clock real en componentes de housekeeping):** (1) el `Schedule.On(TimeRules.Every(60s))` original del heartbeat se disparaba al ritmo del clock simulado del backtest, llevando el tiempo de ejecuciÃ³n de 1 minuto a 20+; reemplazado por `System.Threading.Timer` envuelto en `if (LiveMode)`; (2) la rotaciÃ³n y retenciÃ³n del JSONL usaban `_clock.UtcNow.Date` y eliminaban los propios logs del run; reemplazado por `DateTime.UtcNow.Date` para esas dos operaciones especÃ­ficas, manteniendo `_clock.UtcNow` para el campo `timestamp` de cada evento (que sÃ­ debe reflejar el clock del sistema para correlacionar con Ã³rdenes); (3) los tests `Write_*` del sink fallaban con `IOException` al intentar leer el archivo mientras el sink lo tenÃ­a abierto en modo escritura; corregidos adoptando patrÃ³n `using` con disposiciÃ³n antes de la lectura. **Tests totales agregados:** ~35-40 entre los tres componentes (`JsonlFileLogSinkTests`, `LogTemplateRendererTests`, `HealthHeartbeatTrackerTests`, extensiones de `BarProcessingServiceTests` para verificar emisiÃ³n de `BarProcessedEvent`, `HealthchecksIoPingerTests` con `HttpMessageHandler` mockeado). **MÃ©tricas del backtest idÃ©nticas al baseline** (225 Ã³rdenes, P&L, drawdown), tiempo de ejecuciÃ³n restaurado a ~100 segundos tras los fixes. **Hallazgos no funcionales documentados como deuda:** DEUDA-2 (`Initialize()` se ejecuta dos veces en backtest, revelado por logs duplicados en el JSONL) y DEUDA-3 (timestamps del epoch de QC durante `Initialize()`). **Validaciones pendientes para Hito C** documentadas en ADR-021: confirmar que `heartbeat.json` se actualiza en live, que los pings llegan a Healthchecks.io, que la alerta de Telegram dispara cuando el proceso muere, y si DEUDA-2 aplica tambiÃ©n a live. ADR-021 nuevo cubre todas las decisiones de INFRA-2, alternativas descartadas (Seq/Datadog, Uptime Kuma, Pingdom), el criterio arquitectÃ³nico aprendido (wall clock vs `IClock` en componentes de observabilidad) y la validaciÃ³n pendiente. AI.md ampliado con la regla wall clock vs `IClock`, persistencia JSONL, heartbeat, y nueva secciÃ³n "Variables de Entorno". Ver ADR-021.

### âœ… OPS-2 â€” `StrategyHealthMonitor` â€” implementaciÃ³n runtime de POLICY secciÃ³n 3
**Fecha:** 2026-05-21
**Resumen:** ImplementaciÃ³n completa de los umbrales U1-U4 de POLICY 3.1 como componente runtime. **Pieza A (cableado):** nueva interfaz `IStrategyHealthMonitor` en `Trading.Domain/Abstractions/` con un solo mÃ©todo `bool IsExcluded(string executorIdentifier)`. Nuevo valor `StrategyDegradation` al enum `RiskLimitBreachReason`. Guard en `BarProcessingService` posicionado entre el check de `IsKillSwitchActivated` y el filtro de rÃ©gimen: si `_strategyHealthMonitor.IsExcluded(executorIdentifier)` â†’ `continue`. `NullStrategyHealthMonitor` como placeholder de Pieza A. `FakeStrategyHealthMonitor` en el proyecto de tests. 2 tests nuevos del guard. **Pieza B (monitor completo):** `StrategyHealthThresholds` (POCO inmutable, factory `FromPolicyDefaults()` con los 10 valores literales de POLICY 3.1). `StrategyHealthMonitor` en `Trading.Application/Health/`: consume `OrderFilledEvent` del bus (suscripciÃ³n en constructor), mantiene estado rolling por `ExecutorIdentifier` bajo lock interno (equity acumulado, ATH, ventana de 30 trades cerrados, ventana de 30 puntos diarios de equity, contadores de dÃ­as/trades sostenidos para U2/U3/U4, flag `degraded`). EvalÃºa U1 (DD absoluto desde ATH > 25%) en cada cierre; U2 (DD rolling 30 dÃ­as > 15% sostenido 5 dÃ­as) al avanzar el dÃ­a; U3 (PF rolling < 1.0 sostenido 10 trades) y U4 (expectancy rolling < 0 sostenido 10 trades) armados tras 50 trades acumulados. Al disparar: `LiquidateInstrument` (si hay posiciÃ³n abierta en ese instante â€” defensivo, en la prÃ¡ctica el breach ocurre al cerrar), flag `degraded = true`, `RiskLimitBreachedEvent(StrategyDegradation)`, log `Critical`. U3/U4 no implementan `IRiskMonitor` ni activan kill switch global (ver ADR-023). Wiring en `TradingAlgorithmHost` reemplaza el `NullStrategyHealthMonitor` por el monitor real. **Tests:** ~28 nuevos (`StrategyHealthMonitorTests`: trade lifecycle, U1/U2/U3/U4, multi-estrategia, exclusiÃ³n, evento de breach; `StrategyHealthThresholdsTests`: 4 tests de validaciÃ³n y factory). Total Application.Tests: 121 tests verdes. Domain.Tests: 38 sin cambios. **Invariantes verificadas:** cero `using QuantConnect` en Domain/Application; cero `DateTime.UtcNow` en `Trading.Application/Health/`; cero `throw new Exception` ni `ApplicationException`; literales de POLICY solo en `StrategyHealthThresholds.cs`. **Backtest no-regresiÃ³n:** monitor activo pero `EmaCrossStrategy` no alcanza condiciones de disparo en el baseline (~225 Ã³rdenes esperadas, a verificar manualmente). ADR-023 documenta la decisiÃ³n de componente autÃ³nomo vs `IRiskMonitor`. OPS-3 (persistencia entre reinicios) agregada como deuda en Bloque 4 postergado. Bloque 3 completo; DEUDA-1/2/3 abiertas pero no bloquean Hito C.

### âœ… OPS-1 â€” Trading Policy Document (`POLICY.md`)
**Fecha:** 2026-05-21
**Resumen:** Documento operativo nuevo, versionado en la raÃ­z del repo junto a `AI.md`, `DECISIONS.md` y `ROADMAP.md`. Codifica las reglas operativas inquebrantables que gobiernan cuÃ¡ndo una estrategia o el sistema completo pierden el derecho de operar. **Estructura en 7 secciones**: (1) Principios operativos inquebrantables (5 principios: validaciÃ³n antes de capital, kill switch no se desactiva en caliente, haircut backtestâ†’live esperado 30-50%, gana el monitor cuando hay disenso con la intuiciÃ³n, cada cambio operativo deja huella); (2) umbrales a nivel sistema (drawdown global 25%, pÃ©rdidas consecutivas 5 trades con cooling off 24h, eventos macro Â±30min en pausa manual hasta `EventCalendarMonitor`, anomalÃ­as de infraestructura); (3) umbrales por estrategia (plantilla con U1-U4: DD absoluto desde ATH > 25%, DD rolling 30 dÃ­as > 15% sostenido 5 dÃ­as, PF rolling 30 trades < 1.0 sostenido 10 trades, expectancy rolling 30 trades < 0 sostenido 10 trades; U3 y U4 solo armados tras 50 trades en vivo); (4) cadencia de revisiÃ³n humana (diaria/semanal/mensual/trimestral); (5) runbooks de emergencia (kill switch activado, alerta de proceso caÃ­do, discrepancia con broker, performance anÃ³mala); (6) polÃ­tica de cambios al sistema en operaciÃ³n; (7) estado actual por estrategia (hoy solo `EmaCrossStrategy / BTCUSDT / 1h` en pre-paper). **Decisiones operativas tomadas y documentadas en ADR-022**: dos niveles de semÃ¡foro (OK/Apagar) en lugar de tres (Verde/Amarillo/Rojo/Negro); calibraciÃ³n absoluta de umbrales en lugar de derivada del backtest actual (porque el backtest se construyÃ³ para validar infraestructura, no como proceso de validaciÃ³n cuantitativa institucional); liquidaciÃ³n inmediata al disparar umbral en lugar de pause-only; reactivaciÃ³n con solo anÃ¡lisis escrito en `DECISIONS.md/incidents/` sin re-paper obligatorio. **RecalibraciÃ³n futura** de umbrales planificada para post-Hito G (cuando exista walk-forward analysis con base estadÃ­stica). **Cambios colaterales al ROADMAP**: OPS-2 actualizado con referencia explÃ­cita a las mÃ©tricas y umbrales de POLICY secciÃ³n 3; nueva entrada `EVCAL-1` (`EventCalendarMonitor`) agregada al Bloque 4 postergado con trigger documentado. **Sin cambios de cÃ³digo en este paso**: OPS-1 es 100% documental. El componente runtime que consume POLICY es OPS-2 (prÃ³ximo refactor del Bloque 3). Ver ADR-022.

---

## CÃ³mo usar este archivo

### Al iniciar una sesiÃ³n nueva (con Claude o solo):
1. Abrir `ROADMAP.md` y leer la secciÃ³n **"Refactors pendientes"**.
2. Confirmar cuÃ¡l es el prÃ³ximo refactor (el primero marcado ðŸ”„ o el primer â¬œ del bloque actual).
3. Leer `DECISIONS.md` para entender las decisiones arquitectÃ³nicas tomadas que afectan el refactor.
4. Leer `AI.md` para las reglas de estilo y arquitectura.

### Al completar un refactor:
1. Mover la fila del refactor a la secciÃ³n **"Historial completado"** con fecha y resumen.
2. Si surgieron decisiones arquitectÃ³nicas nuevas, agregarlas a `DECISIONS.md`.
3. Si una decisiÃ³n cambiÃ³ una regla del proyecto (ej. cambia el contrato de logging), actualizar `AI.md`.
4. Commitear los tres archivos junto con el cÃ³digo del refactor.

### Si un refactor se aborta:
1. Marcarlo como âŒ.
2. Agregar nota con la razÃ³n.
3. Si la decisiÃ³n amerita registro, agregar entrada en `DECISIONS.md`.

