namespace DevelopersHub.ClashOfWhatecer
{
    using UnityEngine;

    /// <summary>
    /// 全局战斗与游戏玩法静态配置。
    /// 将来可与外部 Excel 表建立导入联系，在此 SO 中直接修改数值即可生效，无需重新编译。
    /// </summary>
    [CreateAssetMenu(fileName = "GameplaySettings", menuName = "Config/Gameplay Settings")]
    public class GameplaySettingsSO : ScriptableObject
    {
        [Header("大本营")]
        public int maxTownHallLevel = 15;

        [Header("最低采集阈值")]
        public int minGoldCollect = 10;
        public int minElixirCollect = 10;
        public int minDarkElixirCollect = 10;

        [Header("战斗时长（秒）")]
        public int battleDuration = 120;
        public int battlePrepDuration = 30;

        [Header("战斗网格")]
        public int gridSize = 45;
        public float gridCellSize = 1f;
        public int battleGridOffset = 2;

        [Header("战斗模拟参数")]
        public float battleFrameRate = 0.05f;
        public int battleTilesWorthOfOneWall = 15;
        public int battleGroupWallAttackRadius = 5;

        [Header("护盾")]
        public int shieldMinutesAmountToBattleLost = 180;

        [Header("账号安全（供客户端 UI 展示用）")]
        public int recoveryCodeExpiration = 300;
        public int confirmationCodeExpiration = 300;
        public int recoveryCodeLength = 6;
    }
}
