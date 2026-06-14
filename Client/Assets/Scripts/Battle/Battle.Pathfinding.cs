using AStarPathfinding;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DevelopersHub.ClashOfWhatecer
{
    public partial class Battle
    {
        // ── A* scratch pool ──────────────────────────────
        // 每个 Schedule 调用会借出一个 pool slot（独立 NativeArray），
        // FlushBurstRequests 批量 Schedule 全部 pending → Complete → 归还所有 slot。
        // Pool 是内存预分配（免 GC），不是并发限制——实际并行度由 Unity Job Scheduler + CPU 核数决定。
        // 预算型微批处理：每帧最多 8 单位 × 4 角 × 2 模式 = 64 槽，无需更大。
        private const int ASTAR_POOL_SIZE = 64;
        private const int MAX_PATH_SLOTS_PER_FRAME = 64;
        private NativeArray<BurstNodeData>[] _astarPoolNodes;
        private NativeArray<int>[] _astarPoolHeaps;
        private NativeArray<Vector2Int>[] _astarPoolResultPaths;
        private NativeArray<int>[] _astarPoolResultLengths;
        private int[] _astarPoolEpochs;
        private int _astarPoolNextSlot;
        private struct PendingAStar { public int slot; public bool unlimited; public int2 start; public int2 goal; }
        private readonly List<PendingAStar> _pendingAStar = new List<PendingAStar>(16);

        // ── Burst 路径计算缓冲区 ──
        private NativeArray<float> burstPathLengthResult;
        private NativeArray<BurstPathPosition> burstPathPositionResult;
        private NativeArray<byte> burstSearchBlocked;
        private NativeArray<byte> burstUnlimitedBlocked;

        // ── 预算型微批处理 ────────────────────────────
        // 每帧最多处理 MAX_PATH_UNITS_PER_FRAME(8) 个单位，最多占用 MAX_PATH_SLOTS_PER_FRAME(64) 个 A* 槽。
        private readonly List<int> _batchPathBuildingQueue = new List<int>(16);      // 本帧批内对应的 building index
        // 批处理内复用的扁平数组，匹配 8 单位 × 4 角 × 2 模式 = 64 槽预算
        private int[] _batchCornerCounts = new int[16];
        private int[] _batchSlotOffsets = new int[16];       // 每个单位在扁平数组中的起始索引
        private int[] _batchFlatSlots = new int[128];         // [search_c0, unlimited_c0, search_c1, ...]
        private BattleVector2Int[] _batchFlatCandidates = new BattleVector2Int[64];
        private Vector2Int[] _batchFlatStarts = new Vector2Int[64];
        private Vector2Int[] _batchFlatGoals = new Vector2Int[64];
        private int _batchUnitCount;
        private int[] _batchUnitIndices = new int[16];  // 与 _batchPathBuildingQueue 一一对应的 unit index
        // ────────────────────────────────────────────────────────────

        // ── Burst Scratch 缓冲区管理 ──

        private void EnsureBurstScratchBuffers()
        {
            if (!burstPathLengthResult.IsCreated)
            {
                burstPathLengthResult = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
            if (!burstPathPositionResult.IsCreated)
            {
                burstPathPositionResult = new NativeArray<BurstPathPosition>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        private void EnsureBurstAStarBuffers()
        {
            int gridWidth = Data.gridSize + (Data.battleGridOffset * 2);
            int gridHeight = gridWidth;
            int nodeCount = gridWidth * gridHeight;
            int heapLength = nodeCount + 2;

            EnsureBurstBuffer(ref burstSearchBlocked, nodeCount);
            EnsureBurstBuffer(ref burstUnlimitedBlocked, nodeCount);

            for (int i = 0; i < nodeCount; i++)
            {
                burstSearchBlocked[i] = 0;
                burstUnlimitedBlocked[i] = 0;
            }

            EnsureAStarPool(nodeCount, heapLength);
        }

        private void EnsureAStarPool(int nodeCount, int heapLength)
        {
            if (_astarPoolNodes != null && _astarPoolNodes.Length == ASTAR_POOL_SIZE)
            {
                return;
            }

            DisposeAStarPool();

            _astarPoolNodes = new NativeArray<BurstNodeData>[ASTAR_POOL_SIZE];
            _astarPoolHeaps = new NativeArray<int>[ASTAR_POOL_SIZE];
            _astarPoolResultPaths = new NativeArray<Vector2Int>[ASTAR_POOL_SIZE];
            _astarPoolResultLengths = new NativeArray<int>[ASTAR_POOL_SIZE];
            _astarPoolEpochs = new int[ASTAR_POOL_SIZE];

            for (int i = 0; i < ASTAR_POOL_SIZE; i++)
            {
                _astarPoolNodes[i] = new NativeArray<BurstNodeData>(nodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _astarPoolHeaps[i] = new NativeArray<int>(heapLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _astarPoolResultPaths[i] = new NativeArray<Vector2Int>(nodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _astarPoolResultLengths[i] = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _astarPoolResultLengths[i][0] = -1;
            }
        }

        private void DisposeAStarPool()
        {
            if (_astarPoolNodes != null)
            {
                for (int i = 0; i < _astarPoolNodes.Length; i++)
                {
                    DisposeNativeArray(ref _astarPoolNodes[i]);
                    DisposeNativeArray(ref _astarPoolHeaps[i]);
                    DisposeNativeArray(ref _astarPoolResultPaths[i]);
                    DisposeNativeArray(ref _astarPoolResultLengths[i]);
                }
                _astarPoolNodes = null;
                _astarPoolHeaps = null;
                _astarPoolResultPaths = null;
                _astarPoolResultLengths = null;
                _astarPoolEpochs = null;
            }
        }

        private static void EnsureBurstBuffer<T>(ref NativeArray<T> buffer, int length) where T : struct
        {
            if (!buffer.IsCreated || buffer.Length != length)
            {
                if (buffer.IsCreated)
                {
                    buffer.Dispose();
                }
                buffer = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void DisposeBurstAStarBuffers()
        {
            DisposeNativeArray(ref burstPathLengthResult);
            DisposeNativeArray(ref burstPathPositionResult);
            DisposeNativeArray(ref burstSearchBlocked);
            DisposeNativeArray(ref burstUnlimitedBlocked);
            DisposeAStarPool();
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        // ── Burst A* 调度 ──

        /// <summary>
        /// 调度一个Burst A*寻路任务，并返回用于获取结果的槽位索引；调用者需要在合适的时机调用FlushBurstRequests来确保任务被执行。
        /// </summary>
        private int ScheduleBurstFindLocations(bool unlimited, Vector2Int start, Vector2Int goal)
        {
            EnsureBurstAStarBuffers();

            int slot = _astarPoolNextSlot;
            _astarPoolNextSlot = (slot + 1) % ASTAR_POOL_SIZE;
            _pendingAStar.Add(new PendingAStar
            {
                slot = slot,
                unlimited = unlimited,
                start = new int2(start.X, start.Y),
                goal = new int2(goal.X, goal.Y),
            });
            return slot;
        }

        private void FlushBurstRequests()
        {
            if (_pendingAStar.Count == 0)
            {
                return;
            }

            var handles = new NativeArray<JobHandle>(_pendingAStar.Count, Allocator.Temp);
            for (int i = 0; i < _pendingAStar.Count; i++)
            {
                var req = _pendingAStar[i];
                _astarPoolEpochs[req.slot]++;
                handles[i] = new AStarBurstJob
                {
                    blocked = req.unlimited ? burstUnlimitedBlocked : burstSearchBlocked,
                    nodes = _astarPoolNodes[req.slot],
                    heap = _astarPoolHeaps[req.slot],
                    gridW = grid.Width,
                    gridH = grid.Height,
                    searchEpoch = _astarPoolEpochs[req.slot],
                    start = req.start,
                    goal = req.goal,
                    resultPath = _astarPoolResultPaths[req.slot],
                    resultLength = _astarPoolResultLengths[req.slot],
                }.Schedule();
            }

            JobHandle.CombineDependencies(handles).Complete();
            handles.Dispose();
            _pendingAStar.Clear();
            _astarPoolNextSlot = 0;
        }

        private void GetBurstResult(int slot, out int pointCount, out NativeArray<Vector2Int> path)
        {
            pointCount = _astarPoolResultLengths[slot][0];
            path = _astarPoolResultPaths[slot];
        }

        // ── 路径计算工具方法 ──

        /// <summary>
        /// 计算路径总长度（IList 重载）。
        /// </summary>
        private static float GetPathLength(IList<Vector2Int> path, int pointCount, bool includeCellSize = true)
        {
            float length = 0f;
            if (path != null && pointCount > 1)
            {
                for (int i = 1; i < pointCount; i++)
                {
                    length += BattleVector2.Distance(new BattleVector2(path[i - 1].X, path[i - 1].Y), new BattleVector2(path[i].X, path[i].Y));
                }
            }
            if (includeCellSize)
            {
                length *= Data.gridCellSize;
            }
            return length;
        }

        /// <summary>
        /// 计算路径总长度（NativeArray 重载，经由 Burst）。
        /// </summary>
        private float GetPathLength(NativeArray<Vector2Int> path, int pointCount, bool includeCellSize = true)
        {
            if (!path.IsCreated)
            {
                return 0f;
            }
            EnsureBurstScratchBuffers();
            return BurstPathMath.GetPathLength(path, pointCount, includeCellSize ? Data.gridCellSize : 1f, burstPathLengthResult);
        }

        private int CountBlockingWalls(IList<Vector2Int> points, int pointCount)
        {
            int count = 0;
            for (int i = 0; i < pointCount; i++)
            {
                for (int j = 0; j < blockedTiles.Count; j++)
                {
                    if (blockedTiles[j].position.x == points[i].X && blockedTiles[j].position.y == points[i].Y)
                    {
                        if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                        {
                            count++;
                        }
                        break;
                    }
                }
            }
            return count;
        }

        private int CountBlockingWalls(NativeArray<Vector2Int> points, int pointCount)
        {
            int count = 0;
            for (int i = 0; i < pointCount; i++)
            {
                for (int j = 0; j < blockedTiles.Count; j++)
                {
                    if (blockedTiles[j].position.x == points[i].X && blockedTiles[j].position.y == points[i].Y)
                    {
                        if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                        {
                            count++;
                        }
                        break;
                    }
                }
            }
            return count;
        }

        private void PopulateBlockingWalls(Path path)
        {
            path.blocks.Clear();
            for (int i = 0; i < path.pointCount; i++)
            {
                for (int j = 0; j < blockedTiles.Count; j++)
                {
                    if (blockedTiles[j].position.x == path.points[i].X && blockedTiles[j].position.y == path.points[i].Y)
                    {
                        if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                        {
                            path.blocks.Add(blockedTiles[j]);
                        }
                        break;
                    }
                }
            }
        }

        private static bool IsGridPositionValid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Data.gridSize && y < Data.gridSize;
        }

        /// <summary>
        /// 按归一化进度从路径中插值出一个世界坐标位置。
        /// </summary>
        private BattleVector2 GetPathPosition(NativeArray<Vector2Int> path, int pointCount, float t)
        {
            EnsureBurstScratchBuffers();
            BurstPathPosition position = BurstPathMath.GetPathPosition(path, pointCount, t, Data.gridCellSize, burstPathPositionResult);
            return new BattleVector2(position.x, position.y);
        }

        /// <summary>
        /// 将网格坐标转换为格子中心点的世界坐标。
        /// </summary>
        private static BattleVector2 GridToWorldPosition(BattleVector2Int position)
        {
            return new BattleVector2(position.x * Data.gridCellSize + Data.gridCellSize / 2f, position.y * Data.gridCellSize + Data.gridCellSize / 2f);
        }

        /// <summary>
        /// 将世界坐标转换为所在格子的网格坐标。
        /// </summary>
        private static BattleVector2Int WorldToGridPosition(BattleVector2 position)
        {
            return new BattleVector2Int((int)Math.Floor(position.x / Data.gridCellSize), (int)Math.Floor(position.y / Data.gridCellSize));
        }

        // ── 寻路核心逻辑 ──

        /// <summary>
        /// 为单位计算通往目标建筑的路径；地面单位可能先锁定墙体，飞行单位则忽略阻挡直取最短路线。
        /// </summary>
        private (int, Path) GetPathToBuilding(int buildingIndex, int unitIndex)
        {
            if (_buildings[buildingIndex].building.id == Data.BuildingID.wall || _buildings[buildingIndex].building.id == Data.BuildingID.decoration || _buildings[buildingIndex].building.id == Data.BuildingID.obstacle)
            {
                return (-1, null);
            }

            BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);

            int startX = _buildings[buildingIndex].building.x;
            int endX = _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns - 1;
            int startY = _buildings[buildingIndex].building.y;
            int endY = _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.rows - 1;
            if (_units[unitIndex].unit.movement == Data.UnitMoveType.ground && _buildings[buildingIndex].building.id == Data.BuildingID.wall)
            {
                startX--;
                startY--;
                endX++;
                endY++;
            }

            int columnCount = startX == endX ? 1 : 2;
            int rowCount = startY == endY ? 1 : 2;
            if (_units[unitIndex].unit.movement == Data.UnitMoveType.ground)
            {
                #region With Walls Effect
                Path bestPath = null;
                float distance = 99999;
                int blocks = 999;
                int corners = columnCount * rowCount;
                int[] searchSlots = new int[corners];
                int[] unlimitedSlots = new int[corners];
                BattleVector2Int[] candidates = new BattleVector2Int[corners];
                Vector2Int[] starts = new Vector2Int[corners];
                Vector2Int[] goals = new Vector2Int[corners];
                int ci = 0;
                for (int x = 0; x < columnCount; x++)
                {
                    int column = x == 0 ? startX : endX;
                    for (int y = 0; y < rowCount; y++)
                    {
                        int row = y == 0 ? startY : endY;
                        if (IsGridPositionValid(column, row))
                        {
                            candidates[ci] = new BattleVector2Int(column, row);
                            starts[ci] = new Vector2Int(column, row);
                            goals[ci] = new Vector2Int(unitGridPosition.x, unitGridPosition.y);
                            searchSlots[ci] = ScheduleBurstFindLocations(false, starts[ci], goals[ci]);
                            unlimitedSlots[ci] = ScheduleBurstFindLocations(true, starts[ci], goals[ci]);
                        }
                        else
                        {
                            searchSlots[ci] = -1;
                        }
                        ci++;
                    }
                }

                FlushBurstRequests();

                ci = 0;
                for (int x = 0; x < columnCount; x++)
                {
                    for (int y = 0; y < rowCount; y++)
                    {
                        if (searchSlots[ci] < 0) { ci++; continue; }

                        GetBurstResult(searchSlots[ci], out int searchPointCount, out NativeArray<Vector2Int> searchPath);
                        if (Path.IsValid(searchPath, searchPointCount, starts[ci], goals[ci]))
                        {
                            float pathLength = GetPathLength(searchPath, searchPointCount);
                            int lengthToBlocks = (int)Math.Floor(pathLength / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                            if (pathLength < distance && lengthToBlocks <= blocks)
                            {
                                Path path = new Path();
                                if (path.Create(searchPath, searchPointCount, candidates[ci], unitGridPosition))
                                {
                                    path.length = pathLength;
                                    bestPath = path;
                                    distance = pathLength;
                                    blocks = lengthToBlocks;
                                }
                            }
                        }

                        GetBurstResult(unlimitedSlots[ci], out int unlimitedPointCount, out NativeArray<Vector2Int> unlimitedPath);
                        if (Path.IsValid(unlimitedPath, unlimitedPointCount, starts[ci], goals[ci]))
                        {
                            float pathLength = GetPathLength(unlimitedPath, unlimitedPointCount);
                            int blockCount = CountBlockingWalls(unlimitedPath, unlimitedPointCount);
                            if (pathLength < distance && blockCount <= blocks)
                            {
                                Path path = new Path();
                                if (path.Create(unlimitedPath, unlimitedPointCount, candidates[ci], unitGridPosition))
                                {
                                    PopulateBlockingWalls(path);
                                    path.length = pathLength;
                                    bestPath = path;
                                    distance = pathLength;
                                    blocks = blockCount;
                                }
                            }
                        }
                        ci++;
                    }
                }

                if (bestPath == null)
                {
                    return (-1, null);
                }

                bestPath.ReversePoints();
                if (bestPath.blocks.Count > 0)
                {
                    for (int i = 0; i < _units.Count; i++)
                    {
                        if (_units[i].health <= 0 || _units[i].unit.movement != Data.UnitMoveType.ground || i == unitIndex || _units[i].target < 0 || _units[i].mainTarget != buildingIndex || _units[i].mainTarget < 0 || _buildings[_units[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_units[i].mainTarget].health <= 0)
                        {
                            continue;
                        }
                        BattleVector2Int pos = WorldToGridPosition(_units[i].position);
                        Vector2Int pathStart = new Vector2Int(pos.x, pos.y);
                        Vector2Int pathGoal = new Vector2Int(unitGridPosition.x, unitGridPosition.y);
                        int existingSlot = ScheduleBurstFindLocations(false, pathStart, pathGoal);
                        int endSlotForGroup = -1;
                        Vector2Int endForGroup = default;
                        if (id <= Data.battleGroupWallAttackRadius)
                        {
                            endForGroup = _units[i].path.LastPoint;
                            endSlotForGroup = ScheduleBurstFindLocations(false, pathStart, new Vector2Int(endForGroup.X, endForGroup.Y));
                        }
                        FlushBurstRequests();
                        GetBurstResult(existingSlot, out int existingPathCount, out NativeArray<Vector2Int> existingPath);
                        if (!Path.IsValid(existingPath, existingPathCount, pathStart, pathGoal))
                        {
                            continue;
                        }
                        if (id <= Data.battleGroupWallAttackRadius)
                        {
                            Path path = new Path();
                            GetBurstResult(endSlotForGroup, out int sharedPathCount, out NativeArray<Vector2Int> groupPath);
                            if (path.Create(groupPath, sharedPathCount, pos, new BattleVector2Int(endForGroup.X, endForGroup.Y)))
                            {
                                _units[unitIndex].mainTarget = buildingIndex;
                                path.blocks = _units[i].path.blocks;
                                path.length = GetPathLength(path.points, path.pointCount);
                                return (_units[i].target, path);
                            }
                        }
                    }

                    Tile last = bestPath.blocks[bestPath.blocks.Count - 1];
                    for (int i = bestPath.pointCount - 1; i >= 0; i--)
                    {
                        int x = bestPath.points[i].X;
                        int y = bestPath.points[i].Y;
                        if (x == last.position.x && y == last.position.y)
                        {
                            bestPath.pointCount = i;
                            break;
                        }
                    }
                    _units[unitIndex].mainTarget = buildingIndex;
                    return (last.index, bestPath);
                }
                else
                {
                    return (buildingIndex, bestPath);
                }
                #endregion
            }
            else
            {
                #region Without Walls Effect
                Path bestPath = null;
                float distance = 99999;
                int flyCorners = columnCount * rowCount;
                int[] flySlots = new int[flyCorners];
                Vector2Int[] flyStarts = new Vector2Int[flyCorners];
                Vector2Int[] flyGoals = new Vector2Int[flyCorners];
                BattleVector2Int[] flyPositions = new BattleVector2Int[flyCorners];
                int fi = 0;
                for (int x = 0; x < columnCount; x++)
                {
                    int column = x == 0 ? startX : endX;
                    for (int y = 0; y < rowCount; y++)
                    {
                        int row = y == 0 ? startY : endY;
                        if (IsGridPositionValid(column, row))
                        {
                            flyStarts[fi] = new Vector2Int(column, row);
                            flyGoals[fi] = new Vector2Int(unitGridPosition.x, unitGridPosition.y);
                            flyPositions[fi] = new BattleVector2Int(column, row);
                            flySlots[fi] = ScheduleBurstFindLocations(true, flyStarts[fi], flyGoals[fi]);
                        }
                        else
                        {
                            flySlots[fi] = -1;
                        }
                        fi++;
                    }
                }

                FlushBurstRequests();

                fi = 0;
                for (int x = 0; x < columnCount; x++)
                {
                    for (int y = 0; y < rowCount; y++)
                    {
                        if (flySlots[fi] < 0) { fi++; continue; }

                        GetBurstResult(flySlots[fi], out int pointCount, out NativeArray<Vector2Int> flyPath);
                        if (Path.IsValid(flyPath, pointCount, flyStarts[fi], flyGoals[fi]))
                        {
                            float pathLength = GetPathLength(flyPath, pointCount);
                            if (pathLength < distance)
                            {
                                Path path = new Path();
                                if (path.Create(flyPath, pointCount, flyPositions[fi], unitGridPosition))
                                {
                                    path.length = pathLength;
                                    bestPath = path;
                                    distance = pathLength;
                                }
                            }
                        }
                        fi++;
                    }
                }
                if (bestPath != null)
                {
                    bestPath.ReversePoints();
                    return (buildingIndex, bestPath);
                }
                #endregion
            }
            return (-1, null);
        }

        /// <summary>
        /// 为需要破墙的地面单位寻找应先攻击的墙体，以及到该墙体的路径。
        /// </summary>
        private (int, Path) GetPathToWall(int unitIndex)
        {
            BattleVector2Int unitGridPosition = WorldToGridPosition(_units[unitIndex].position);
            FillSortedTargets(unitIndex);
            foreach (var target in sortedTargetBuffer)
            {
                Vector2Int pathStart = new Vector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y);
                Vector2Int pathGoal = new Vector2Int(unitGridPosition.x, unitGridPosition.y);
                int slot1 = ScheduleBurstFindLocations(false, pathStart, pathGoal);
                FlushBurstRequests();
                GetBurstResult(slot1, out int pointCount, out NativeArray<Vector2Int> wallSearchPath);
                if (Path.IsValid(wallSearchPath, pointCount, pathStart, pathGoal))
                {
                    continue;
                }
                else
                {
                    sharedWallUnitIndexBuffer.Clear();
                    sharedWallUnitPositionBuffer.Clear();
                    sharedWallPathStartBuffer.Clear();
                    sharedWallSlotBuffer.Clear();

                    for (int i = 0; i < _units.Count; i++)
                    {
                        if (_units[i].health <= 0 || _units[i].unit.movement != Data.UnitMoveType.ground || i == unitIndex || _units[i].target < 0 || _units[i].mainTarget != target.Key || _units[i].mainTarget < 0 || _buildings[_units[i].mainTarget].building.id != Data.BuildingID.wall || _buildings[_units[i].mainTarget].health <= 0)
                        {
                            continue;
                        }

                        BattleVector2Int pos = WorldToGridPosition(_units[i].position);
                        Vector2Int sharedPathStart = new Vector2Int(pos.x, pos.y);
                        sharedWallUnitIndexBuffer.Add(i);
                        sharedWallUnitPositionBuffer.Add(pos);
                        sharedWallPathStartBuffer.Add(sharedPathStart);
                        sharedWallSlotBuffer.Add(ScheduleBurstFindLocations(false, sharedPathStart, pathGoal));
                    }

                    if (sharedWallSlotBuffer.Count > 0)
                    {
                        FlushBurstRequests();
                        for (int sharedIndex = 0; sharedIndex < sharedWallSlotBuffer.Count; sharedIndex++)
                        {
                            GetBurstResult(sharedWallSlotBuffer[sharedIndex], out int sharedExistingCount, out NativeArray<Vector2Int> sharedPath);
                            if (!Path.IsValid(sharedPath, sharedExistingCount, sharedWallPathStartBuffer[sharedIndex], pathGoal))
                            {
                                continue;
                            }

                            int sourceUnitIndex = sharedWallUnitIndexBuffer[sharedIndex];
                            if (id <= Data.battleGroupWallAttackRadius)
                            {
                                BattleVector2Int pos = sharedWallUnitPositionBuffer[sharedIndex];
                                Vector2Int end = _units[sourceUnitIndex].path.LastPoint;
                                Path p = new Path();
                                int endSlot = ScheduleBurstFindLocations(false, sharedWallPathStartBuffer[sharedIndex], new Vector2Int(end.X, end.Y));
                                FlushBurstRequests();
                                GetBurstResult(endSlot, out int pathToEndCount, out NativeArray<Vector2Int> endPath);
                                if (p.Create(endPath, pathToEndCount, pos, new BattleVector2Int(end.X, end.Y)))
                                {
                                    _units[unitIndex].mainTarget = target.Key;
                                    p.blocks = _units[sourceUnitIndex].path.blocks;
                                    p.length = GetPathLength(p.points, p.pointCount);
                                    return (_units[sourceUnitIndex].target, p);
                                }
                            }
                        }
                    }

                    Path path = new Path();
                    int unlimitedSlot = ScheduleBurstFindLocations(true, pathGoal, pathStart);
                    FlushBurstRequests();
                    GetBurstResult(unlimitedSlot, out int unlimitedCount, out NativeArray<Vector2Int> unlimitedPath);
                    if (path.Create(unlimitedPath, unlimitedCount, unitGridPosition, new BattleVector2Int(_buildings[target.Key].building.x, _buildings[target.Key].building.y)))
                    {
                        path.length = GetPathLength(path.points, path.pointCount);
                        for (int i = 0; i < path.pointCount; i++)
                        {
                            for (int j = 0; j < blockedTiles.Count; j++)
                            {
                                if (blockedTiles[j].position.x == path.points[i].X && blockedTiles[j].position.y == path.points[i].Y)
                                {
                                    if (blockedTiles[j].id == Data.BuildingID.wall && _buildings[blockedTiles[j].index].health > 0)
                                    {
                                        int t = blockedTiles[j].index;
                                        path.pointCount = j;
                                        path.length = GetPathLength(path.points, path.pointCount);
                                        return (t, path);
                                    }
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            return (-1, null);
        }
    }
}
