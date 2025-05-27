using System.Collections.Generic;
using Fusion;
using September.Common;

namespace InGame.Player.Ability
{
    public interface IAbilityExecutor
    {
        public Dictionary<PlayerRef, List<AbilityRuntimeInfo>> PlayerActiveAbilityInfo { get; }
        void RequestAbilityExecution(AbilityContext context);
    }
}
