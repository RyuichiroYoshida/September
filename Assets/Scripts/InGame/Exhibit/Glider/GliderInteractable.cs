using InGame.Interact;
using September.Common;
using September.InGame.Exhibit;
using UnityEngine;

namespace September
{
	public class GliderInteractable : ProjectileInteractableBase
	{
		protected override void CheckInteractEnd(PlayerInput input)
		{
			if(!HasStateAuthority) return; 
			if(_usingPlayer == null) return;
			base.CheckInteractEnd(input);
			if (_move is GliderMove move && move.IsFinished) InteractEnd();
		}
	}
}