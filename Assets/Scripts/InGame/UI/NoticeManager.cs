using System;
using Cysharp.Threading.Tasks;
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

   public async void UpdateNoticeText()
   {
      _text.text = "鬼が変更されました";
      await UniTask.Delay(TimeSpan.FromSeconds(3));
      _text.text = "";
   }

   public void HideNoticeText()
   {
      _text.text = "";
   }
   
}
