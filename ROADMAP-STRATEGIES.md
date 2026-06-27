# ROADMAP-STRATEGIES — Investigación y desarrollo de estrategias

> **Estado de este documento:** BORRADOR sujeto a revisión y validación. No commiteado.
>
> **Propósito:** mantener visibilidad, entre sesiones, de dos cosas que el track de ingeniería (`ROADMAP.md`) no cubre:
> 1. **Cómo se construye la cartera** — la arquitectura de descorrelación y de riesgo que decide *cómo conviven* las estrategias (Parte I).
> 2. **Qué edges buscamos y cómo los validamos con honestidad** — el ciclo de vida de una hipótesis (Parte II).
>
> Es complementario —no sustituto— de `ROADMAP.md` (el track de "qué máquina construimos para testear"), de `DECISIONS.md` (los ADR) y de `POLICY.md` (las reglas operativas en vivo).
>
> **Regla de oro:** ninguna estrategia se construye sin una **Fase 0 escrita y aprobada** primero. Una idea sin mecanismo económico no entra. Aplica incluso a estrategias que "todo el mundo sabe que funcionan".
>
> **Convenciones de estado:** ✅ aprobada/completada · 🔄 en curso · ⬜ pendiente · ❌ descartada (anotar razón).

---

## 0. Relación entre los dos roadmaps

Los dos tracks no son independientes: el de ingeniería **es la implementación** de las fases de validación del ciclo de vida de una estrategia. No hay que construir maquinaria nueva para validar; hay que usar la que ya existe.

| Fase del ciclo de estrategia | ¿Dónde vive en `ROADMAP.md`? |
|---|---|
| Fase 0 — Portón económico | **Este documento** (no existe en el track de ingeniería) |
| Fase 1 — Higiene de datos | Data layer + regla anti look-ahead/leakage de `AI.md` |
| Fase 2 — Presupuesto de búsqueda | Disciplina de conteo de intentos (transversal) + Hito H |
| Fase 3 — Validación temporal | **Hito G** (walk-forward) + Hito H (purged k-fold) |
| Fase 4 — Robustez por régimen | Hito G + métricas estratificadas por régimen (Hito B) |
| Fase 5 — Realismo de producción | Hito D-prev (costos/funding reales contra broker) |
| Fase 6 — Capa de portafolio | **Parte I de este documento** + `CompatibleRegimes` + balance L/S |
| Fase 7 — Gobernanza | `POLICY.md` + `StrategyHealthMonitor` (U1-U4) + Hito C/D |

**Implicancia:** ya está implementada casi toda la maquinaria de validación. Lo que falta es el track de *hipótesis* y de *arquitectura de cartera* —este documento— que decide **qué** alimentar a esa maquinaria y **cómo** ensamblar lo que sobrevive.

---

# PARTE I — Arquitectura de cartera

> Esta parte es el "cómo conviven las estrategias". Es la que da el activo durable: un portafolio descorrelacionado. Ninguna estrategia individual es el objetivo.

## I.1 — Objetivo y filosofía

1. **El objetivo es la cartera, no la estrategia campeona.** Coherente con ADR-055: el éxito de una estrategia se mide por su **contribución a la cartera**, no por su Sharpe standalone. Esperar Sharpe-3 individual es la señal más confiable de sobreajuste.

2. **La descorrelación se busca por MECANISMO, no por activo ni por marco temporal.** En cripto, distintos activos NO descorrelacionan: BTC/ETH/alts corren juntos en régimen normal y **convergen a correlación 1 en los crashes**, justo cuando la diversificación tiene que pagar. Distintos timeframes sobre el mismo activo tampoco descorrelacionan (comparten signo direccional). Lo que descorrelaciona de verdad es el **motor de retorno**: tendencia direccional vs. reversión vs. carry. (Ver I.2.)

