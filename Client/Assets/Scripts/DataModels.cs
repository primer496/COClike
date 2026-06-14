namespace DevelopersHub.ClashOfWhatecer
{
    using System;
    using System.Collections.Generic;

    public static partial class Data
    {
        // 轻量级 DTO，供 UI 页面和网络载荷使用。
        public class PlayersRanking
        {
            public int page = 1;
            public int pagesCount = 1;
            public List<PlayerRank> players = new List<PlayerRank>();
        }

        public class PlayerRank
        {
            public long id = 0;
            public int rank = 0;
            public string name = "";
            public int trophies = 0;
            public int xp = 0;
            public int level = 0;
        }

        public class Research
        {
            public long id;
            public ResearchType type;
            public string globalID;
            public int level;
            public bool researching;
            public DateTime end;
        }

        public class CharMessage
        {
            public long id = 0;
            public long accountID = 0;
            public string name = "";
            public Data.ChatType type = 0;
            public long globalID = 0;
            public long clanID = 0;
            public string message = "";
            public string color = "";
            public string time = "";
        }

        public class JoinRequest
        {
            public long id = 0;
            public long accountID = 0;
            public string name = "";
            public int level = 1;
            public int trophies = 0;
            public DateTime time;
        }

        public class ClansList
        {
            public int page = 1;
            public int pagesCount = 1;
            public List<Data.Clan> clans = new List<Clan>();
        }

        public class ClanWarSearch
        {
            public long id = 0;
            public long clan = 0;
            public long player = 0;
            public DateTime time;
            public List<ClanWarSearchMember> members = null;
            public List<long> notMatch = new List<long>();
            public int match = -1;
            public bool handled = false;
        }

        public class ClanWarData
        {
            public long id = 0;
            public bool searching = false;
            public int count = 0;
            public string starter = "";
            public long clan1ID = 0;
            public long clan2ID = 0;
            public long winnerID = 0;
            public int size = 0;
            public bool hasReport = false;
            public int clan1Stars = 0;
            public int clan2Stars = 0;
            public int maxStars = 0;
            public DateTime startTime;
            public Clan clan1 = null;
            public Clan clan2 = null;
        }

        public class ClanWarAttack
        {
            public long id = 0;
            public DateTime start;
            public long attacker = 0;
            public long defender = 0;
            public int stars = 0;
            public int gold = 0;
            public int elixir = 0;
            public int dark = 0;
            public bool starsCounted = false;
        }

        public class ClanWarSearchMember
        {
            public int tempPosition = -1;
            public int warPosition = -1;
            public ClanMember data = new ClanMember();
            public List<Building> Buildings = new List<Building>();

            public int wallsPower = 0;
            public int defencePower = 0;

            public int townHall = 0;
            public int spellFactory = 0;
            public int darkSpellFactory = 0;
            public int barracks = 0;
            public int darkBarracks = 0;
            public int campsCapacity = 0;
        }

        public class Clan
        {
            public long id = 0;
            public string name = "Clan";
            public ClanJoinType joinType = ClanJoinType.AnyoneCanJoin;
            public int level = 1;
            public int xp = 0;
            public int rank = 0;
            public int trophies = 0;
            public int minTrophies = 0;
            public int minTownhallLevel = 0;
            public int pattern = 0;
            public int background = 0;
            public string patternColor = "";
            public string backgroundColor = "";
            public List<ClanMember> members = new List<ClanMember>();
            public ClanWar war = new ClanWar();
        }

        public class ClanWar
        {
            public long id = 0;
            public long clan1 = 0;
            public long clan2 = 0;
            public int stage = 0;
            public DateTime start;
            public List<ClanWarAttack> attacks = new List<ClanWarAttack>();
        }

        public class ClanMember
        {
            public long id = 0;
            public string name = "Player";
            public int level = 1;
            public int xp = 0;
            public int rank = 0;
            public int trophies = 0;
            public int townHallLevel = 1;
            public bool online = false;
            public long clanID = 0;
            public int clanRank = 0;
            public long warID = 0;
            public int warPos = 0;
        }

        public class Player
        {
            public long id = 0;
            public string name = "Player";
            public int gems = 0;
            public int trophies = 0;
            public bool banned = false;
            public DateTime nowTime;
            public DateTime shield;
            public int xp = 0;
            public int level = 1;
            public DateTime clanTimer;
            public long clanID = 0;
            public int clanRank = 0;
            public long warID = 0;
            public string email = "";
            public int layout = 0;
            public DateTime shield1;
            public DateTime shield2;
            public DateTime shield3;
            public List<Building> buildings = new List<Building>();
            public List<Unit> units = new List<Unit>();
            public List<Spell> spells = new List<Spell>();
        }

        public class ServerSpell
        {
            public long databaseID = 0;
            public SpellID id = SpellID.lightning;
            public int level = 0;
            public int requiredGold = 0;
            public int requiredElixir = 0;
            public int requiredGems = 0;
            public int requiredDarkElixir = 0;
            public int brewTime = 0;
            public int housing = 1;
            public float radius = 0;
            public int pulsesCount = 0;
            public float pulsesDuration = 0;
            public float pulsesValue = 0;
            public float pulsesValue2 = 0;
            public int researchTime = 0;
            public int researchGold = 0;
            public int researchElixir = 0;
            public int researchDarkElixir = 0;
            public int researchGems = 0;
            public int researchXp = 0;
        }

        public class Spell
        {
            public long databaseID = 0;
            public SpellID id = SpellID.lightning;
            public int level = 0;
            public int hosing = 1;
            public bool brewed = false;
            public bool ready = false;
            public int brewTime = 0;
            public float brewedTime = 0;
            public int housing = 1;
            public ServerSpell server = null;
        }

        // 战斗快照类用于衔接实时模拟与回放/战报数据。
        public class BattleFrame
        {
            public int frame = 0;
            public List<BattleFrameUnit> units = new List<BattleFrameUnit>();
            public List<BattleFrameSpell> spells = new List<BattleFrameSpell>();
        }

        public class BattleReport
        {
            public long attacker = 0;
            public long defender = 0;
            public int totalFrames = 0;
            public BattleType type = BattleType.normal;
            public List<Building> buildings = new List<Building>();
            public List<BattleFrame> frames = new List<BattleFrame>();
        }

        public class BattleReportItem
        {
            public long id = 0;
            public long attacker = 0;
            public long defender = 0;
            public string username = "";
            public DateTime time;
            public int stars = 0;
            public int trophies = 0;
            public int gold = 0;
            public int elixir = 0;
            public int dark = 0;
            public bool seen = false;
            public bool hasReply = false;
        }

        public class BattleFrameUnit
        {
            public long id = 0;
            public int x = 0;
            public int y = 0;
            public Unit unit = null;
        }

        public class BattleFrameSpell
        {
            public long id = 0;
            public int x = 0;
            public int y = 0;
            public Spell spell = null;
        }

        public class BattleData
        {
            public Battle battle = null;
            public BattleType type = BattleType.normal;
            public List<BattleFrame> savedFrames = new List<BattleFrame>();
            public List<BattleFrame> frames = new List<BattleFrame>();
        }

        public class OpponentData
        {
            public long id = 0;
            public Data.Player data = null;
            public List<Building> buildings = null;
        }

        public class BattleStartBuildingData
        {
            public BuildingID id = BuildingID.townhall;
            public long databaseID = 0;
            public int lootGoldStorage = 0;
            public int lootElixirStorage = 0;
            public int lootDarkStorage = 0;
        }

        public class InitializationData
        {
            public long accountID = 0;
            public string password = "";
            public string[] versions;
            public List<ServerBuilding> serverBuildings = new List<ServerBuilding>();
            public List<ServerUnit> serverUnits = new List<ServerUnit>();
            public List<ServerSpell> serverSpells = new List<ServerSpell>();
            public List<Research> research = new List<Research>();
        }

        // Server* 类描述配置层级的属性，而下面的运行时类保存可变状态。
        public class ServerUnit
        {
            public UnitID id = UnitID.barbarian;
            public int level = 0;
            public int requiredGold = 0;
            public int requiredElixir = 0;
            public int requiredGems = 0;
            public int requiredDarkElixir = 0;
            public int trainTime = 0;
            public int health = 0;
            public int housing = 0;
            public int researchTime = 0;
            public int researchGold = 0;
            public int researchElixir = 0;
            public int researchDarkElixir = 0;
            public int researchGems = 0;
            public int researchXp = 0;
        }

        public class Unit
        {
            public UnitID id = UnitID.barbarian;
            public int level = 0;
            public long databaseID = 0;
            public int hosing = 1;
            public bool trained = false;
            public bool ready = false;
            public int health = 0;
            public int trainTime = 0;
            public float trainedTime = 0;
            public float moveSpeed = 1;
            public float attackSpeed = 1;
            public float attackRange = 1;
            public float damage = 1;
            public float splashRange = 0;
            public float rangedSpeed = 5;
            public TargetPriority priority = TargetPriority.none;
            public UnitMoveType movement = UnitMoveType.ground;
            public float priorityMultiplier = 1;
        }

        public class Building
        {
            public BuildingID id = BuildingID.townhall;
            public int level = 0;
            public long databaseID = 0;
            public int x = 0;
            public int y = 0;
            public int warX = -1;
            public int warY = -1;
            public int columns = 0;
            public int rows = 0;
            public int goldStorage = 0;
            public int elixirStorage = 0;
            public int darkStorage = 0;
            public DateTime boost;
            public int health = 100;
            public float damage = 0;
            public int capacity = 0;
            public int goldCapacity = 0;
            public int elixirCapacity = 0;
            public int darkCapacity = 0;
            public float speed = 0;
            public float radius = 0;
            public DateTime constructionTime;
            public bool isConstructing = false;
            public int buildTime = 0;
            public BuildingTargetType targetType = BuildingTargetType.none;
            public float blindRange = 0;
            public float splashRange = 0;
            public float rangedSpeed = 5;
            public double percentage = 0;
        }

        public class ServerBuilding
        {
            public string id = "";
            public int level = 0;
            public long databaseID = 0;
            public int requiredGold = 0;
            public int requiredElixir = 0;
            public int requiredGems = 0;
            public int requiredDarkElixir = 0;
            public int columns = 0;
            public int rows = 0;
            public int buildTime = 0;
            public int gainedXp = 0;
        }

        [System.Serializable]
        public class BuildingAvailability
        {
            public int level = 1;
            public BuildingCount[] buildings = null;
        }

        [System.Serializable]
        public class BuildingCount
        {
            public string id = "global_id";
            public int count = 0;
            public int maxLevel = 1;
            public int have = 0;
        }
    }
}
