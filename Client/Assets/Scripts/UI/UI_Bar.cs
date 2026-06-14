namespace DevelopersHub.ClashOfWhatecer
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;

    public class UI_Bar : MonoBehaviour
    {

        public Image bar = null;
        public RectTransform rect = null;
        public TextMeshProUGUI[] texts = null;
        [SerializeField] private float height = 0.04f;
        [SerializeField] private float aspect = 2.5f;

        private Vector2 size = Vector2.one;
        private int _lastZoomVersion = -1;

        private void Awake()
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
        }

        private void Start()
        {
            RefreshSize(true);
        }

        private void OnEnable()
        {
            UIEvents.onViewportChanged += OnViewportChanged;
        }

        private void OnDisable()
        {
            UIEvents.onViewportChanged -= OnViewportChanged;
        }

        private void OnViewportChanged() => RefreshSize();

        private void RefreshSize(bool force = false)
        {
            if (rect != null && CameraController.instanse != null)
            {
                int zoomVersion = CameraController.instanse.zoomVersion;
                if (!force && _lastZoomVersion == zoomVersion)
                {
                    return;
                }
                size = new Vector2(Screen.height * height * aspect, Screen.height * height);
                rect.sizeDelta = size / CameraController.instanse.zoomScale;
                _lastZoomVersion = zoomVersion;
            }
        }

    }
}