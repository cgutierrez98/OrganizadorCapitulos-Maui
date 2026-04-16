using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class RenameStrategyFactory
    {
        public IRenameStrategy CreateStrategy(Core.Enums.RenameMode mode)
        {
            return mode == Core.Enums.RenameMode.Maintain ? (IRenameStrategy)new MaintainStrategy() : new ChangeStrategy();
        }
    }
}
