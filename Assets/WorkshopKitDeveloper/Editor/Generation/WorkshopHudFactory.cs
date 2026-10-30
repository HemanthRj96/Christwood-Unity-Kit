using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnityWorkshop
{
    /// <summary>Builds the Workshop HUD prefab: a scalable overlay canvas plus its own Event System.</summary>
    static class WorkshopHudFactory
    {
        static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.14f, 0.78f);
        static readonly Color DimColor = new Color(0.02f, 0.03f, 0.05f, 0.7f);
        static readonly Color MessageColor = new Color(1f, 0.83f, 0.3f, 0.95f);
        static readonly Color ErrorColor = new Color(0.75f, 0.12f, 0.15f, 0.95f);
        static readonly Color ButtonColor = new Color(0.24f, 0.86f, 0.52f, 1f);
        static readonly Color TextColor = new Color(0.97f, 0.97f, 0.98f, 1f);
        static readonly Color DarkText = new Color(0.08f, 0.1f, 0.14f, 1f);

        static Font s_Font;
        static Sprite s_Sprite;

        public static GameObject Build(string path)
        {
            s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            s_Sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = new GameObject("Workshop HUD");
            var hud = root.AddComponent<WorkshopHUD>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasObject.transform;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(root.transform, false);

            // Title and objective, top left.
            var header = Panel(canvasRect, "Header", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(760f, 0f), PanelColor);
            Stack(header, 20, 6);
            var title = Label(header, "Title", "Game Title", 40, FontStyle.Bold, TextAnchor.UpperLeft, TextColor);
            var objective = Label(header, "Objective", "Objective", 26, FontStyle.Normal, TextAnchor.UpperLeft, TextColor);

            // Progress, countdown and keys, stacked top right. Hidden panels take no space.
            var column = Rect(canvasRect, "Status Column");
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(1f, 1f);
            column.anchoredPosition = new Vector2(-32f, -32f);
            column.sizeDelta = new Vector2(340f, 0f);
            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 12f;
            columnLayout.childAlignment = TextAnchor.UpperRight;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var progressGroup = Panel(column, "Progress", Vector2.one, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero, PanelColor);
            Stack(progressGroup, 18, 0);
            var progress = Label(progressGroup, "Count", "Gems: 0 / 0", 34, FontStyle.Bold, TextAnchor.MiddleCenter, MessageColor);

            var timerGroup = Panel(column, "Timer", Vector2.one, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero, PanelColor);
            Stack(timerGroup, 16, 0);
            var timer = Label(timerGroup, "Text", "Time left: 3:00", 30, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);

            var keysGroup = Panel(column, "Keys", Vector2.one, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero, PanelColor);
            Stack(keysGroup, 16, 4);
            var keys = Label(keysGroup, "Text", "Red (Circle) Key", 26, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            keys.supportRichText = true;

            // Controls, bottom left.
            var controlsPanel = Panel(canvasRect, "Controls", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32f, 32f), new Vector2(360f, 0f), PanelColor);
            Stack(controlsPanel, 16, 0);
            var controls = Label(controlsPanel, "Text", "WASD: Move\nEsc: Pause", 22, FontStyle.Normal, TextAnchor.UpperLeft, TextColor);

            // Goal message, upper centre.
            var messagePanel = Panel(canvasRect, "Message", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(820f, 0f), MessageColor);
            Stack(messagePanel, 18, 0);
            var message = Label(messagePanel, "Text", "Message", 30, FontStyle.Bold, TextAnchor.MiddleCenter, DarkText);

            // Pause.
            var pause = FullScreen(canvasRect, "Pause", DimColor);
            var pauseBox = Panel(pause, "Box", Half, Half, Half, Vector2.zero, new Vector2(560f, 0f), PanelColor);
            Stack(pauseBox, 36, 18);
            Label(pauseBox, "Heading", "Paused", 60, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            var resume = Button(pauseBox, "Resume", "Resume");
            var pauseRestart = Button(pauseBox, "Restart", "Restart Level");
            Label(pauseBox, "Hint", "Press Esc to resume", 22, FontStyle.Italic, TextAnchor.MiddleCenter, TextColor);

            // Results.
            var results = FullScreen(canvasRect, "Results", DimColor);
            var resultBox = Panel(results, "Box", Half, Half, Half, Vector2.zero, new Vector2(620f, 0f), PanelColor);
            Stack(resultBox, 40, 18);
            var resultTitle = Label(resultBox, "Heading", "You Win!", 72, FontStyle.Bold, TextAnchor.MiddleCenter, MessageColor);
            var resultDetail = Label(resultBox, "Detail", "Time: 0:00", 30, FontStyle.Normal, TextAnchor.MiddleCenter, TextColor);
            var resultRestart = Button(resultBox, "Play Again", "Play Again");

            // Setup problems.
            var errorPanel = Panel(canvasRect, "Setup Problem", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Half, Vector2.zero, new Vector2(900f, 0f), ErrorColor);
            Stack(errorPanel, 28, 10);
            Label(errorPanel, "Heading", "This game is not set up yet", 36, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            var errorText = Label(errorPanel, "Text", "Problem", 26, FontStyle.Normal, TextAnchor.MiddleCenter, TextColor);

            var serialized = new SerializedObject(hud);
            Set(serialized, "m_Title", title);
            Set(serialized, "m_Objective", objective);
            Set(serialized, "m_Controls", controls);
            Set(serialized, "m_ProgressGroup", progressGroup.gameObject);
            Set(serialized, "m_Progress", progress);
            Set(serialized, "m_MessagePanel", messagePanel.gameObject);
            Set(serialized, "m_Message", message);
            Set(serialized, "m_TimerGroup", timerGroup.gameObject);
            Set(serialized, "m_Timer", timer);
            Set(serialized, "m_KeysGroup", keysGroup.gameObject);
            Set(serialized, "m_Keys", keys);
            Set(serialized, "m_PausePanel", pause.gameObject);
            Set(serialized, "m_ResumeButton", resume);
            Set(serialized, "m_PauseRestartButton", pauseRestart);
            Set(serialized, "m_ResultPanel", results.gameObject);
            Set(serialized, "m_ResultTitle", resultTitle);
            Set(serialized, "m_ResultDetail", resultDetail);
            Set(serialized, "m_ResultRestartButton", resultRestart);
            Set(serialized, "m_ErrorPanel", errorPanel.gameObject);
            Set(serialized, "m_ErrorText", errorText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pause.gameObject.SetActive(false);
            results.gameObject.SetActive(false);
            errorPanel.gameObject.SetActive(false);
            messagePanel.gameObject.SetActive(false);
            timerGroup.gameObject.SetActive(false);
            keysGroup.gameObject.SetActive(false);

            return WorkshopPrefabFactory.Save(root, path);
        }

        static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        static void Set(SerializedObject serialized, string property, Object value) =>
            serialized.FindProperty(property).objectReferenceValue = value;

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = s_Sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform FullScreen(RectTransform parent, string name, Color color)
        {
            var rect = Rect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        /// <summary>Vertical layout that grows the panel to fit its text.</summary>
        static void Stack(RectTransform panel, int padding, float spacing)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        static Text Label(RectTransform parent, string name, string value, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = s_Font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button Button(RectTransform parent, string name, string label)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = s_Sprite;
            image.type = Image.Type.Sliced;
            image.color = ButtonColor;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 76f;
            layout.minHeight = 76f;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 1f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.85f, 0.75f);
            button.colors = colors;

            var text = Label(rect, "Label", label, 32, FontStyle.Bold, TextAnchor.MiddleCenter, DarkText);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
