using Fusion;
using UnityEngine;

namespace InGame.Interact
{
    public interface IInteractable
    {
        void Interact(IInteractableContext context);
    }

    public interface IInteractableContext
    {
        int Interactor { get; }
        Vector3 WorldPosition { get; }
    }

    public class InteractableContext : IInteractableContext
    {
        public int Interactor { get; set; }
        public Vector3 WorldPosition { get; set; }
    }
}