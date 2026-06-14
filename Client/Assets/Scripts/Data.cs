namespace DevelopersHub.ClashOfWhatecer
{
    /// <summary>
    /// 核心数据访问类，通过 partial 拆分到以下文件：
    /// - DataEnums.cs   — 所有枚举
    /// - DataModels.cs  — 所有 DTO / 数据结构类
    /// - DataUtils.cs   — 序列化、压缩、字符串工具方法
    /// - GameRules.cs   — 业务规则与数值计算方法
    /// 本文件仅保留全局配置字段（由 DataConfigLoader 在运行时从 SO 注入）。
    /// </summary>
    public static partial class Data
    {
        // 全局玩法调优参数，供经济、战斗和部落系统共享。
        // 以下字段在运行时由 DataConfigLoader 从对应 ScriptableObject 资产覆写；默认值作为未加载 SO 时的兜底。
        public static int maxTownHallLevel = 10;
        public static int minGoldCollect = 10;
        public static int minElixirCollect = 10;
        public static int minDarkElixirCollect = 10;
        public static int battleDuration = 120;
        public static int battlePrepDuration = 30;
        public static int gridSize = 45;
        public static float gridCellSize = 1;

        public static float battleFrameRate = 0.05f;
        public static int battleTilesWorthOfOneWall = 15;
        public static int battleGroupWallAttackRadius = 5;
        public static int battleGridOffset = 2;
        public static int shieldMinutesAmountToBattleLost = 180;

        public static int clanMaxMembers = 50;
        public static int clansPerPage = 20;
        public static int clanNameMinLength = 3;
        public static int clanJoinTimeGapHours = 24;
        public static int clanCreatePrice = 40000;
        public static int clanWarAttacksPerPlayer = 2;
        public static int clanWarPrepHours = 24;
        public static int clanWarBattleHours = 24;
        public static double clanWarMatchMinPercentage = 0.70d;

        public static double clanWarMatchTownHallEffectPercentage = 0.60d;
        public static double clanWarMatchSpellFactoryEffectPercentage = 0.05d;
        public static double clanWarMatchDarkSpellFactoryEffectPercentage = 0.05d;
        public static double clanWarMatchBarracksEffectPercentage = 0.05d;
        public static double clanWarMatchDarkBarracksEffectPercentage = 0.05d;
        public static double clanWarMatchCampsEffectPercentage = 0.20d;

        public static int[] clanRanksWithEditPermission = { 1, 2 };
        public static int[] clanRanksWithWarPermission = { 1, 2 };
        public static int[] clanRanksWithKickMembersPermission = { 1, 2 };
        public static int[] clanRanksWithAcceptJoinRequstsPermission = { 1, 2 };
        public static int[] clanRanksWithPromoteMembersPermission = { 1, 2 };
        public static int[] clanWarAvailableCounts = { 5, 10, 15, 20 };

        public static int globalChatArchiveMaxMessages = 30;
        public static int clanChatArchiveMaxMessages = 30;
        public static int chatSyncPeriod = 2;

        public static readonly string mysqlDateTimeFormat = "%Y-%m-%d %H:%i:%s"; // 服务端专用，保持不变

        public static int recoveryCodeExpiration = 300;
        public static int confirmationCodeExpiration = 300;
        public static int recoveryCodeLength = 6;

        /// <summary>各大本营等级的建筑解锁配置。由 DataConfigLoader 从 BuildingAvailabilitySO 资产填充。</summary>
        public static BuildingAvailability[] buildingAvailability = null;
    }
}