using UnityEngine;
using UnityEngine.UI;

namespace P2D.Presentation.UI
{
    /// <summary>
    /// Reference 1080×1920. Phones taller than 9:16 match width (extra vertical space);
    /// tablets wider than 9:16 match height and keep content in a centered column.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class PortraitCanvasScaler : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);
        const float ReferenceAspect = 1080f / 1920f;

        CanvasScaler scaler;
        Vector2Int lastScreen;

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = ReferenceResolution;
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) Apply();
        }

        void Apply()
        {
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.height <= 0) return;
            float aspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = aspect > ReferenceAspect ? 1f : 0f;
        }
    }
}
