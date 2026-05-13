public abstract class StrategyInstance
{
    protected StrategyDefinition Config;

    public void Initialize(StrategyDefinition config)
    {
        Config = config;
        OnInitialize();
    }

    protected abstract void OnInitialize();

    public abstract void OnData(Slice data);

    protected bool ShouldExitByTime(int barsOpen)
    {
        return Config.CombineWithTimeExit && barsOpen >= Config.MaxBars;
    }
}

public class StrategyFactory
{
    public StrategyInstance Create(StrategyDefinition def)
    {
        switch (def.StrategyName)
        {
            case "ScalpingEMA":
                return new ScalpingEmaStrategy();

            case "MeanReversion":
                return new MeanReversionStrategy();

            default:
                throw new Exception($"Strategy no registrada: {def.StrategyName}");
        }
    }
}

public class TimeframeManager
{
    public Resolution Resolution { get; private set; }

    private List<StrategyInstance> _strategies = new();

    public TimeframeManager(Resolution resolution)
    {
        Resolution = resolution;
    }

    public void AddStrategy(StrategyInstance strategy)
    {
        _strategies.Add(strategy);
    }

    public void OnData(Slice data)
    {
        foreach (var strat in _strategies)
        {
            strat.OnData(data);
        }
    }
}

public class BaseStrategy : QCAlgorithm
{
    private Dictionary<string, TimeframeManager> _timeframes;
    private StrategyFactory _factory;
    private RootConfig _config;

    public override void Initialize()
    {
        SetStartDate(2022, 1, 1);
        SetCash(10000);

        _factory = new StrategyFactory();
        _timeframes = new Dictionary<string, TimeframeManager>();

        LoadConfig();
        ConfigureTimeframes();
        ConfigureStrategies();
    }
	
	private void LoadConfig()
{
    var loader = new StrategyConfigLoader();

    string path = @"config.json";

    _config = loader.Load(path);
}

private void ConfigureTimeframes()
{
    foreach (var kvp in _config.Timeframes)
    {
        string tf = kvp.Key;

        Resolution resolution = MapResolution(tf);

        _timeframes[tf] = new TimeframeManager(resolution);

        AddCryptoFuture("BTCUSDT", resolution); // simplificado
    }
}

private Resolution MapResolution(string tf)
{
    return tf switch
    {
        "1m" => Resolution.Minute,
        "5m" => Resolution.Minute,
        "15m" => Resolution.Minute,
        "30m" => Resolution.Minute,
        "1h" => Resolution.Hour,
        "4h" => Resolution.Hour,
        "1d" => Resolution.Daily,
        _ => throw new Exception("TF no soportado")
    };
}

private void ConfigureStrategies()
{
    foreach (var kvp in _config.Timeframes)
    {
        string tf = kvp.Key;
        var node = kvp.Value;

        var tfManager = _timeframes[tf];

        foreach (var def in node.Strategies)
        {
            var strategy = _factory.Create(def);

            strategy.Initialize(def);

            tfManager.AddStrategy(strategy);
        }
    }
}


