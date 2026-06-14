using AStarPathfinding;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DevelopersHub.ClashOfWhatecer
{
    public partial class Battle
    {
        // ────────────────────────────────────────────────
        // 委托定义
        // ────────────────────────────────────────────────
        public delegate void SpellSpawned(long databaseID, Data.SpellID id, BattleVector2 target, float radius);
        public delegate void Spawned(long id);
        public delegate void AttackCallback(long index, long target);
        public delegate void IndexCallback(long index);
        public delegate void FloatCallback(long index, float value);
        public delegate void DoubleCallback(long index, double value);
        public delegate void BlankCallback();
        public delegate void ProjectileCallback(int id, BattleVector2 current, BattleVector2 target);

        // ────────────────────────────────────────────────
        // Burst 路径计算辅助类型
        // ────────────────────────────────────────────────

        private struct BurstPathPosition
        {
            public float x;
            public float y;
        }

        [BurstCompile]
        private struct PathLengthJob : IJob
        {
            [ReadOnly] public NativeArray<Vector2Int> path;
            public int pointCount;
            public float cellSize;
            public NativeArray<float> result;

            public void Execute()
            {
                float length = 0f;
                if (!path.IsCreated || pointCount <= 1)
                {
                    result[0] = 0f;
                    return;
                }

                for (int i = 1; i < pointCount; i++)
                {
                    float2 a = new float2(path[i - 1].X, path[i - 1].Y);
                    float2 b = new float2(path[i].X, path[i].Y);
                    length += math.distance(a, b);
                }

                result[0] = length * cellSize;
            }
        }

        [BurstCompile]
        private struct PathPositionJob : IJob
        {
            [ReadOnly] public NativeArray<Vector2Int> path;
            public int pointCount;
            public float t;
            public float cellSize;
            public NativeArray<BurstPathPosition> result;

            public void Execute()
            {
                BurstPathPosition value = default;
                if (!path.IsCreated || pointCount <= 0)
                {
                    result[0] = value;
                    return;
                }

                if (pointCount == 1)
                {
                    value.x = path[0].X * cellSize + cellSize * 0.5f;
                    value.y = path[0].Y * cellSize + cellSize * 0.5f;
                    result[0] = value;
                    return;
                }

                t = math.clamp(t, 0f, 1f);
                float totalLength = 0f;
                for (int i = 1; i < pointCount; i++)
                {
                    float2 a = new float2(path[i - 1].X, path[i - 1].Y);
                    float2 b = new float2(path[i].X, path[i].Y);
                    totalLength += math.distance(a, b);
                }
                totalLength *= cellSize;
                if (totalLength <= 0f)
                {
                    value.x = path[0].X * cellSize + cellSize * 0.5f;
                    value.y = path[0].Y * cellSize + cellSize * 0.5f;
                    result[0] = value;
                    return;
                }

                float length = 0f;
                for (int i = 1; i < pointCount; i++)
                {
                    float2 a = new float2(path[i - 1].X, path[i - 1].Y);
                    float2 b = new float2(path[i].X, path[i].Y);
                    float segmentLength = math.distance(a, b) * cellSize;
                    float progress = (length + segmentLength) / totalLength;
                    if (progress >= t)
                    {
                        float previous = length / totalLength;
                        float factor = (t - previous) / (progress - previous);
                        float2 worldA = a * cellSize + new float2(cellSize * 0.5f, cellSize * 0.5f);
                        float2 worldB = b * cellSize + new float2(cellSize * 0.5f, cellSize * 0.5f);
                        float2 world = math.lerp(worldA, worldB, factor);
                        value.x = world.x;
                        value.y = world.y;
                        result[0] = value;
                        return;
                    }
                    length += segmentLength;
                }

                value.x = path[0].X * cellSize + cellSize * 0.5f;
                value.y = path[0].Y * cellSize + cellSize * 0.5f;
                result[0] = value;
            }
        }

        private static class BurstPathMath
        {
            public static float GetPathLength(NativeArray<Vector2Int> path, int pointCount, float cellSize, NativeArray<float> result)
            {
                new PathLengthJob
                {
                    path = path,
                    pointCount = pointCount,
                    cellSize = cellSize,
                    result = result,
                }.Run();
                return result[0];
            }

            public static BurstPathPosition GetPathPosition(NativeArray<Vector2Int> path, int pointCount, float t, float cellSize, NativeArray<BurstPathPosition> result)
            {
                new PathPositionJob
                {
                    path = path,
                    pointCount = pointCount,
                    t = t,
                    cellSize = cellSize,
                    result = result,
                }.Run();
                return result[0];
            }
        }

        // ────────────────────────────────────────────────
        // 战斗实体类型
        // ────────────────────────────────────────────────

        public class Projectile
        {
            public int id = 0;
            public int target = -1;
            public float damage = 0;
            public float splash = 0;
            public float timer = 0;
            public TargetType type = TargetType.unit;
            public bool heal = false;
            public bool follow = true;
            public BattleVector2 position = new BattleVector2();
        }

        public enum TargetType
        {
            unit, building
        }

        public class Tile
        {
            public Tile(Data.BuildingID id, BattleVector2Int position, int index = -1)
            {
                this.id = id;
                this.position = position;
                this.index = index;
            }
            public Data.BuildingID id;
            public BattleVector2Int position;
            public int index = -1;
        }

        public class Spell
        {
            public Data.Spell spell = null;
            public IndexCallback pulseCallback = null;
            public IndexCallback doneCallback = null;
            public BattleVector2 position;
            public bool done = false;
            public int palsesDone = 0;
            public double palsesTimer = 0;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public void Initialize(int x, int y)
            {
                if (spell == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }

        public class Unit
        {
            public Data.Unit unit = null;
            public float health = 0;
            public int target = -1;
            public int mainTarget = -1;
            public BattleVector2 position;
            public BattleVector2 positionOnGrid { get { return new BattleVector2(position.x - Data.battleGridOffset, position.y - Data.battleGridOffset); } }
            public Path path = null;
            public double pathTime = 0;
            public double pathTraveledTime = 0;
            public double attackTimer = 0;
            public bool moving = false;
            public Dictionary<int, float> resourceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> defenceTargets = new Dictionary<int, float>();
            public Dictionary<int, float> otherTargets = new Dictionary<int, float>();
            public AttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
            public void ClearPath()
            {
                if (path != null)
                {
                    path.Dispose();
                    path = null;
                }
            }
            public void AssignTarget(int target, Path path)
            {
                attackTimer = unit.attackSpeed;
                if (!ReferenceEquals(this.path, path))
                {
                    ClearPath();
                }
                this.target = target;
                this.path = path;
                if (path != null)
                {
                    pathTraveledTime = 0;
                    pathTime = path.length / (unit.moveSpeed * Data.gridCellSize);
                }
                else
                {
                    pathTraveledTime = 0;
                    pathTime = 0;
                }
                if(targetCallback != null)
                {
                    targetCallback.Invoke(unit.databaseID);
                }
            }
            public void AssignHealerTarget(int target, float distance)
            {
                ClearPath();
                attackTimer = unit.attackSpeed;
                this.target = target;
                pathTraveledTime = 0;
                pathTime = distance / (unit.moveSpeed * Data.gridCellSize);
            }
            public void TakeDamage(float damage)
            {
                if (health <= 0) { return; }
                health -= damage;
                if (damageCallback != null)
                {
                    damageCallback.Invoke(unit.databaseID, damage);
                }
                if (health < 0) { health = 0; }
                if (health <= 0)
                {
                    ClearPath();
                    if (dieCallback != null)
                    {
                        dieCallback.Invoke(unit.databaseID);
                    }
                }
            }
            public void Heal(float amount)
            {
                if (amount <= 0 || health <= 0) { return; }
                health += amount;
                if (health > unit.health) { health = unit.health; }
                if (healCallback != null)
                {
                    healCallback.Invoke(unit.databaseID, amount);
                }
            }
            public void Initialize(int x, int y)
            {
                if (unit == null) { return; }
                position = GridToWorldPosition(new BattleVector2Int(x, y));
            }
        }

        public class Building
        {
            public Data.Building building = null;
            public float health = 0;
            public int target = -1;
            public double attackTimer = 0;
            public double percentage = 0;
            public BattleVector2 worldCenterPosition;
            public AttackCallback attackCallback = null;
            public DoubleCallback destroyCallback = null;
            public FloatCallback damageCallback = null;
            public BlankCallback starCallback = null;

            public int lootGoldStorage = 0;
            public int lootElixirStorage = 0;
            public int lootDarkStorage = 0;

            public int lootedGold = 0;
            public int lootedElixir = 0;
            public int lootedDark = 0;

            public void TakeDamage(float damage, ref Grid grid, ref List<Tile> blockedTiles, ref double percentage, ref bool fiftySatar, ref bool hallStar, ref bool allStar)
            {
                if (health <= 0) { return; }
                health -= damage;
                if (damageCallback != null)
                {
                    damageCallback.Invoke(building.databaseID, damage);
                }
                if (health < 0) { health = 0; }

                double loot = 1d - ((double)health / (double)building.health);
                if (lootGoldStorage > 0) { lootedGold = (int)Math.Floor(lootGoldStorage * loot); }
                if (lootElixirStorage > 0) { lootedElixir = (int)Math.Floor(lootElixirStorage * loot); }
                if (lootDarkStorage > 0) { lootedDark = (int)Math.Floor(lootDarkStorage * loot); }

                if (health <= 0)
                {
                    for (int x = building.x; x < building.x + building.columns; x++)
                    {
                        for (int y = building.y; y < building.y + building.rows; y++)
                        {
                            grid[x, y].Blocked = false;
                            for (int i = 0; i < blockedTiles.Count; i++)
                            {
                                if (blockedTiles[i].position.x == x && blockedTiles[i].position.y == y)
                                {
                                    blockedTiles.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                    if (this.percentage > 0)
                    {
                        percentage += this.percentage;
                    }
                    if (destroyCallback != null)
                    {
                        destroyCallback.Invoke(building.databaseID, this.percentage);
                    }
                    if (building.id == Data.BuildingID.townhall && !hallStar)
                    {
                        hallStar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                    int p = (int)Math.Floor(percentage * 100d);
                    if (p >= 50 && !fiftySatar)
                    {
                        fiftySatar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                    if (p >= 100 && !allStar)
                    {
                        allStar = true;
                        if (starCallback != null)
                        {
                            starCallback.Invoke();
                        }
                    }
                }
            }
            public void Initialize()
            {
                health = building.health;
                percentage = building.percentage;
                lootedGold = 0;
                lootedElixir = 0;
                lootedDark = 0;
            }
        }

        public class UnitToAdd
        {
            public Unit unit = null;
            public int x;
            public int y;
            public Spawned callback = null;
            public AttackCallback attackCallback = null;
            public IndexCallback dieCallback = null;
            public FloatCallback damageCallback = null;
            public FloatCallback healCallback = null;
            public IndexCallback targetCallback = null;
        }

        public class SpellToAdd
        {
            public Spell spell = null;
            public int x;
            public int y;
            public SpellSpawned callback = null;
        }

        // ────────────────────────────────────────────────
        // 路径数据结构
        // ────────────────────────────────────────────────

        public class Path
        {
            public Path()
            {
                length = 0;
                points = default;
                blocks = new List<Tile>();
            }
            public bool Create(IList<Vector2Int> result, BattleVector2Int start, BattleVector2Int end)
            {
                if (!IsValid(result, new Vector2Int(start.x, start.y), new Vector2Int(end.x, end.y)))
                {
                    Dispose();
                    pointCount = 0;
                    return false;
                }
                else
                {
                    blocks.Clear();
                    int newPointCount = result.Count;
                    Dispose();
                    pointCount = newPointCount;
                    points = new NativeArray<Vector2Int>(pointCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < pointCount; i++)
                    {
                        points[i] = result[i];
                    }
                    this.start.x = start.x;
                    this.start.y = start.y;
                    this.end.x = end.x;
                    this.end.y = end.y;
                    return true;
                }
            }
            public bool Create(NativeArray<Vector2Int> result, int resultLength, BattleVector2Int start, BattleVector2Int end)
            {
                if (!IsValid(result, resultLength, new Vector2Int(start.x, start.y), new Vector2Int(end.x, end.y)))
                {
                    Dispose();
                    pointCount = 0;
                    return false;
                }

                blocks.Clear();
                Dispose();
                pointCount = resultLength;
                points = new NativeArray<Vector2Int>(pointCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeArray<Vector2Int>.Copy(result, points, pointCount);
                this.start.x = start.x;
                this.start.y = start.y;
                this.end.x = end.x;
                this.end.y = end.y;
                return true;
            }
            public static bool IsValid(IList<Vector2Int> points, Vector2Int start, Vector2Int end)
            {
                if (points == null || points.Count <= 0)
                {
                    return false;
                }
                if (!points[points.Count - 1].Equals(end) || !points[0].Equals(start))
                {
                    return false;
                }
                return true;
            }
            public static bool IsValid(NativeArray<Vector2Int> points, int pointCount, Vector2Int start, Vector2Int end)
            {
                if (!points.IsCreated || pointCount <= 0 || pointCount > points.Length)
                {
                    return false;
                }
                if (!points[pointCount - 1].Equals(end) || !points[0].Equals(start))
                {
                    return false;
                }
                return true;
            }
            public void Dispose()
            {
                if (points.IsCreated)
                {
                    points.Dispose();
                }
                pointCount = 0;
            }
            public Vector2Int LastPoint => points[pointCount - 1];
            public void ReversePoints()
            {
                if (!points.IsCreated || pointCount <= 1)
                {
                    return;
                }
                int half = pointCount / 2;
                for (int i = 0; i < half; i++)
                {
                    int j = pointCount - 1 - i;
                    Vector2Int temp = points[i];
                    points[i] = points[j];
                    points[j] = temp;
                }
            }
            public BattleVector2Int start;
            public BattleVector2Int end;
            public NativeArray<Vector2Int> points;
            public int pointCount = 0;
            public float length = 0;
            public List<Tile> blocks = null;
        }

        // ────────────────────────────────────────────────
        // 向量类型
        // ────────────────────────────────────────────────

        public struct BattleVector2
        {
            public float x;
            public float y;

            public BattleVector2(float x, float y) { this.x = x; this.y = y; }

            public static BattleVector2 LerpUnclamped(BattleVector2 a, BattleVector2 b, float t)
            {
                return new BattleVector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
            }

            public static float Distance(BattleVector2 a, BattleVector2 b)
            {
                float diff_x = a.x - b.x;
                float diff_y = a.y - b.y;
                return (float)Math.Sqrt(diff_x * diff_x + diff_y * diff_y);
            }

            public static float Distance(BattleVector2Int a, BattleVector2Int b)
            {
                return Distance(new BattleVector2(a.x, a.y), new BattleVector2(b.x, b.y));
            }

            /// <summary>
            /// 以指定速度将一个坐标平滑推进到目标坐标。
            /// </summary>
            /// <param name="source">起始坐标。</param>
            /// <param name="target">目标坐标。</param>
            /// <param name="speed">每秒移动距离，调用方无需额外乘以 deltaTime。</param>
            public static BattleVector2 LerpStatic(BattleVector2 source, BattleVector2 target, float deltaTime, float speed)
            {
                if ((source.x == target.x && source.y == target.y) || speed <= 0) { return source; }
                float distance = Distance(source, target);
                float t = speed * deltaTime;
                if (t > distance) { t = distance; }
                return LerpUnclamped(source, target, distance == 0 ? 1 : t / distance);
            }
        }

        public struct BattleVector2Int
        {
            public int x;
            public int y;

            public BattleVector2Int(int x, int y) { this.x = x; this.y = y; }
        }
    }
}
