using UnityEngine;

public class RankingDataHolder : MonoBehaviour
{
    public static RankingDataHolder Instance { get; private set; }

    public string[] Names { get; private set; }
    public int[] Scores { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetData(string[] names, int[] scores)
    {
        Names = names;
        Scores = scores;
    }

    public void Clear()
    {
        Names = null;
        Scores = null;
    }
}
