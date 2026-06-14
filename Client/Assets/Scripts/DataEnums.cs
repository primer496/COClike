namespace DevelopersHub.ClashOfWhatecer
{
    public static partial class Data
    {
        public enum ResearchType
        {
            unit = 1, spell = 2
        }

        public enum ChatType
        {
            global = 1, clan = 2
        }

        public enum ClanRank
        {
            member = 0, leader = 1, coleader = 2, elder = 3
        }

        public enum ClanJoinType
        {
            AnyoneCanJoin = 0, NotAcceptingNewMembers = -1, TakingJoinRequests = 1
        }

        public enum TargetPriority
        {
            none = 0, all = 1, defenses = 2, resources = 3, walls = 4
        }

        public enum BuildingTargetType
        {
            none = 0, ground = 1, air = 2, all = 3
        }

        public enum UnitMoveType
        {
            ground = 0, jump = 1, fly = 2, underground = 3
        }

        public enum BuildingID
        {
            townhall = 0, goldmine = 1, goldstorage = 2, elixirmine = 3, elixirstorage = 4,
            darkelixirmine = 5, darkelixirstorage = 6, buildershut = 7, armycamp = 8, barracks = 9,
            darkbarracks = 10, wall = 11, cannon = 12, archertower = 13, mortor = 14, airdefense = 15,
            wizardtower = 16, hiddentesla = 19, bombtower = 20, xbow = 21, infernotower = 22,
            decoration = 23, obstacle = 24, boomb = 25, springtrap = 26, airbomb = 27, giantbomb = 28,
            seekingairmine = 29, skeletontrap = 30, clancastle = 31, spellfactory = 32,
            darkspellfactory = 33, laboratory = 34, airsweeper = 35, kingaltar = 36, qeenaltar = 37
        }

        public enum UnitID
        {
            barbarian = 0, archer = 1, goblin = 2, healer = 3, wallbreaker = 4, giant = 5, miner = 6,
            balloon = 7, wizard = 8, dragon = 9, pekka = 10, babydragon = 11, electrodragon = 12,
            yeti = 13, dragonrider = 14, electrotitan = 15, minion = 16, hogrider = 17, valkyrie = 18,
            golem = 19, witch = 20, lavahound = 21, bowler = 22, icegolem = 23, headhunter = 24,
            skeleton = 25, bat = 26
        }

        // 当前项目中已经实现效果的法术：lightning、healing、rage、freeze、invisibility、haste
        public enum SpellID
        {
            lightning = 0, healing = 1, rage = 2, jump = 3, freeze = 4, invisibility = 5, recall = 6,
            earthquake = 7, haste = 8, skeleton = 9, bat = 10
        }

        public enum BattleType
        {
            normal = 1, war = 2, quest = 3
        }

        public enum BuyResourcePack
        {
            gold_10 = 0, gold_50 = 1, gold_100 = 2, elixir_10 = 3, elixir_50 = 4, elixir_100 = 5,
            dark_10 = 6, dark_50 = 7, dark_100 = 8
        }
    }
}
