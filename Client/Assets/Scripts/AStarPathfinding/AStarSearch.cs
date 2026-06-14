using System;

namespace AStarPathfinding 
{

    /// <summary>
    /// [已废弃] 托管 A* 寻路算法实现 —— 所有运行时寻路已迁移至 BurstAStarJob。
    /// 
    /// 本文件仅作为 Burst 版本的算法参考保留，不再被战斗主流程调用。
    /// 若需要查看或修改 A* 寻路逻辑，请优先编辑 BurstAStarJob.cs。
    /// </summary>
    public class AStarSearch 
    {

        private readonly IGridProvider _grid;
        private readonly FastPriorityQueue _open;
        /// <summary>
        /// 搜索轮次计数器。每次 Reset() 自增，Cell.SearchId 与此值不等时视为未初始化。
        /// 将 Grid.Reset() O(n²) 降为 O(1)。
        /// </summary>
        private int _searchEpoch = 0;

        /// <summary>
        /// 构造函数：绑定网格并预分配优先队列。
        /// 
        /// FastPriorityQueue 的容量设为网格总格数（Width × Height），
        /// 这是理论上 Open List 可能包含的最大节点数，避免运行期扩容。
        /// </summary>
        /// <param name="grid">提供格子数据及 Blocked 信息的网格接口。</param>
        public AStarSearch(IGridProvider grid) 
        {
            _grid = grid;
            _open = new FastPriorityQueue(_grid.Size.X * _grid.Size.Y);
        }

        /// <summary>
        /// 启发函数：计算从 cell 到 goal 的「八方向距离（Octile Distance）」。
        /// 
        /// 在允许 8 方向移动的网格中，Octile Distance 是可接受（admissible）启发——
        /// 它永远不高估真实代价，因此 A* 在此启发下能保证找到最短路径。
        /// 
        /// 公式推导：
        ///   设 dx = |x1-x2|, dy = |y1-y2|
        ///   - 纯正交移动代价：max(dx, dy) 步（先走对角线 min(dx,dy) 步，再走直线）
        ///   - 每步正交代价 = 1，每步对角线代价 = √2
        ///   - 因此：octile = 1 * (dx + dy) + (√2 - 2) * min(dx, dy)
        ///     等价于：(dx + dy) - 2*min(dx,dy) 正交步 + min(dx,dy) 对角步
        ///              = (dx+dy) * 1 + (√2 - 2*1) * min(dx,dy)
        /// 
        /// 注意：本实现每步 G 代价固定为 1（见 Find 中 g = node.G + 1），
        /// 而非对角线用 √2，因此启发值略高于实际对角代价，会在少量情况下降低最优性，
        /// 但换来更快的搜索速度（更激进地向目标推进）。
        /// </summary>
        /// <param name="cell">当前节点。</param>
        /// <param name="goal">目标节点（在 Find 内部传入的是当前节点 node，详见 Find 方法注释）。</param>
        /// <returns>启发估算代价（double）。</returns>
        private double Heuristic(Cell cell, Cell goal) 
        {
            var dX = Math.Abs(cell.Location.X - goal.Location.X);
            var dY = Math.Abs(cell.Location.Y - goal.Location.Y);

            // Octile distance
            return 1 * (dX + dY) + (Math.Sqrt(2) - 2 * 1) * Math.Min(dX, dY);
        }

        /// <summary>
        /// 确保 cell 已为本轮搜索初始化（延迟 O(1) 替代 Grid.Reset）。
        /// 若 SearchId 不匹配当前 epoch，则原地清零搜索状态字段并打上本轮戳记。
        /// </summary>
        private void EnsureCellFresh(Cell cell)
        {
            if (cell.SearchId != _searchEpoch)
            {
                cell.SearchId  = _searchEpoch;
                cell.G         = 0;
                cell.H         = 0;
                cell.F         = 0;
                cell.Closed    = false;
                cell.Parent    = null;
            }
        }

        /// <summary>
        /// 重置搜索状态，清空所有格子的 G/H/F/Closed/Parent，并清空 Open List。
        /// 
        /// 通过自增 _searchEpoch 实现 O(1) 逻辑重置：
        /// 格子状态仅在首次被访问时按需清零（见 EnsureCellFresh），
        /// 不再需要 O(n²) 遍历整个网格。
        /// 注意：Blocked 标志不会被清除，因为它由外部的建筑布局决定，与单次寻路无关。
        /// </summary>
        public void Reset() 
        {
            _searchEpoch++;
            _open.Clear();
        }

