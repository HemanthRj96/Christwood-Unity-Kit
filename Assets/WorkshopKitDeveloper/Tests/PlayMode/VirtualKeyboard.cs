using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// A test keyboard that the game reads through its normal Input System actions. Headless runs have no
    /// focused Game view, which the Input System treats as "ignore devices", so this swaps in temporary
    /// settings that accept unfocused input and restores the project's settings on dispose.
    /// </summary>
    public sealed class VirtualKeyboard : IDisposable
    {
        readonly InputSettings m_ProjectSettings;
        readonly InputSettings m_TestSettings;
        Keyboard m_Device;

        public VirtualKeyboard()
        {
            m_ProjectSettings = InputSystem.settings;
            m_TestSettings = Object.Instantiate(m_ProjectSettings);
            m_TestSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            m_TestSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = m_TestSettings;
        }

        /// <summary>Holds exactly these keys down (others are released).</summary>
        public void Press(params Key[] keys)
        {
            m_Device ??= InputSystem.AddDevice<Keyboard>("Workshop Test Keyboard");
            InputSystem.QueueStateEvent(m_Device, new KeyboardState(keys));
        }

        public void ReleaseAll()
        {
            if (m_Device != null)
                InputSystem.QueueStateEvent(m_Device, new KeyboardState());
        }

        public void Dispose()
        {
            if (m_Device != null)
            {
                InputSystem.RemoveDevice(m_Device);
                m_Device = null;
            }
            InputSystem.settings = m_ProjectSettings;
            Object.Destroy(m_TestSettings);
        }
    }
}
