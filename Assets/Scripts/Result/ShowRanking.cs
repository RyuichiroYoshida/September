using System;
using Fusion;
using September.InGame.Common;
using TMPro;
using UnityEngine;

public class ShowRanking : MonoBehaviour
{
   　TextMeshProUGUI _text;
    private void Start()
    {
        string result = null;
        _text = GetComponent<TextMeshProUGUI>();
        var names = RankingDataHolder.Instance.Names;
        var scores = RankingDataHolder.Instance.Scores;
        for (int i = 0; i < names.Length; i++)
        {
            if (i == names.Length - 1)
            {
                result += $"鬼:{names[i]} 得点:{scores[i]}点 \r\n";
            }
            else
            {
                result += $"{i + 1}位:{names[i]} 得点:{scores[i]}点 \r\n";
            }
        }
        _text.text = result;
    }

   
}