3. **La descorrelación es estadística, sobre la curva de capital — no es un hedge trade-a-trade.** Dos jugadas opuestas no "se cubren" posición contra posición en tiempo real (eso depende de que ambas existan simultáneamente, lo cual no controlamos). Lo que ocurre es que, sobre cientos de trades, sus curvas de ganancia van a contramano y **se suavizan entre sí**. La protección real contra los ratos "descubiertos" no es tapar cada posición, sino que **ninguna posición sola pueda lastimar mucho** (ver I.4, nivel "por trade").

4. **Muchos activos dan capacidad de ELEGIR, no de diversificar.** Como los activos cripto están correlacionados, correr una misma jugada sobre 15 monedas no son 15 apuestas: es la misma apuesta repetida. El valor de un universo amplio es de **selección** —entre los que firman señal, quedarse con la expresión más limpia (2-3)— no de diversificación. **Se escanea ancho, se sostiene poco.** El límite se pone como presupuesto de riesgo por jugada (ver I.4), no como conteo de posiciones.

## I.2 — Los ejes de la cartera (las "jugadas")

Tres motores de retorno que ganan en momentos distintos. Realistas para nuestro caso (Binance, datos de microestructura ya conectados, limitación estructural de costos conocida).

| Eje | A qué le apuesta | Gana cuando el mercado… | Costo de mantener | Dirección |
|---|---|---|---|---|
| **1 · Tendencia** | a que un movimiento sostenido continúa | se mueve fuerte y sostenido | bajo (opera poco) | direccional (sesgo long en cripto) |
| **2 · Carry de funding** | a cobrar el "alquiler" del apalancamiento (long spot + short perp del **mismo** activo) | está caliente/apalancado, en cualquier dirección | muy bajo | **neutral por construcción** |
| **3 · Reversión microestructural** | a que una sobrerreacción de corto plazo vuelve a su lugar (nicho OFI/CVD ya instrumentado) | oscila en rango (lo *opuesto* a tendencia) | alto ⚠️ (sensible a costos) | long o short |

**Ortogonalidad:** la 1 y la 3 son opuestas naturales (tendencia vs. rango); la 2 corre por un carril aparte (no le importa la dirección). Eso es descorrelación real.

**Notas de diseño:**
- El **carry** es neutral *por construcción* (long y short son el mismo activo, se cancelan exacto). Era el mejor diversificador conceptual ("el arquero"). **⚠️ Descartado en Fase 0 (ver §III): research preliminar indica que hoy no tiene edge neto por costos** — es el edge más crowdeado y se arbitró (Principio 8). Queda como hueco: la cartera pierde su único carril neutral y barato.
- La **reversión** es la más sensible a costos (ya visto en research): hay que diseñarla para operar poco y selectivo, o los costos se la comen.
- Empezar con 2-3 ejes es suficiente. Agregar más ahora sería sobre-ingeniería.

**Lo que queda afuera a propósito** (la limitación de costos ya los hace inviables): cross-sectional entre muchas monedas (cerrado en research por costos), market-making, cualquier ultra-alta frecuencia.

## I.3 — Sesgo direccional de la cartera

**Decisión: sesgo long moderado.** No neutral puro. Se admite una exposición neta long para capturar la deriva alcista de fondo del mercado cripto; las jugadas modulan alrededor de ese centro.

- El sesgo long es una **decisión de riesgo/retorno consciente**, no un default. Trae pegado "más dolor en los crashes".
- **Un kill switch no compra sesgo long gratis.** Es un cinturón de seguridad (reactivo, binario, cubre la catástrofe), no un volante. El costo cotidiano del sesgo long son los drawdowns normales, que ocurren *por debajo* del umbral del switch. El sesgo se dimensiona por cuánto neto se aguanta en un crash *antes* de que actúe ninguna red — no por "lo dejo alto porque el switch me salva".
- Esto coincide con la práctica institucional documentada para cripto: sesgo a largo, cortos pocos y de mecanismo distinto.

## I.4 — Arquitectura de riesgo (tres niveles)

