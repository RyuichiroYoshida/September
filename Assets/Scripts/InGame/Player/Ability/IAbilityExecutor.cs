using System.Collections.Generic;
using Fusion;
using September.Common;

namespace InGame.Player.Ability
{
    public interface IAbilityExecutor
    {
        public Dictionary<int, List<AbilityRuntimeInfo>> PlayerActiveAbilityInfo { get; }
        void ApplyAbilityState(AbilitySharedState abilitySharedState);
        void RequestAbilityExecution(AbilityContext context);
    }
}
