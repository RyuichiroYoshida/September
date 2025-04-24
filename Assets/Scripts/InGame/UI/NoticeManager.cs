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

   public void UpdateNoticeText(string nickName)
   {
      _text.text = $"鬼がPLayer{nickName}に変更されました";
   }

   public void HideNoticeText()
   {
      _text.text = "";
   }
   
}
