namespace DevelopersHub.ClashOfWhatecer
{
    using UnityEngine;

    /// <summary>
    /// 部落系统静态配置。
    /// 将来可与外部 Excel 表建立导入联系，在此 SO 中直接修改数值即可生效，无需重新编译。
    /// </summary>
    [CreateAssetMenu(fileName = "ClanSettings", menuName = "Config/Clan Settings")]
    public class ClanSettingsSO : ScriptableObject
    {
        [Header("部落规模")]
        public int clanMaxMembers = 50;
        public int clansPerPage = 20;
        public int clanNameMinLength = 3;
        public int clanJoinTimeGapHours = 24;
        public int clanCreatePrice = 40000;

        [Header("部落战规则")]
        public int clanWarAttacksPerPlayer = 2;
        public int clanWarPrepHours = 24;
        public int clanWarBattleHours = 24;
        [Range(0f, 1f)] public float clanWarMatchMinPercentage = 0.70f;

        [Header("部落战匹配权重（各项之和应为 1）")]
        [Range(0f, 1f)] public float clanWarMatchTownHallEffectPercentage = 0.60f;
        [Range(0f, 1f)] public float clanWarMatchSpellFactoryEffectPercentage = 0.05f;
        [Range(0f, 1f)] public float clanWarMatchDarkSpellFactoryEffectPercentage = 0.05f;
        [Range(0f, 1f)] public float clanWarMatchBarracksEffectPercentage = 0.05f;
        [Range(0f, 1f)] public float clanWarMatchDarkBarracksEffectPercentage = 0.05f;
        [Range(0f, 1f)] public float clanWarMatchCampsEffectPercentage = 0.20f;

        [Header("部落战可选人数")]
        public int[] clanWarAvailableCounts = { 5, 10, 15, 20 };

        [Header("权限等级（rank 值，1=leader 2=coleader）")]
        public int[] clanRanksWithEditPermission = { 1, 2 };
        public int[] clanRanksWithWarPermission = { 1, 2 };
        public int[] clanRanksWithKickMembersPermission = { 1, 2 };
        public int[] clanRanksWithAcceptJoinRequestsPermission = { 1, 2 };
        public int[] clanRanksWithPromoteMembersPermission = { 1, 2 };

        [Header("聊天归档")]
        public int globalChatArchiveMaxMessages = 30;
        public int clanChatArchiveMaxMessages = 30;
        public int chatSyncPeriod = 2;
    }
}