El control de riesgo opera en tres niveles independientes. El error es intentar hacer los tres con un solo mecanismo (p. ej. contar y aparear posiciones); separados, cada uno se resuelve limpio.

| Nivel | Qué controla | Para qué |
|---|---|---|
| **Por trade** | cuánto puede perder *una* posición sola (tamaño × distancia al stop) | que ninguna te rompa; es la red de seguridad real |
| **Por jugada (sleeve)** | el riesgo total que puede tomar cada eje, sin importar cuántas señales firmen | que una jugada no se coma a las otras |
| **Por book (neto)** | la exposición neta long vs. short del conjunto | que la cartera no sea una apuesta direccional escondida |

**Principios transversales:**

- **El riesgo se mide por exposición, no por conteo.** Contar posiciones no mide ni el tamaño ni la dirección: dos posiciones "balanceadas" por conteo pueden ser dos longs (doble apuesta) o estar neutrales. El número que importa es cuánto, en plata y dirección, está apostado.
- **Ajuste por beta/volatilidad.** Un dólar en una alt salvaje no pesa igual que un dólar en BTC. La exposición neta se ajusta por cuánto se sacude cada activo, o el neto miente.
- **Separar la decisión de alfa de la decisión de exposición.** Son dos decisiones distintas y conviene no fusionarlas:
  1. **Decisión de alfa:** se toma la señal *cuando aparece*, sin pedirle permiso al balance. Una buena señal no se rechaza por estética del book.
  2. **Decisión de exposición:** por separado, se mira el neto del book entero. Si se va de la banda, *recién ahí* se actúa.
  Fusionarlas (p. ej. "solo abro X si tengo Y para compensar") contamina el alfa: terminás operando para cumplir el balance en vez de para ganar, y sesgás los sleeves (p. ej. una reversión que solo puede ir short pierde la mitad de su edge).

## I.5 — El termostato de exposición neta

Es la herramienta del nivel "por book". **No persigue neto cero** (eso obliga a forzar trades); define una **banda de comodidad** centrada en el sesgo long moderado elegido (I.3).

- Mientras el neto esté **dentro** de la banda → se opera libre, se toma cada señal.
- Cuando una señal nueva **sacaría** el neto de la banda → recién ahí se interviene: se achica, se saltea, o se pone un contrapeso barato a nivel del total.

Ejemplo (exposición ajustada por beta, no notional crudo): tendencia long BTC = +100; reversión short ETH = −60; carry = 0 (neutral). **Neto = +40** (levemente long), **bruto = 160**. Si la banda es, digamos, centrada en +40 con tolerancia, una entrada que llevara el neto fuera de la banda se modula; las demás pasan.

## I.6 — Criterio de entrada: contribución marginal

Reemplaza al gate M4 *standalone* como criterio de admisión de una estrategia a la cartera. La pregunta correcta NO es "¿qué tan buena es esta estrategia sola?" sino:

> **Sumando esta estrategia a las que YA tengo, ¿la cartera completa mejora?**

- Se mide por **contribución marginal** al Sharpe de la cartera y por **correlación (incluida correlación de cola / en drawdown)** contra el book existente.
- Una estrategia mediocre que gana cuando las demás pierden vale más que una brillante que gana en los mismos momentos (redundante).
- Esto es práctica institucional estándar (risk budgeting, contribución marginal al riesgo). *(Pendiente de bajar a métrica concreta y umbral — ver I.7.)*

## I.7 — Dos ejes de descorrelación, complementarios

La cartera descorrelaciona en **dos ejes ortogonales**; hacen falta los dos.

| Herramienta | Eje | Qué resuelve | Su agujero (que cubre la otra) |
|---|---|---|---|
| **Gating por régimen** (`CompatibleRegimes` / HMM) | tiempo | cada estrategia opera donde tiene edge y se calla donde no → suaviza la curva a lo largo del ciclo | no acota la concentración **instantánea** |
| **Termostato de exposición neta** (I.5) | instante | acota cuánto neto se apuesta en cada momento | no decide si las jugadas se solapan en el tiempo |