        private Cell Search(Vector2Int start, Vector2Int goal)
        {
            Reset();
            Cell startCell = _grid[start];
            Cell goalCell = _grid[goal];

            // 确保起点已为本轮搜索初始化（G=0）
            EnsureCellFresh(startCell);

            // 步骤1：将起点压入 Open List，初始优先级（F）为 0
            _open.Enqueue(startCell, 0);
            var bounds = _grid.Size;
            Cell node = null;

            // 步骤2：主循环——每轮展开 F 最小的节点
            while (_open.Count > 0) 
            {
                // 取出当前代价最低的节点，移入 Closed List
                node = _open.Dequeue();
                node.Closed = true;

                // cBlock：记录当前节点的正交邻居中是否存在阻挡格，用于对角线切角检测
                var cBlock = false;

                // 进入所有邻居的 G 代价（统一为 1 步，不区分正交/对角线）
                var g = node.G + 1;

                // 已到达终点，退出循环，准备回溯路径
                if (goalCell.Location == node.Location) break;

                Vector2Int proposed = new Vector2Int(0, 0);

                // 遍历 8 个方向：索引 0-3 正交，索引 4-7 对角线
                for (var i = 0; i < PathingConstants.Directions.Length; i++) 
                {
                    var direction = PathingConstants.Directions[i];
                    proposed.X = node.Location.X + direction.X;
                    proposed.Y = node.Location.Y + direction.Y;

                    // Bounds checking：超出网格边界则跳过
                    if (proposed.X < 0 || proposed.X >= bounds.X || proposed.Y < 0 || proposed.Y >= bounds.Y)
                    {
                        continue;
                    }

                    Cell neighbour = _grid[proposed];

                    if (neighbour.Blocked) 
                    {
                        // 正交方向遇到阻挡：标记 cBlock，后续对角线将全部跳过
                        if (i < 4) cBlock = true;
                        continue;
                    }

                    // Prevent slipping between blocked cardinals by an open diagonal
                    // 对角线切角阻断：若任意正交邻居被阻挡，禁止走对角线（防止穿越建筑角落）
                    if (i >= 4 && cBlock) continue;

                    // 延迟初始化：首次访问时按需清零搜索状态（O(1) epoch 方案）
                    EnsureCellFresh(neighbour);

                    // 已在 Closed List 中，最优路径已确定，跳过
                    if (neighbour.Closed) continue;

                    if (!_open.Contains(neighbour)) 
                    {
                        // 邻居首次被发现：初始化代价并加入 Open List
                        neighbour.G = g;                           // 从起点到邻居的实际代价
                        neighbour.H = Heuristic(neighbour, node);  // 到目标的启发估算
                        neighbour.Parent = node;                   // 记录来路，用于路径回溯

                        // F will be set by the queue
                        // FastPriorityQueue.Enqueue 内部会将 priority 写入 neighbour.F
                        _open.Enqueue(neighbour, neighbour.G + neighbour.H);
                    }
                    else if (g + neighbour.H < neighbour.F) 
                    {
                        // 邻居已在 Open List 中，但经由当前节点的路径更短：更新代价和父节点，
                        // 并通知堆重新排序（UpdatePriority 内部会调用 CascadeUp/CascadeDown），
                        // 确保堆结构始终有效，保证 A* 能正确找到最优路径。
                        neighbour.G = g;
                        neighbour.Parent = node;
                        _open.UpdatePriority(neighbour, neighbour.G + neighbour.H);
                    }
                }
            }

            return node;
        }

        private static int CountPathNodes(Cell node)
        {
            int count = 0;
            while (node != null)
            {
                count++;
                node = node.Parent;
            }
            return count;
        }

        private static Cell[] BuildCellPath(Cell node)
        {
            int count = CountPathNodes(node);
            Cell[] path = new Cell[count];
            for (int i = count - 1; i >= 0; i--)
            {
                path[i] = node;
                node = node.Parent;
            }
            return path;
        }

        private static Vector2Int[] BuildLocationPath(Cell node)
        {
            int count = CountPathNodes(node);
            Vector2Int[] path = new Vector2Int[count];
            for (int i = count - 1; i >= 0; i--)
            {
                path[i] = node.Location;
                node = node.Parent;
            }
            return path;
        }

        /// <summary>
        /// 执行 A* 搜索，返回从 start 到 goal 的路径（含起点和终点）。
        /// </summary>
        /// <param name="start">起始格子坐标。</param>
        /// <param name="goal">目标格子坐标。</param>
        /// <returns>
        /// Cell 数组，索引 0 为起点，最后一个元素为终点（或可达的最近点）。
        /// 若起点即终点，返回仅含起点的单元素数组。
        /// </returns>
        public Cell[] Find(Vector2Int start, Vector2Int goal) 
        {
            return BuildCellPath(Search(start, goal));
        }
        ///<!----> <summary>
        /// 执行 A* 搜索，返回从 start 到 goal 的路径（含起点和终点）的坐标数组。
        /// </summary>
        /// <param name="start">起始格子坐标。</param>
        /// <param name="goal">目标格子坐标。</param>
        /// <returns>
        public Vector2Int[] FindLocations(Vector2Int start, Vector2Int goal)
        {
            return BuildLocationPath(Search(start, goal));
        }
    }

}