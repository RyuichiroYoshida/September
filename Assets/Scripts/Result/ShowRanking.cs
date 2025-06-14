using September.InGame.Common;
using TMPro;
using UnityEngine;

public class ShowRanking : MonoBehaviour
{
   　TextMeshProUGUI _text;
    private string _ranking;
    void Start()
    {
        _text = GetComponent<TextMeshProUGUI>();
        int count = 0;
        var rankingData = InGameManager.Scores;
        foreach (var item in rankingData)
        {
            count++;
            _text.text += $"{count}位：{item.playerName} 　{item.score}点 \r\n";
        }
    }
}
