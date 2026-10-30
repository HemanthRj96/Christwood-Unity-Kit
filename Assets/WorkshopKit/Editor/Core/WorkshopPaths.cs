namespace UnityWorkshop
{
    /// <summary>Workshop-owned asset locations. Every generated file lives under <see cref="Root"/>.</summary>
    public static class WorkshopPaths
    {
        public const string Root = "Assets/WorkshopKit";
        public const string Content = Root + "/Content";
        public const string Templates = Content + "/Templates";
        public const string Prefabs = Content + "/Prefabs";
        public const string SystemPrefabs = Prefabs + "/System";
        public const string EnvironmentPrefabs = Prefabs + "/Environment";
        public const string GameplayPrefabs = Prefabs + "/Gameplay";
        public const string DecorationPrefabs = Prefabs + "/Decoration";
        public const string Configuration = Content + "/Configuration";
        public const string Materials = Content + "/Materials";
        public const string Audio = Content + "/Audio";
        public const string UI = Content + "/UI";
        public const string Examples = Root + "/Examples";
        public const string Recovery = Root + "/Recovery";
        public const string TeamGames = Root + "/TeamGames";
        public const string AdoptedAssets = Root + "/AdoptedAssets";

        public const string Catalog = Configuration + "/WorkshopCatalog.asset";
        public const string GameSetupPrefab = SystemPrefabs + "/Game Setup.prefab";
        public const string HudPrefab = SystemPrefabs + "/Workshop HUD.prefab";
        public const string StartPointPrefab = GameplayPrefabs + "/Start Point.prefab";
        public const string ExitGoalPrefab = GameplayPrefabs + "/Exit Goal.prefab";

        public const string MenuRoot = "Tools/Unity Workshop/";
    }
}
