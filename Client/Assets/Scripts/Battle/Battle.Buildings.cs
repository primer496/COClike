using AStarPathfinding;
using System;
using System.Collections.Generic;

namespace DevelopersHub.ClashOfWhatecer
{
    public partial class Battle
    {
        /// <summary>
        /// 同步指定建筑在 Burst A* blocked 数组中的阻挡状态。
        /// </summary>
        private void SyncBurstBlockedForBuilding(int buildingIndex)
        {
            if (!burstSearchBlocked.IsCreated || grid == null)
            {
                return;
            }

            Data.Building building = _buildings[buildingIndex].building;
            for (int x = building.x; x < building.x + building.columns; x++)
            {
                for (int y = building.y; y < building.y + building.rows; y++)
                {
                    burstSearchBlocked[(y * grid.Width) + x] = grid[x, y].Blocked ? (byte)1 : (byte)0;
                }
            }
        }

        /// <summary>
        /// 对指定建筑造成伤害，建筑被摧毁时自动同步 BurstA* blocked 信息。
        /// </summary>
        private void DamageBuilding(int buildingIndex, float damage)
        {
            float previousHealth = _buildings[buildingIndex].health;
            _buildings[buildingIndex].TakeDamage(damage, ref grid, ref blockedTiles, ref percentage, ref fiftyPercentDestroyed, ref townhallDestroyed, ref completelyDestroyed);
            if (previousHealth > 0f && _buildings[buildingIndex].health <= 0f)
            {
                SyncBurstBlockedForBuilding(buildingIndex);
            }
        }

        /// <summary>
        /// 处理单个防御建筑在当前帧的索敌、视野检查与攻击结算。
        /// </summary>
        private void HandleBuilding(int index, double deltaTime)
        {
            if (_buildings[index].target >= 0)
            {
                if (_units[_buildings[index].target].health <= 0 || !IsUnitInRange(_buildings[index].target, index) || (_units[_buildings[index].target].unit.movement == Data.UnitMoveType.underground && _units[_buildings[index].target].path != null))
                {
                    _buildings[index].target = -1;
                }
                else
                {
                    bool freeze = false;
                    for (int i = 0; i < _spells.Count; i++)
                    {
                        if (_spells[i].done) { continue; }
                        if (_spells[i].spell.id == Data.SpellID.freeze)
                        {
                            double p = GetBuildingInSpellRangePercentage(i, index);
                            if (p > 0)
                            {
                                freeze = true;
                                break;
                            }
                        }
                    }
                    if (!freeze)
                    {
                        if (IsUnitCanBeSeen(_buildings[index].target, index))
                        {
                            _buildings[index].attackTimer += deltaTime;
                            int attacksCount = (int)Math.Floor(_buildings[index].attackTimer / _buildings[index].building.speed);
                            if (attacksCount > 0)
                            {
                                _buildings[index].attackTimer -= (attacksCount * _buildings[index].building.speed);
                                for (int i = 1; i <= attacksCount; i++)
                                {
                                    if (_buildings[index].building.radius > 0 && _buildings[index].building.rangedSpeed > 0)
                                    {
                                        float distance = BattleVector2.Distance(_units[_buildings[index].target].position, _buildings[index].worldCenterPosition);
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.unit;
                                        projectile.target = _buildings[index].target;
                                        projectile.timer = distance / (_buildings[index].building.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = _buildings[index].building.damage;
                                        projectile.splash = _buildings[index].building.splashRange;
                                        projectile.follow = true;
                                        projectile.position = _buildings[index].worldCenterPosition;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _buildings[index].worldCenterPosition, _units[_buildings[index].target].position);
                                        }
                                    }
                                    else
                                    {
                                        _units[_buildings[index].target].TakeDamage(_buildings[index].building.damage);
                                        if (_buildings[index].building.splashRange > 0)
                                        {
                                            for (int j = 0; j < _units.Count; j++)
                                            {
                                                if (j != _buildings[index].target)
                                                {
                                                    float distance = BattleVector2.Distance(_units[j].position, _units[_buildings[index].target].position);
                                                    if (distance < _buildings[index].building.splashRange * Data.gridCellSize)
                                                    {
                                                        _units[j].TakeDamage(_buildings[index].building.damage * (1f - (distance / _buildings[index].building.splashRange * Data.gridCellSize)));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_buildings[index].attackCallback != null)
                                    {
                                        _buildings[index].attackCallback.Invoke(_buildings[index].building.databaseID, _units[_buildings[index].target].unit.databaseID);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _buildings[index].target = -1;
                        }
                    }
                }
            }
            if (_buildings[index].target < 0)
            {
                if (FindTargetForBuilding(index))
                {
                    HandleBuilding(index, deltaTime);
                }
            }
        }

        /// <summary>
        /// 为指定防御建筑寻找一个满足攻击类型限制且可见的单位目标。
        /// </summary>
        private bool FindTargetForBuilding(int index)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health <= 0 || _units[i].unit.movement == Data.UnitMoveType.underground && _units[i].path != null)
                {
                    continue;
                }

                if (_buildings[index].building.targetType == Data.BuildingTargetType.ground && _units[i].unit.movement == Data.UnitMoveType.fly)
                {
                    continue;
                }

                if (_buildings[index].building.targetType == Data.BuildingTargetType.air && _units[i].unit.movement != Data.UnitMoveType.fly)
                {
                    continue;
                }

                if (IsUnitInRange(i, index) && IsUnitCanBeSeen(i, index))
                {
                    _buildings[index].attackTimer = _buildings[index].building.speed;
                    _buildings[index].target = i;
                    return true;
                }
            }
            return false;
        }

        private bool IsUnitInRange(int unitIndex, int buildingIndex)
        {
            float distance = BattleVector2.Distance(_buildings[buildingIndex].worldCenterPosition, _units[unitIndex].position);
            if (distance <= (_buildings[buildingIndex].building.radius * Data.gridCellSize))
            {
                if (_buildings[buildingIndex].building.blindRange > 0 && distance <= _buildings[buildingIndex].building.blindRange)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool IsUnitCanBeSeen(int unitIndex, int buildingIndex)
        {
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.invisibility)
                {
                    float distance = BattleVector2.Distance(_units[unitIndex].position, _spells[i].position);
                    if (distance <= (_spells[i].spell.server.radius * Data.gridCellSize))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 计算建筑落在法术范围内的覆盖率。
        /// </summary>
        private double GetBuildingInSpellRangePercentage(int spellIndex, int buildingIndex)
        {
            double percentage = 0;
            float distance = BattleVector2.Distance(_buildings[buildingIndex].worldCenterPosition, _spells[spellIndex].position);
            float radius = Math.Max(_buildings[buildingIndex].building.columns, _buildings[buildingIndex].building.rows) * Data.gridCellSize / 2f;
            float delta = (_spells[spellIndex].spell.server.radius * Data.gridCellSize) - (distance + radius);
            if (delta >= 0)
            {
                percentage = 1;
            }
            else
            {
                delta = Math.Abs(delta);
                if (delta < radius * 2f)
                {
                    percentage = delta / (radius * 2f);
                }
            }
            return percentage;
        }
    }
}
