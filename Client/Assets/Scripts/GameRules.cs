namespace DevelopersHub.ClashOfWhatecer
{
    using System;
    using System.Collections.Generic;

    // 所有依赖静态配置字段的业务规则与数值计算方法。
    public static partial class Data
    {
        /// <summary>
        /// 检查消息是否允许发送。
        /// </summary>
        public static bool IsMessageGoodToSend(string message)
        {
            // if (message.Contains("fork")) { return false; }
            return true;
        }

        /// <summary>
        /// 根据部落战结果计算获得的部落经验值。
        /// </summary>
        public static int GetClanWarGainedXP(int gainedStars, int enemyGainedStars, int maxStars, bool didWonFirstAttack)
        {
            int xp = 0;
            double percentage = (double)gainedStars / (double)enemyGainedStars;

            if (percentage >= 0.4d) { xp += 10; }
            if (percentage >= 0.6d) { xp += 25; }
            if (gainedStars > enemyGainedStars) { xp += 50; }
            if (didWonFirstAttack) { xp += 10; }

            return xp;
        }

        /// <summary>
        /// 获取部落升到下一等级所需的经验值。
        /// </summary>
        public static int GetClanNexLevelRequiredXp(int currentLevel)
        {
            switch (currentLevel)
            {
                case 1: return 0;
                case 2: return 500;
                case 3: return 1200;
                case 4: return 1900;
                case 5: return 3100;
                case 6: return 3800;
                case 7: return 4500;
                case 8: return 5200;
                case 9: return 5900;
                case 10: return 7900;
                case 11: return 8600;
                case 12: return 9300;
                case 13: return 10000;
                case 14: return 10700;
                case 15: return 15700;
                case 16: return 16400;
                case 17: return 17100;
                case 18: return 17800;
                case 19: return 18500;
                case 20: return 23500;
                case 21: return 24200;
                case 22: return 24900;
                case 23: return 25600;
                case 24: return 26300;
                case 25: return 31300;
                case 26: return 32000;
                case 27: return 32700;
                case 28: return 33400;
                case 29: return 34100;
                case 30: return 39100;
                case 31: return 39800;
                case 32: return 40500;
                case 33: return 41200;
                case 34: return 41900;
                case 35: return 46900;
                case 36: return 47600;
                case 37: return 48300;
                case 38: return 49000;
                case 39: return 49700;
                case 40: return 54700;
                case 41: return 55400;
                case 42: return 56100;
                case 43: return 56800;
                case 44: return 57500;
                case 45: return 62500;
                case 46: return 63200;
                case 47: return 63900;
                case 48: return 64600;
                case 49: return 65300;
                case 50: return 70300;
                case 51: return 71000;
                default: return 99999999;
            }
        }

        /// <summary>
        /// 判断指定兵种是否已经解锁。
        /// </summary>
        public static bool IsUnitUnlocked(UnitID id, int barracksLevel, int darkBarracksLevel)
        {
            switch (id)
            {
                case UnitID.barbarian: return barracksLevel >= 1;
                case UnitID.archer: return barracksLevel >= 2;
                case UnitID.giant: return barracksLevel >= 3;
                case UnitID.goblin: return barracksLevel >= 4;
                case UnitID.wallbreaker: return barracksLevel >= 5;
                case UnitID.balloon: return barracksLevel >= 6;
                case UnitID.wizard: return barracksLevel >= 7;
                case UnitID.healer: return barracksLevel >= 8;
                case UnitID.dragon: return barracksLevel >= 9;
                case UnitID.pekka: return barracksLevel >= 10;
                case UnitID.babydragon: return barracksLevel >= 11;
                case UnitID.miner: return barracksLevel >= 12;
                case UnitID.electrodragon: return barracksLevel >= 13;
                case UnitID.yeti: return barracksLevel >= 14;
                case UnitID.dragonrider: return barracksLevel >= 15;
                case UnitID.electrotitan: return barracksLevel >= 16;
                case UnitID.minion: return darkBarracksLevel >= 1;
                case UnitID.hogrider: return darkBarracksLevel >= 2;
                case UnitID.valkyrie: return darkBarracksLevel >= 3;
                case UnitID.golem: return darkBarracksLevel >= 4;
                case UnitID.witch: return darkBarracksLevel >= 5;
                case UnitID.lavahound: return darkBarracksLevel >= 6;
                case UnitID.bowler: return darkBarracksLevel >= 7;
                case UnitID.icegolem: return darkBarracksLevel >= 8;
                case UnitID.headhunter: return darkBarracksLevel >= 9;
                default: return false;
            }
        }

        /// <summary>
        /// 判断指定法术是否已经解锁。
        /// </summary>
        public static bool IsSpellUnlocked(SpellID id, int spellFactoryLevel, int darkSpellFactoryLevel)
        {
            switch (id)
            {
                case SpellID.lightning: return spellFactoryLevel >= 1;
                case SpellID.healing: return spellFactoryLevel >= 2;
                case SpellID.rage: return spellFactoryLevel >= 3;
                //case SpellID.jump: return spellFactoryLevel >= 4;
                case SpellID.freeze: return spellFactoryLevel >= 4;
                case SpellID.invisibility: return spellFactoryLevel >= 5;
                case SpellID.earthquake: return darkSpellFactoryLevel >= 1;
                case SpellID.haste: return darkSpellFactoryLevel >= 2;
                case SpellID.skeleton: return darkSpellFactoryLevel >= 3;
                case SpellID.bat: return darkSpellFactoryLevel >= 4;
                default: return false;
            }
        }

        /// <summary>
        /// 获取玩家升到下一等级所需的经验值。
        /// </summary>
        public static int GetNexLevelRequiredXp(int currentLevel)
        {
            if (currentLevel == 1) { return 30; }
            else if (currentLevel <= 200) { return (currentLevel - 1) * 50; }
            else if (currentLevel <= 299) { return ((currentLevel - 200) * 500) + 9500; }
            else { return ((currentLevel - 300) * 1000) + 60000; }
        }

        /// <summary>
        /// 获取搜索对手时需要消耗的金币。
        /// </summary>
        public static int GetBattleSearchCost(int townHallLevel)
        {
            switch (townHallLevel)
            {
                case 1: return 10;
                case 2: return 25;
                case 3: return 50;
                case 4: return 100;
                case 5: return 200;
                case 6: return 380;
                case 7: return 420;
                case 8: return 580;
                case 9: return 850;
                case 10: return 1000;
                case 11: return 1500;
                case 12: return 2000;
                case 13: return 4000;
                case 14: return 6000;
                case 15: return 10000;
                default: return 999999;
            }
        }

        /// <summary>
        /// 获取达到当前等级累计获得的总经验值。
        /// </summary>
        public static int GetTotalXpEarned(int currentLevel)
        {
            if (currentLevel == 1) { return 0; }
            else if (currentLevel <= 201) { return ((currentLevel - 1) * (currentLevel - 2) * 25) + 30; }
            else if (currentLevel <= 299) { return ((currentLevel - 200) * (currentLevel - 200) * 250) + (9250 * (currentLevel - 200)) + 985530; }
            else { return ((currentLevel - 300) * (currentLevel - 300) * 500) + (59500 * (currentLevel - 300)) + 4410530; }
        }

        /// <summary>
        /// 计算资源库中金矿或圣水可被掠夺的数量。
        /// </summary>
        public static int GetStorageGoldAndElixirLoot(int townhallLevel, float storage)
        {
            double p = 0;
            switch (townhallLevel)
            {
                case 1: case 2: case 3: case 4: case 5: case 6: p = 0.2d; break;
                case 7: p = 0.18d; break;
                case 8: p = 0.16d; break;
                case 9: p = 0.14d; break;
                case 10: p = 0.12d; break;
                default: p = 0.1d; break;
            }
            return (int)Math.Floor(storage * p);
        }

        /// <summary>
        /// 计算暗黑重油罐中可被掠夺的数量。
        /// </summary>
        public static int GetStorageDarkElixirLoot(int townhallLevel, float storage)
        {
            double p = 0;
            switch (townhallLevel)
            {
                case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 8: p = 0.06d; break;
                case 9: p = 0.05d; break;
                default: p = 0.04d; break;
            }
            return (int)Math.Floor(storage * p);
        }

        /// <summary>
        /// 计算采集类建筑中金矿或圣水可被掠夺的数量。
        /// </summary>
        public static int GetMinesGoldAndElixirLoot(int townhallLevel, float storage)
        {
            return (int)Math.Floor(storage * 0.5d);
        }

        /// <summary>
        /// 计算暗黑重油采集器中可被掠夺的数量。
        /// </summary>
        public static int GetMinesDarkElixirLoot(int townhallLevel, float storage)
        {
            return (int)Math.Floor(storage * 0.75d);
        }

        /// <summary>
        /// 根据进攻方与防守方杯数计算对战胜负的奖惩杯数。
        /// </summary>
        public static (int, int) GetBattleTrophies(int attackerTrophies, int defendderTrophies)
        {
            int win = 0;
            int lose = 0;
            if (attackerTrophies == defendderTrophies)
            {
                win = 30;
                lose = 20;
            }
            else
            {
                double delta = Math.Abs(attackerTrophies - defendderTrophies);
                if (attackerTrophies > defendderTrophies)
                {
                    win = 30 - (int)Math.Floor(delta * (28d / 600d));
                    lose = 20 + (int)Math.Floor(delta * (19d / 600d));
                    if (win < 2) { win = 2; }
                }
                else
                {
                    win = 30 + (int)Math.Floor(delta * (28d / 600d));
                    lose = 20 - (int)Math.Floor(delta * (19d / 600d));
                    if (lose < 1) { lose = 1; }
                }
            }
            return (win, lose);
        }

        /// <summary>
        /// 根据部落战结果计算双方部落奖惩杯数。
        /// </summary>
        public static (int, int) GetWarTrophies(int clan1Trophies, int clan2Trophies, int clan1Stars, int clan2Stars, int maxStars)
        {
            int clan1 = 0;
            int clan2 = 0;
            if (clan1Stars != clan2Stars && (clan1Stars > 0 || clan2Stars > 0))
            {
                double delta = Math.Abs(clan1Trophies - clan2Trophies);
                if (clan1Stars > clan2Stars)
                {
                    double percentage = (double)clan1Stars / (double)maxStars;
                    clan1 = (int)Math.Floor((20 + (clan1Trophies < clan2Trophies ? delta * 0.05d : 0)) * percentage);
                    clan2 = -clan1;
                }
                else
                {
                    double percentage = (double)clan2Stars / (double)maxStars;
                    clan2 = (int)Math.Floor((20 + (clan2Trophies < clan1Trophies ? delta * 0.05d : 0)) * percentage);
                    clan1 = -clan2;
                }
            }
            else
            {
                clan1 = -5;
                clan2 = -5;
            }
            return (clan1, clan2);
        }

        /// <summary>
        /// 将建筑数据转换为战斗模拟使用的建筑列表。
        /// </summary>
        public static List<Battle.Building> BuildingsToBattleBuildings(List<Building> buildings, BattleType type)
        {
            List<Battle.Building> battleBuildings = new List<Battle.Building>();
            int townhallLevel = 1;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id == Data.BuildingID.townhall)
                {
                    townhallLevel = buildings[i].level;
                    break;
                }
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].databaseID != buildings[i].databaseID || buildings[i].id != buildings[i].id || buildings[i].health != buildings[i].health || buildings[i].damage != buildings[i].damage || buildings[i].percentage != buildings[i].percentage)
                {
                    return null;
                }

                Battle.Building building = new Battle.Building();
                building.building = buildings[i];
                if (type == Data.BattleType.war)
                {
                    building.building.x = building.building.warX;
                    building.building.y = building.building.warY;
                }

                if (building.building.x < 0 || building.building.y < 0)
                {
                    continue;
                }

                building.building.x += Data.battleGridOffset;
                building.building.y += Data.battleGridOffset;

                switch (building.building.id)
                {
                    case Data.BuildingID.townhall:
                        building.lootGoldStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        building.lootElixirStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        building.lootDarkStorage = Data.GetStorageDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        break;
                    case Data.BuildingID.goldmine:
                        building.lootGoldStorage = Data.GetMinesGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        break;
                    case Data.BuildingID.goldstorage:
                        building.lootGoldStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        break;
                    case Data.BuildingID.elixirmine:
                        building.lootElixirStorage = Data.GetMinesGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        break;
                    case Data.BuildingID.elixirstorage:
                        building.lootElixirStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        break;
                    case Data.BuildingID.darkelixirmine:
                        building.lootDarkStorage = Data.GetMinesDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        break;
                    case Data.BuildingID.darkelixirstorage:
                        building.lootDarkStorage = Data.GetStorageDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        break;
                }
                battleBuildings.Add(building);
            }
            return battleBuildings;
        }

        /// <summary>
        /// 获取建筑加速所需的资源消耗。
        /// </summary>
        public static int GetBoostResourcesCost(Data.BuildingID id, int level)
        {
            return 20;
        }

        /// <summary>
        /// 获取指定大本营等级下某建筑的数量与等级上限。
        /// </summary>
        public static BuildingCount GetBuildingLimits(int townHallLevel, string globalID)
        {
            if (buildingAvailability == null) return null;
            if (townHallLevel > 0 && townHallLevel < buildingAvailability.Length)
            {
                for (int i = 0; i < buildingAvailability.Length; i++)
                {
                    if (buildingAvailability[i].level == townHallLevel)
                    {
                        for (int j = 0; j < buildingAvailability[i].buildings.Length; j++)
                        {
                            if (buildingAvailability[i].buildings[j].id == globalID)
                            {
                                return CloneClass(buildingAvailability[i].buildings[j]);
                            }
                        }
                        break;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定大本营等级对应的建筑开放配置。
        /// </summary>
        public static BuildingAvailability GetTownHallLimits(int targetTownHallLevel)
        {
            if (buildingAvailability == null) return null;
            for (int i = 0; i < buildingAvailability.Length; i++)
            {
                if (buildingAvailability[i].level == targetTownHallLevel)
                {
                    return CloneClass(buildingAvailability[i]);
                }
            }
            return null;
        }

        /// <summary>
        /// 根据剩余秒数计算立即完成所需宝石数量。
        /// </summary>
        public static int GetInstantBuildRequiredGems(int remainedSeconds)
        {
            int gems = 0;
            if (remainedSeconds > 0)
            {
                if (remainedSeconds <= 60)
                {
                    gems = 1;
                }
                else if (remainedSeconds <= 3600)
                {
                    gems = (int)(0.00537f * ((float)remainedSeconds - 60f)) + 1;
                }
                else if (remainedSeconds <= 86400)
                {
                    gems = (int)(0.00266f * ((float)remainedSeconds - 3600f)) + 20;
                }
                else
                {
                    gems = (int)(0.00143f * ((float)remainedSeconds - 86400f)) + 260;
                }
            }
            return gems;
        }

        /// <summary>
        /// 根据缺少的资源数量计算所需宝石数量。
        /// </summary>
        public static int GetResourceGemCost(int gold, int elixir, int dark)
        {
            if (gold < 0) { gold = 0; }
            if (elixir < 0) { elixir = 0; }
            if (dark < 0) { dark = 0; }
            if (gold <= 0 && elixir <= 0 && dark <= 0)
            {
                return 0;
            }
            else
            {
                return (int)Math.Ceiling(((double)(gold + elixir) * 0.001d + (double)dark * 0.1d));
            }
        }
    }
}
