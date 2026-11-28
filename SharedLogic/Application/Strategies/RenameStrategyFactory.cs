using organizadorCapitulos.Core.Entities;
using organizadorCapitulos.Core.Interfaces.Strategies;

namespace organizadorCapitulos.Application.Strategies
{
    public class RenameStrategyFactory
    {
        private static readonly IRenameStrategy Maintain = new MaintainStrategy();
        private static readonly IRenameStrategy Change = new ChangeStrategy();
        private static readonly IRenameStrategy Movie = new MovieStrategy();

        public IRenameStrategy CreateStrategy(Core.Enums.RenameMode mode)
        {
            return mode switch
            {
                Core.Enums.RenameMode.Maintain => Maintain,
                Core.Enums.RenameMode.Change => Change,
                Core.Enums.RenameMode.Movie => Movie,
                _ => Change
            };
        }
    }
}
