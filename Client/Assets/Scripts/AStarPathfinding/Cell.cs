namespace AStarPathfinding 
{
    public class Cell 
    {
        public bool Blocked;
        public bool Closed;
        public double F;
        public double G;
        public double H;

        public Vector2Int Location;
        public Cell Parent;
        public int QueueIndex;
        /// <summary>搜索轮次标识，用于 O(1) 延迟初始化（epoch 方案）。</summary>
        public int SearchId;

        public Cell(Vector2Int location) => Location = location;
        public override string ToString() => $"[{Location.X},{Location.Y}]";
    }
}