**El detalle que los une:** el gating por régimen puede *concentrar*. Cuando el régimen Trend está activo, lo único que habla es tendencia → en ese momento el book es todo long-tendencia. El régimen da descorrelación sobre el ciclo, pero en ese punto del tiempo estás concentrado — y eso es exactamente lo que agarra el termostato neto. Uno decide *quién habla*; el otro, *cuánto neto* cuando habla.

> El gating por régimen **no es un almuerzo gratis** — es un modelo con sus propios modos de falla (mala clasificación, retardo en detectar el cambio). Ver la metodología de uso correcto en II.4.

**Pendientes de la Parte I (para cerrar en sesión):**
- ⬜ Bajar "contribución marginal" a métrica concreta y umbral de admisión.
- ⬜ Fijar la banda del termostato (centro y tolerancia) en términos de exposición ajustada por beta.
- ⬜ Definir el contrapeso barato a nivel agregado (¿short de índice? ¿reducción de tamaño?).

---

# PARTE II — Proceso de research (ciclo de vida de una hipótesis)

## II.1 — Principios de research

1. **Mecanismo antes que indicador.** Una estrategia se elige por su *por qué* económico (prima de riesgo, sesgo conductual, restricción estructural, fricción de microestructura), no por el indicador que la implementa. Si no podés nombrar el mecanismo, no hay estrategia.
2. **Re-correr la Fase 0 al cambiar de mercado.** Que un mecanismo funcione en equities no implica que funcione en cripto (ej.: BAB depende de restricción de apalancamiento, que cripto disuelve).
3. **Independencia de mecanismo entre estrategias.** Dos estrategias del portafolio deben explotar causas distintas. **La pata corta de una estrategia pareada NO es una estrategia corta independiente.**
4. **Descorrelación por régimen, no solo por dirección.** Vía `CompatibleRegimes` (HMM, Hito B), cada estrategia opera donde tiene edge y se calla donde no. Es un eje de descorrelación en el *tiempo*, complementario del termostato de exposición (ver I.7). **No reemplaza** el control de exposición neta.
5. **El mecanismo debe predecir cuándo falla.** Una hipótesis que no dice en qué condiciones *debería* dejar de funcionar es una racionalización a posteriori, no una hipótesis.
6. **La Fase 0 es necesaria pero débil.** Un "por qué" plausible es barato; se inventa para cualquier correlación. La Fase 0 dice *dónde* buscar; la corrección por número de pruebas (Fase 2) dice *si lo que encontraste es real*. Las dos, o ninguna.
7. **"Oro" se define honesto.** El activo durable es el *proceso* y el *portafolio descorrelacionado*, no la estrategia campeona.
8. **El edge accesible se erosiona.** Lo que una fábrica retail encuentra fácil, lo encuentran muchos. La vida útil de un edge accesible es limitada; se monitorea su decay (Fase 5).
9. **Pulso propio sin filtros.** Si quitarle un filtro (régimen/HMM, u otro) mata la estrategia, entonces el filtro *era* la estrategia: es un indicador haciéndose pasar por mecanismo. Un edge real respira sin el filtro; el filtro lo pule, no lo inventa. (Ver II.4.)

## II.2 — Ciclo de vida de una estrategia (el portón de 8 fases)

Proceso que toda estrategia recorre. **Es también lo que la fábrica del futuro (Hitos F-H) automatizará** —pero recién después de recorrerlo a mano dos o tres veces.

### Fase 0 — Portón económico *(antes de tocar datos)*
- Hipótesis económica escrita: ¿por qué *debería* existir el edge?
- Pre-registro: dirección, signo, mercados, horizonte, mecanismo —antes de ver resultados.
- Criterios de muerte definidos por adelantado: qué métrica, qué umbral, cuánto tiempo.
- **Predicción de falla:** ¿en qué régimen/condición este mecanismo debería dejar de funcionar?
- **Decisión de gating por régimen:** si el mecanismo predice dependencia de régimen, el gating es parte de la hipótesis *desde acá* (ver II.4).

