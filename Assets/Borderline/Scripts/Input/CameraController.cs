using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace Borderline
{
    /// <summary>
    /// Mobile-ready 2D camera and the single owner of pointer input:
    ///  - one finger / left mouse drag  = pan
    ///  - two-finger pinch / scroll wheel = zoom (around the fingers / cursor)
    ///  - short press without movement    = Tapped(worldPosition)
    /// Touches that start on UI are ignored. Works with the new Input System (default in Unity 6)
    /// and falls back to the legacy Input Manager automatically.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField, Min(0.5f)] float minOrthoSize = 2f;
        [SerializeField, Min(1f)] float maxOrthoSize = 12f;
        [Tooltip("Fraction of the current size zoomed per scroll step.")]
        [SerializeField, Range(0.01f, 0.5f)] float scrollZoomStep = 0.1f;

        [Header("Tap detection")]
        [Tooltip("Max finger travel for a tap, in 160-dpi 'points' (scaled by screen DPI).")]
        [SerializeField, Min(1f)] float tapMaxMovePoints = 12f;
        [SerializeField, Min(0.05f)] float tapMaxSeconds = 0.45f;

        [Header("Bounds")]
        [SerializeField, Min(0f)] float boundsPadding = 1f;

        /// <summary>Raised with the world position of a tap that did not land on UI.</summary>
        public event Action<Vector2> Tapped;

        Camera cam;
        Bounds bounds;
        bool hasBounds;

        readonly List<Vector2> pointers = new List<Vector2>(4);
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        int previousCount;
        Vector2 lastSingle, downPosition;
        float downTime;
        bool tapCandidate, dragging, singleBlockedByUI, pinchBlockedByUI;
        float lastPinchDistance;
        Vector2 lastPinchMid;

        float TapThresholdPixels => tapMaxMovePoints * (Screen.dpi > 0f ? Screen.dpi / 160f : 1f);

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Disable();
#endif
        }

        public void SetBounds(Bounds worldBounds)
        {
            bounds = worldBounds;
            hasBounds = true;
            float fit = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(0.1f, cam.aspect)) + boundsPadding;
            maxOrthoSize = Mathf.Max(minOrthoSize + 0.5f, fit);
            ClampCamera();
        }

        public void FocusOn(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            ClampCamera();
        }

        void Update()
        {
            GatherPointers();
            int count = pointers.Count;

            float scroll = ReadScroll();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector2 mouse = ReadMousePosition();
                if (!IsOverUI(mouse))
                    ZoomAt(mouse, cam.orthographicSize * (1f - Mathf.Sign(scroll) * scrollZoomStep));
            }

            if (count == 1)
            {
                Vector2 p = pointers[0];
                if (previousCount != 1)
                {
                    if (previousCount == 0)
                    {
                        downPosition = p;
                        downTime = Time.unscaledTime;
                        singleBlockedByUI = IsOverUI(p);
                        tapCandidate = !singleBlockedByUI;
                        dragging = false;
                    }
                    lastSingle = p; // also covers 2 -> 1 finger without a jump
                }
                else if (!singleBlockedByUI)
                {
                    if (!dragging && (p - downPosition).magnitude > TapThresholdPixels)
                    {
                        dragging = true;
                        tapCandidate = false;
                    }
                    if (dragging) Pan(lastSingle, p);
                    lastSingle = p;
                }
            }
            else if (count >= 2)
            {
                tapCandidate = false;
                Vector2 a = pointers[0];
                Vector2 b = pointers[1];
                float distance = Vector2.Distance(a, b);
                Vector2 mid = (a + b) * 0.5f;

                if (previousCount < 2)
                {
                    pinchBlockedByUI = IsOverUI(a) && IsOverUI(b);
                }
                else if (!pinchBlockedByUI)
                {
                    Pan(lastPinchMid, mid);
                    if (lastPinchDistance > 1f && distance > 1f)
                        ZoomAt(mid, cam.orthographicSize * (lastPinchDistance / distance));
                }
                lastPinchDistance = distance;
                lastPinchMid = mid;
            }
            else
            {
                if (previousCount == 1 && tapCandidate && Time.unscaledTime - downTime <= tapMaxSeconds)
                    Tapped?.Invoke(cam.ScreenToWorldPoint(lastSingle));
                tapCandidate = false;
                dragging = false;
            }

            previousCount = count;
            ClampCamera();
        }

        // ------------------------------------------------------------------ camera ops

        void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            Vector3 from = cam.ScreenToWorldPoint(fromScreen);
            Vector3 to = cam.ScreenToWorldPoint(toScreen);
            Vector3 delta = from - to;
            delta.z = 0f;
            transform.position += delta;
        }

        void ZoomAt(Vector2 screenPoint, float newSize)
        {
            Vector3 before = cam.ScreenToWorldPoint(screenPoint);
            cam.orthographicSize = Mathf.Clamp(newSize, minOrthoSize, maxOrthoSize);
            Vector3 after = cam.ScreenToWorldPoint(screenPoint);
            Vector3 delta = before - after;
            delta.z = 0f;
            transform.position += delta;
        }

        void ClampCamera()
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
            if (!hasBounds) return;
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, bounds.min.x, bounds.max.x);
            p.y = Mathf.Clamp(p.y, bounds.min.y, bounds.max.y);
            transform.position = p;
        }

        // ------------------------------------------------------------------ input abstraction

        void GatherPointers()
        {
            pointers.Clear();
#if ENABLE_INPUT_SYSTEM
            foreach (ETouch touch in ETouch.activeTouches)
                pointers.Add(touch.screenPosition);
            if (pointers.Count == 0 && Mouse.current != null && Mouse.current.leftButton.isPressed)
                pointers.Add(Mouse.current.position.ReadValue());
#else
            for (int i = 0; i < Input.touchCount; i++)
                pointers.Add(Input.GetTouch(i).position);
            if (pointers.Count == 0 && Input.GetMouseButton(0))
                pointers.Add(Input.mousePosition);
#endif
        }

        static float ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        static Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var data = new PointerEventData(eventSystem) { position = screenPosition };
            uiHits.Clear();
            eventSystem.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }
    }
}
