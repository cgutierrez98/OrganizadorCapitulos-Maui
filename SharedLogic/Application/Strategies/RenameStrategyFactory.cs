using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class RenameStrategyFactory
    {
        public IRenameStrategy CreateStrategy(Core.Enums.RenameMode mode)
        {
            return mode switch
            {
                Core.Enums.RenameMode.Maintain => new MaintainStrategy(),
                Core.Enums.RenameMode.Change => new ChangeStrategy(),
                Core.Enums.RenameMode.Movie => new MovieStrategy(),
                _ => new ChangeStrategy()
            };
        }
    }
}
