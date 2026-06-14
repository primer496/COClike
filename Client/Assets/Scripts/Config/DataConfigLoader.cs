namespace DevelopersHub.ClashOfWhatecer
{
    using UnityEngine;

    /// <summary>
    /// 在游戏启动时（早于第一个场景加载）从 Resources/Config/ 下读取所有 SO 资产，
    /// 并将值写回 Data 静态字段，使全局代码行为与 SO 配置保持一致。
    /// SO 资产路径规则：Assets/Resources/Config/<FileName>.asset
    /// </summary>
    public static class DataConfigLoader
    {
        private static GameplaySettingsSO _gameplay;
        private static ClanSettingsSO _clan;
        private static BuildingAvailabilitySO _buildingAvailability;

        public static GameplaySettingsSO Gameplay => _gameplay;
        public static ClanSettingsSO Clan => _clan;
        public static BuildingAvailabilitySO BuildingAvailability => _buildingAvailability;
        /// 在游戏启动时自动调用，加载 SO 配置并覆盖 Data.cs 中的默认值。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LoadGameplaySettings();
            LoadClanSettings();
            LoadBuildingAvailability();
        }

        private static void LoadGameplaySettings()
        {
            _gameplay = Resources.Load<GameplaySettingsSO>("Config/GameplaySettings");
            if (_gameplay == null)
            {
                Debug.LogWarning("[DataConfigLoader] GameplaySettings SO 未找到，使用 Data.cs 默认值。路径：Resources/Config/GameplaySettings");
                return;
            }

            Data.maxTownHallLevel               = _gameplay.maxTownHallLevel;
            Data.minGoldCollect                 = _gameplay.minGoldCollect;
            Data.minElixirCollect               = _gameplay.minElixirCollect;
            Data.minDarkElixirCollect           = _gameplay.minDarkElixirCollect;
            Data.battleDuration                 = _gameplay.battleDuration;
            Data.battlePrepDuration             = _gameplay.battlePrepDuration;
            Data.gridSize                       = _gameplay.gridSize;
            Data.gridCellSize                   = _gameplay.gridCellSize;
            Data.battleFrameRate                = _gameplay.battleFrameRate;
            Data.battleTilesWorthOfOneWall      = _gameplay.battleTilesWorthOfOneWall;
            Data.battleGroupWallAttackRadius    = _gameplay.battleGroupWallAttackRadius;
            Data.battleGridOffset               = _gameplay.battleGridOffset;
            Data.shieldMinutesAmountToBattleLost = _gameplay.shieldMinutesAmountToBattleLost;
            Data.recoveryCodeExpiration         = _gameplay.recoveryCodeExpiration;
            Data.confirmationCodeExpiration     = _gameplay.confirmationCodeExpiration;
            Data.recoveryCodeLength             = _gameplay.recoveryCodeLength;
        }

        private static void LoadClanSettings()
        {
            _clan = Resources.Load<ClanSettingsSO>("Config/ClanSettings");
            if (_clan == null)
            {
                Debug.LogWarning("[DataConfigLoader] ClanSettings SO 未找到，使用 Data.cs 默认值。路径：Resources/Config/ClanSettings");
                return;
            }

            Data.clanMaxMembers                              = _clan.clanMaxMembers;
            Data.clansPerPage                                = _clan.clansPerPage;
            Data.clanNameMinLength                           = _clan.clanNameMinLength;
            Data.clanJoinTimeGapHours                        = _clan.clanJoinTimeGapHours;
            Data.clanCreatePrice                             = _clan.clanCreatePrice;
            Data.clanWarAttacksPerPlayer                     = _clan.clanWarAttacksPerPlayer;
            Data.clanWarPrepHours                            = _clan.clanWarPrepHours;
            Data.clanWarBattleHours                          = _clan.clanWarBattleHours;
            Data.clanWarMatchMinPercentage                   = _clan.clanWarMatchMinPercentage;
            Data.clanWarMatchTownHallEffectPercentage        = _clan.clanWarMatchTownHallEffectPercentage;
            Data.clanWarMatchSpellFactoryEffectPercentage    = _clan.clanWarMatchSpellFactoryEffectPercentage;
            Data.clanWarMatchDarkSpellFactoryEffectPercentage= _clan.clanWarMatchDarkSpellFactoryEffectPercentage;
            Data.clanWarMatchBarracksEffectPercentage        = _clan.clanWarMatchBarracksEffectPercentage;
            Data.clanWarMatchDarkBarracksEffectPercentage    = _clan.clanWarMatchDarkBarracksEffectPercentage;
            Data.clanWarMatchCampsEffectPercentage           = _clan.clanWarMatchCampsEffectPercentage;
            Data.clanRanksWithEditPermission                 = _clan.clanRanksWithEditPermission;
            Data.clanRanksWithWarPermission                  = _clan.clanRanksWithWarPermission;
            Data.clanRanksWithKickMembersPermission          = _clan.clanRanksWithKickMembersPermission;
            Data.clanRanksWithAcceptJoinRequstsPermission    = _clan.clanRanksWithAcceptJoinRequestsPermission;
            Data.clanRanksWithPromoteMembersPermission       = _clan.clanRanksWithPromoteMembersPermission;
            Data.clanWarAvailableCounts                      = _clan.clanWarAvailableCounts;
            Data.globalChatArchiveMaxMessages                = _clan.globalChatArchiveMaxMessages;
            Data.clanChatArchiveMaxMessages                  = _clan.clanChatArchiveMaxMessages;
            Data.chatSyncPeriod                              = _clan.chatSyncPeriod;
        }

        private static void LoadBuildingAvailability()
        {
            _buildingAvailability = Resources.Load<BuildingAvailabilitySO>("Config/BuildingAvailability");
            if (_buildingAvailability == null)
            {
                Debug.LogWarning("[DataConfigLoader] BuildingAvailability SO 未找到，使用 Data.cs 默认值。路径：Resources/Config/BuildingAvailability");
                return;
            }

            // 将 SO 数据转换为 Data.BuildingAvailability[] 供现有静态方法（GetBuildingLimits / GetTownHallLimits）使用
            var configs = _buildingAvailability.townHallConfigs;
            var result = new Data.BuildingAvailability[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                var src = configs[i];
                var entries = new Data.BuildingCount[src.buildings.Count];
                for (int j = 0; j < src.buildings.Count; j++)
                {
                    entries[j] = new Data.BuildingCount
                    {
                        id       = src.buildings[j].id,
                        count    = src.buildings[j].count,
                        maxLevel = src.buildings[j].maxLevel,
                    };
                }
                result[i] = new Data.BuildingAvailability
                {
                    level     = src.townHallLevel,
                    buildings = entries,
                };
            }
            Data.buildingAvailability = result;
        }
    }
}
