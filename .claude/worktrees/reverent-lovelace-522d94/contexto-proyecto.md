Trading.Domain.csproj
+---Interfaces
¦       IStrategy.cs
¦       IStrategyConfigLoader.cs
¦
+---Models
¦       MarketData.cs
¦       RootConfig.cs
¦       StrategyDefinition.cs
¦       TimeframeNode.cs

Trading.Strategies.csproj
¦   Estratega.cs
¦   strategies.json
¦   Trading.Strategies.csproj
¦
+---Estrategia
¦       EmaCrossStrategy.cs
¦
+---Infrastructure
¦       ConsolidatedBarHandler.cs
¦       KillSwitchManager.cs
¦       OrderEventHandler.cs
¦       PositionSizer.cs
¦       StrategyConfigLoader.cs
¦       StrategyExecutor.cs
¦       StrategyFactory.cs
¦       TimeframeHelper.cs