### Fase 1 — Higiene de datos
- Datos point-in-time, sin survivorship bias (incluir coins deslistados), sin look-ahead.
- Costos realistas desde el primer backtest: comisiones, slippage, impacto; borrow y funding para cortos.
- Partición train / validation / **bóveda** (holdout que se toca una sola vez).

### Fase 2 — Presupuesto de búsqueda
- Contar **cada** intento (cada parámetro, cada variante —incluido con/sin HMM—, cada descarte). N es input de la estadística.
- Deflated Sharpe Ratio (ajusta por N, skew, kurtosis, largo de muestra).
- Probability of Backtest Overfitting (PBO) vía CSCV.

### Fase 3 — Validación temporal *(→ Hito G/H)*
- Combinatorial purged cross-validation con embargo y purging.
- Walk-forward con ventanas deslizantes.
- **Cualquier selección de variante (p. ej. HMM sí/no) se hace dentro del in-sample / dentro de cada fold, nunca comparando resultados OOS** (ver II.4).

### Fase 4 — Robustez *(→ Hito G + Hito B)*
- Mesetas de parámetros, no picos.
- Estabilidad por régimen y por subperíodo (incluida crisis).
- Montecarlo (reordenamiento/remuestreo) para acotar el rango de desempeño esperable.

### Fase 5 — Realismo de producción *(→ Hito D-prev)*
- Estimación de capacidad (¿cuánto capital aguanta antes de que el impacto coma el edge?).
- Monitoreo de crowding y alpha decay.

### Fase 6 — Capa de portafolio *(→ Parte I)*
- Tratar las estrategias como candidatas correlacionadas: agrupar por correlación antes de asignar.
- Admisión por **contribución marginal** (I.6), no por Sharpe standalone.
- Encaje en la **arquitectura de riesgo de tres niveles** (I.4), el **termostato de exposición neta** (I.5) y el **sesgo long moderado** (I.3).

### Fase 7 — Gobernanza *(→ POLICY + Hito C/D)*
- `StrategyHealthMonitor` (U1-U4) vigila la estrategia en vivo.
- Paper (Hito C) → live chico (Hito D). El backtest nunca es la última validación.

**Puerta dura:** ninguna estrategia opera capital real sin walk-forward aprobado en Hito G (= Fase 3-4). Coincide con `POLICY.md` P1 y 7.1.

## II.3 — Orden de trabajo: edge primero, infra a demanda

- Se busca el **edge** primero; la infraestructura de cartera (termostato, métricas de contribución marginal) se construye **a demanda**, recién cuando hay edges validados que la justifiquen. No se construye la estantería antes de tener productos.
- El plan de cartera (Parte I) es un **documento vivo**: se reescribe a medida que sobreviven edges. No es un blueprint congelado; es la función de fitness que gobierna la búsqueda.

## II.4 — Metodología del gating por régimen (HMM): cómo evaluarlo sin quemar el OOS

El gating por régimen es un eje de descorrelación valioso (I.7), pero **es un modelo con modos de falla** y hay evidencia de que puede degradar una señal (eje-2: "OFI Contrarian + régimen HMM — NO-GO"; n=1, no determinante, pero suficiente para no darlo por gratis). Reglas para usarlo con honestidad estadística:

**Regla central:** el HMM es una perilla; el OOS es para **medir**, no para **girar perillas**. En el momento en que el OOS/bóveda decide "HMM sí/no" por desempeño, deja de ser fuera de muestra y se quema. Además, "elegir el mejor de dos" es un intento más de búsqueda que hay que pagar en el Deflated Sharpe (Fase 2).

