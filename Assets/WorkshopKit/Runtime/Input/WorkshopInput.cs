using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityWorkshop
{
    /// <summary>
    /// Keyboard and mouse actions shared by every template. Created by the scene's Game Setup and
    /// disposed with it, so actions never leak between play sessions or scenes.
    /// </summary>
    public sealed class WorkshopInput : IDisposable
    {
        readonly InputAction m_Move;
        readonly InputAction m_Look;
        readonly InputAction m_Jump;
        readonly InputAction m_Interact;
        readonly InputAction m_Pause;
        bool m_Disposed;

        public WorkshopInput()
        {
            m_Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            m_Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            m_Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            m_Look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            m_Jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            m_Interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");

            m_Pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            m_Pause.AddBinding("<Keyboard>/p");

            m_Move.Enable();
            m_Look.Enable();
            m_Jump.Enable();
            m_Interact.Enable();
            m_Pause.Enable();
        }

        public Vector2 Move => m_Disposed ? Vector2.zero : Vector2.ClampMagnitude(m_Move.ReadValue<Vector2>(), 1f);
        public Vector2 Look => m_Disposed ? Vector2.zero : m_Look.ReadValue<Vector2>();
        public bool JumpPressed => !m_Disposed && m_Jump.WasPressedThisFrame();
        public bool InteractPressed => !m_Disposed && m_Interact.WasPressedThisFrame();
        public bool PausePressed => !m_Disposed && m_Pause.WasPressedThisFrame();

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            m_Move.Dispose();
            m_Look.Dispose();
            m_Jump.Dispose();
            m_Interact.Dispose();
            m_Pause.Dispose();
        }
    }
}
