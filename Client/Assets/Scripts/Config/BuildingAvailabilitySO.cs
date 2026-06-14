namespace DevelopersHub.ClashOfWhatecer
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 单个建筑在某大本营等级下的数量与等级上限。
    /// </summary>
    [System.Serializable]
    public class BuildingEntry
    {
        public string id = "";
        public int count = 0;
        public int maxLevel = 1;
    }

    /// <summary>
    /// 某大本营等级下所有可建建筑的配置列表。
    /// </summary>
    [System.Serializable]
    public class TownHallBuildingConfig
    {
        public int townHallLevel = 1;
        public List<BuildingEntry> buildings = new List<BuildingEntry>();

        /// <summary>
        /// 查找指定 id 的建筑配置，未找到返回 null。
        /// </summary>
        public BuildingEntry GetEntry(string buildingId)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id == buildingId)
                    return buildings[i];
            }
            return null;
        }
    }

    /// <summary>
    /// 所有大本营等级对应的建筑解锁与等级上限配置表。
    /// 将来可与外部 Excel 表建立导入联系，在此 SO 中直接修改数值即可生效，无需重新编译。
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingAvailability", menuName = "Config/Building Availability")]
    public class BuildingAvailabilitySO : ScriptableObject
    {
        public List<TownHallBuildingConfig> townHallConfigs = new List<TownHallBuildingConfig>();

        /// <summary>
        /// 获取指定大本营等级的配置，未找到返回 null。
        /// </summary>
        public TownHallBuildingConfig GetTownHallConfig(int townHallLevel)
        {
            for (int i = 0; i < townHallConfigs.Count; i++)
            {
                if (townHallConfigs[i].townHallLevel == townHallLevel)
                    return townHallConfigs[i];
            }
            return null;
        }

        /// <summary>
        /// 获取指定大本营等级下某建筑的配置，未找到返回 null。
        /// </summary>
        public BuildingEntry GetBuildingEntry(int townHallLevel, string buildingId)
        {
            var config = GetTownHallConfig(townHallLevel);
            return config?.GetEntry(buildingId);
        }
    }
}
