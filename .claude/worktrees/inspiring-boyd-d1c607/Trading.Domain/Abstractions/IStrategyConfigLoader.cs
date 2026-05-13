using Trading.Domain.Models;

namespace Trading.Domain.Abstractions
{
    public interface IStrategyConfigLoader
    {
        RootConfig Load(string path);
    }
}
