namespace DevelopersHub.ClashOfWhatecer
{
    /// <summary>
    /// 轻量静态事件总线，用于解耦 UI 组件与数据源。
    /// 订阅者必须在 OnDisable / OnDestroy 中取消订阅，避免内存泄漏。
    /// </summary>
    public static class UIEvents
    {
        /// <summary>
        /// 当相机视口发生实际变化（平移 / 缩放 / 窗口尺寸变化）时触发。
        /// UI_Button、UI_Bar 可订阅此事件刷新尺寸；
        /// Building 可订阅此事件更新世界坐标到屏幕坐标的映射。
        /// </summary>
        public static event System.Action onViewportChanged;

        internal static void FireViewportChanged() => onViewportChanged?.Invoke();
    }
}
