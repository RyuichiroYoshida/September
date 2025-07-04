using UnityEngine;

namespace September.Common
{
    public static class NickNameProvider
    {
        const string NickNameKey = "NickName";
        
        public static string GetNickName()
        {
            if (PlayerPrefs.HasKey(NickNameKey))
            {
                return PlayerPrefs.GetString(NickNameKey);
            }
            else
            {
                return string.Empty;
            }
        }

        public static void SetNickName(string nickName)
        {
            if (nickName == string.Empty) return;
            PlayerPrefs.SetString(NickNameKey, nickName);
        }
    }
}