**Cómo se decide el HMM (en orden de preferencia):**
- **A — Por mecanismo, en Fase 0 (preferible).** Si la hipótesis económica dice "esto solo debería funcionar en régimen X", el gating es parte de la hipótesis desde el arranque; se valida *esa* versión y no se prueban dos para elegir.
- **B — Anidado en los folds.** Si se quiere que los datos decidan HMM-sí/no, la elección vive **dentro de cada ventana de entrenamiento** del walk-forward, nunca comparando los OOS finales. El OOS solo juzga la receta completa, HMM incluido.

**Probar con y sin HMM desde el principio: está BIEN**, con dos condiciones y un bonus:
1. **Contar las dos como N=2** y pagarlo en Deflated Sharpe/PBO. Cuidado con la pendiente resbaladiza (declarar la grilla completa por adelantado, no ir agregando variantes).
2. **No elegir la ganadora mirando el OOS/bóveda** (usar A o B; la bóveda confirma *una sola*, ya elegida).
3. **Bonus — es un diagnóstico de salud:** ¿la estrategia tiene pulso *sin* HMM?
   - Base con pulso + HMM la refina (sube Sharpe, baja drawdown) → sano.
   - Base muerta y solo vive con HMM → **alarma roja**: el filtro probablemente fabrica el edge ajustando las etiquetas de régimen al período de prueba, no lo refina (ver Principio 9).

---

## III — Registro y secuencia de estrategias

| ID | Estrategia | Mecanismo (Fase 0) | Eje (I.2) | Régimen | Estado | Notas |
|---|---|---|---|---|---|---|
| S1 | Trend-following / TS-Momentum | Subreacción / continuación (conductual) | 1 · Tendencia | Trend | ❌ Descartada (Capa A / TS10, 2026-06-27) | Trend ungated sin edge robusto (edge IS casi puro SOL, 0/11 OOS); TS10 (gate HMM {Trend}) no rescata — inerte/degradante 0/3 brazos. Eje 1 (Tendencia) de la cartera queda vacío. |
| S2 | Carry de funding | Prima por financiamiento del apalancamiento | 2 · Carry | indiferente (neutral) | ❌ Descartada Fase 0 | Research preliminar: hoy sin edge neto por costos (edge crowdeado/arbitrado). Pendiente: confirmar contra estructura de costos propia antes de cerrar definitivamente. |
| S3 | Mean Reversion (microestructura, OFI/CVD) | Corrección de sobrerreacción en rango | 3 · Reversión | Squeeze | ❌ Descartada (Capa A, 2026-06-26) | Materializada como el **Eje 3 (sub-hora 5m/15m)**. El M4 que pasó (54/54) corría a **costo 0.0**; al re-evaluar con costos reales (0.12% RT) en la **Capa A** (ADR-056), **0/54 configs sobreviven** (Sharpe -4 a -44). Edge bruto real (~2-3 bps/barra) pero ~6x menor que los costos. Mismo muro estructural que ejes 1/1b. **Sin C# construido** — la Capa A la mató antes. |
| S4+ | *(abierto)* | A definir vía Fase 0 | A definir | A definir | ⬜ | Solo después de S1-S3 validadas a mano. |

**EmaCrossStrategy:** permanece como estrategia de desarrollo/infra (ejercita U1-U4, heartbeat, kill switch). No se valida ni opera live. No se entrelaza con la validación seria de S1.

> **Nota (muro de costos):** dos de los tres ejes están muertos por costos — carry (S2, crowdeado/arbitrado) y reversión de microestructura (S3, alta frecuencia). Sumado a los ejes 1/1b/2 del backlog de `ROADMAP.md`, la **reversión de microestructura de alta frecuencia lleva 4 rechazos por la misma causa estructural** (costo taker retail). Conclusión operativa: el perfil que sobrevive es **baja frecuencia** → S1 (tendencia) pasa a ser la prioridad. Queda además un hueco de mecanismo ortogonal neutral; problema abierto, no inmediato.

