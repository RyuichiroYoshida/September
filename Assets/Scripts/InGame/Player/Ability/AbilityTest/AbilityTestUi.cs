using System.Text;
using Fusion;
using InGame.Player.Ability;
using September.Common;
using UnityEngine;
using UnityEngine.UI;

public class AbilityTestUi : MonoBehaviour
{
    [SerializeField] private Text _playersAbilityText;
    private readonly StringBuilder _stringBuilder = new StringBuilder();

    private void Update()
    {
        _stringBuilder.Clear();
        var abilityExecutor = StaticServiceLocator.Instance.Get<IAbilityExecutor>();
        if (abilityExecutor == null) return;
        var info = abilityExecutor.PlayerActiveAbilityInfo;
        _stringBuilder.AppendLine("Players Ability Info");
        foreach (var playerRef in info.Keys)
        {
            _stringBuilder.AppendLine($"Player {playerRef}");
            foreach (var ability in info[playerRef])
            {
                var abilityName = ability.Instance.AbilityName.ToString();
                var time = ability.Instance.CurrentCooldown;
                var maxTime = ability.Instance.Cooldown;
                _stringBuilder.AppendLine($"Ability: {abilityName} Cooldown: {time:F1}/{maxTime:F1}");
            }
        }
        _playersAbilityText.text = _stringBuilder.ToString();
    }
}
