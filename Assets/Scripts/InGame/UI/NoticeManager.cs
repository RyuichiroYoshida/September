using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class NoticeManager : NetworkBehaviour
{
   Text _text;
   public override void Spawned()
   {
      _text = GetComponent<Text>();
   }

   public void UpdateNoticeText(int playerID)
   {
      _text.text = $"鬼がPLayer{playerID}に変更されました";
   }

   public void HideNoticeText()
   {
      _text.text = "";
   }
   
}