**Secuencia de trabajo:**
1. **[PASO INMEDIATO]** Cerrar la **Fase 0 de S1** (trend-following, §IV) y correrla por la **Capa A** (ADR-056), extendiendo la gramática del spec con los primitivos de tendencia. S3/eje 3 quedó ❌ por costos; S1 es el siguiente por su perfil de baja frecuencia.
2. Si S1 sobrevive la Capa A, recorre el resto del ciclo (Capa B / Lean → Hito G).
3. Con dos estrategias de mecanismo distinto validadas, recién entonces el Strategy Scaffolder (Hito F) sabe qué generalizar → nace la fábrica.

---

## IV — Fase 0 de S1 — Trend-following / Time-Series Momentum *(registro histórico)*

> Estado: ❌ CERRADA — NO-GO (TS10, 2026-06-27)

**Cierre (2026-06-27):** La Capa A evaluó 10 variantes de TS (TS1-TS10), 38 configs × 3 activos. Resultado: 0/11 configs que pasaron IS sobreviven OOS (Sharpes OOS tipicos -0.5 a -1.8). TS10 (gate HMM {Trend}) fue el ultimo intento: 0/3 brazos pasan el gate IS; el gate es inerte en BTC, degrada ETH y destruye SOL. La prediccion de mecanismo de §IV (trend rinde mejor en regimen Trend) queda falsada. Registro completo en `Trading.Research/strategy_experiments.md` (secciones S1 y TS10). **S1 no pasa a Capa B (C#).**

---

*(El contenido de diseño de Fase 0 se conserva abajo como registro histórico.)*

> **Por qué S1 ahora [registro]:** el muro de costos retail ya mató el carry y toda la reversión de microestructura de alta frecuencia (ejes 1/1b/3). El trend-following es de **baja frecuencia / bajo costo** — el perfil que sobrevive. Se valida por la **Capa A** (Python) primero, extendiendo la gramática del spec con los primitivos de tendencia.

- **Mercado / instrumento:** perpetuals de cripto líquido (BTC, ETH; universo a acotar). Datos: OHLCV, 100% accesibles a retail.
- **Mecanismo económico:** subreacción inicial a información seguida de continuación del precio (sentiment / under-reaction; De Long et al. sobre noise-trader risk). En cripto el mecanismo se *amplifica*: mercado dominado por retail y sentimiento. Soporte empírico: Liu & Tsyvinski (2018) y evidencia de driver por sobrerreacción.
- **Dirección / signo:** **sesgo a largo.** La pata corta del momentum en cripto se destruye con los saltos al alza; los estudios muestran que long-only supera a long-short. Cortos, si existen, mínimos y como protección, no como motor. (Coherente con I.3.)
- **Horizonte:** a definir (el momentum cripto cubre desde intradía hasta meses; elegir 1-2 horizontes, no barrer todos —eso mete look-ahead, ver Fase 2).
- **Régimen:** gateada a Trend vía `CompatibleRegimes`. **La inclusión del HMM se decide por mecanismo (II.4-A) o anidada en folds (II.4-B), nunca eligiendo por desempeño OOS.** Probar con/sin HMM desde el inicio como diagnóstico de pulso (II.4, bonus).
- **Predicción de falla (clave):** debe rendir mal o negativo en mercados bajistas/laterales sostenidos y cuando el régimen no es Trend. Si el backtest muestra que "gana en todos los regímenes", es señal de sobreajuste, no de robustez.
- **Criterios de muerte (pre-registro):** heredan U1-U4 de `POLICY.md` 3.1 como piso operativo; los umbrales de validación cuantitativa se fijan en Hito G.

**Pendiente para cerrar la Fase 0 de S1:**
- ⬜ Acotar universo de instrumentos y horizonte(s).
- ⬜ Definir señal concreta de entrada/salida (¿signo de retorno N-períodos? ¿breakout? ¿EMA?) y justificar por qué esa y no otra.
- ⬜ Escribir la predicción de falla en términos testeables contra el clasificador de régimen.

---

## V — Estado actual y próximo paso

- **Track de ingeniería:** Hito C (paper trading) en curso. Maquinaria de validación (Fases 1-7) mayormente implementada. **Capa A del pipeline (ADR-056) construida y operativa** (`Trading.Research/layer_a_validate.py`).
- **Track de estrategias:**
  - **S3 (mean-reversion microestructura OFI/CVD):** ❌ descartada por costos en la Capa A (2026-06-26). El M4 que pasó corría a costo 0.0; con costos reales, 0/54 configs sobreviven.
  - **S1 (trend-following):** ❌ descartada (Capa A / TS10, 2026-06-27). 10 variantes TS, 0/11 configs sobreviven OOS. Gate HMM {Trend} no rescata — inerte/degradante 0/3 brazos. Ver TS10 en strategy_experiments.md.
  - **S2 (carry de funding):** ❌ descartada en Fase 0 por costos.
  - **Eje volatilidad (eje 4 del backlog / Capa A):** ❌ NO-GO (2026-06-27). 3/3 hipotesis rechazadas en Capa A. H-V1: mecanismo falsado (spikes = crashes con continuacion, no capitulaciones). H-V2: sin meseta, sesgo de activo SOL, OOS colapsa. H-V3: deteriora OOS en 3/3. Bloqueo de venue (Binance sin opciones/variance swaps/indice de vol tradeable): el carril de vol neutral no existe. El hueco del carril neutral (carry) sigue abierto — la vol no lo lleno.
  - **Momentum cross-sectional / time-series — universo ancho (Tarea 3, ADR-055):** ❌ NO-GO Capa A (2026-06-27). Re-test sobre 196 perps USDT Binance, panel diario 2020-2026, point-in-time, costos escalonados (majors 0.12% / alts 0.22% RT). 1/8 configs pasa Sharpe IS >= 0.5 (marginal, 0.526). SIN meseta: Sharpe IS decae monotonamente con L. OOS colapsa en 8/8 configs (Sharpe −1.1 a −3.6). Falla de mecanismo: rankear por retorno bruto en universo correlacionado a BTC ≈ rankear por beta → alta-beta revierte. El sesgo residual del universo es OPTIMISTA (no incluye perps removidos pre-2021, sin impacto de mercado) → el NO-GO es robusto. Ver `Trading.Research/strategy_experiments.md`.
- **Arquitectura de cartera (Parte I):** decidida en lo conceptual. Pendientes de bajar a números: I.7. **Aviso:** con S1 ❌, eje vol ❌ y momentum ancho ❌, los tres ejes de la Parte I (trend/carry/reversión) siguen vacíos. El hueco de mecanismo neutral sigue abierto.
- **Inflexion (2026-06-27):** Con S1 ❌, eje vol ❌ y momentum ancho ❌, los tres ejes originales (trend/carry/reversion) y los brazos de universo ancho quedan vacios. **Hilo vivo restante senalado por el director (no condenado por los tests anteriores):** momentum residual / beta-neutral (remover factor mercado, rankear por residuo idiosincratico) — neutral por construccion, candidato al carril del carry que sigue hueco. Pendiente de decision de research-direction.

**Próximo paso:** Decision sobre momentum residual / beta-neutral (pendiente del director).

---

## Cómo usar este archivo

- Al evaluar cualquier idea de estrategia nueva: escribir su Fase 0 acá primero. Si no pasa el portón, no se construye.
- Al avanzar una estrategia de fase: actualizar su fila en §III y su estado.
- Cuando una estrategia se descarta: marcar ❌ con la razón (es información valiosa, no fracaso).
- Mantener sincronizado con `ROADMAP.md`: cuando una estrategia llega a una fase que vive en un hito de ingeniería, referenciar el hito.
- La Parte I (arquitectura de cartera) se revisa cuando entra o sale un sleeve, o cuando se baja un pendiente de I.7 a número.
