using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class NoticeManager : NetworkBehaviour
{
   Text _text;
   public override void Spawned()
   {
      _text = GetComponent<Text>();
      _text.text = "";
   }

   public void UpdateNoticeText()
   {
      _text.text = "鬼が変更されました";
   }

   public void HideNoticeText()
   {
      _text.text = "";
   }
   
}
