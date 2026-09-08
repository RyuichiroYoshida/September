using InGame.Interact;
using September.InGame.Exhibit;
using UnityEngine;

namespace September
{
	public class GliderInteractable : ProjectileInteractableBase
	{
		public override void FixedUpdateNetwork()
		{
			base.FixedUpdateNetwork();
			if (CurrentUsePlayerRef.IsNone) return;
			if(!HasStateAuthority) return; 
			if (_move is GliderMove move && move.IsFinished) InteractEnd();
		}
	}
}