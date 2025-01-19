using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ServerSync;

namespace BuildResourcesModNamespace
{
    [BepInPlugin("Jammerbam.buildresourcesmod", "Build Resources Mod", "1.0.0")]
    public class BuildResourcesMod : BaseUnityPlugin
    {
        private static BuildResourcesMod Instance;
        private static ConfigSync configSync = new ConfigSync("BuildResourcesMod")
        {
            DisplayName = "Build Resources Mod",
            CurrentVersion = "1.0.0",
            MinimumRequiredVersion = "1.0.0",
            IsLocked = true,
            ModRequired = true
        };

        private Dictionary<GameObject, string> PieceToTableMap = new Dictionary<GameObject, string>();
        private Dictionary<string, PieceTable> CachedPieceTables = new Dictionary<string, PieceTable>();
        private Dictionary<string, SyncedConfigEntry<bool>> CategoryConfigs = new Dictionary<string, SyncedConfigEntry<bool>>();
        private SyncedConfigEntry<string> Exceptions;
        private ConfigEntry<bool> EnableLogging;

        private HashSet<string> ExceptionPieces = new HashSet<string>();
        private bool LastChecked;
        private string LastPiece;


        public static void Dbgl(string message, bool pref = true)
        {
            if (Instance?.EnableLogging?.Value == true)
            {
                Debug.Log((pref ? "[BuildResourcesMod] " : "") + message);
            }
        }
        

        void Awake()
        {
            Instance = this;
        
            // Initialize core configurations
            EnableLogging = Config.Bind("Debug", "EnableLogging", false, "Enable detailed logging for debugging.");
            Dbgl($"Initialized EnableLogging: {EnableLogging.Value}");

            AddCategoryConfig("Misc", "Require resources for Misc category.", true);
            AddCategoryConfig("Crafting", "Require resources for Crafting category.", true);
            AddCategoryConfig("Furniture", "Require resources for Furniture category.", false);
            AddCategoryConfig("BuildingWorkbench", "Require resources for BuildingWorkbench category.", false);
            AddCategoryConfig("BuildingStonecutter", "Require resources for BuildingStonecutter category.", false);
            AddCategoryConfig("Cultivator", "Require resources for the cultivator.", true);
            AddCategoryConfig("Hoe", "Require resources for the hoe.", true);
        
            Exceptions = configSync.AddConfigEntry(
            Config.Bind("Exceptions", "PieceExceptions", "", "Comma-separated list of pieces that are always buildable.")
            );

            var harmony = new Harmony("Jammerbam.buildresourcesmod");
            harmony.PatchAll();
        
            Dbgl("Mod initialized successfully.");
        }

        
        void Start()
        {
            StartCoroutine(WaitForGameLoad());
            StartCoroutine(WaitForConfigSync());
        }

        private System.Collections.IEnumerator WaitForGameLoad()
        {
            Dbgl("Waiting for the game to fully initialize...");

            // Initial delay to allow all mods and game assets to initialize
            yield return new WaitForSeconds(5f);

            // Ensure that primary objects for mod categories are loaded
            while (Resources.FindObjectsOfTypeAll<PieceTable>().Length == 0)
            {
                yield return null; // Wait until piece tables are available
            }

            Dbgl("Game initialization complete. Proceeding with category detection.");
            HandleCategories();

            // Wait for the world to fully load
            yield return new WaitUntil(() => ZNetScene.instance != null);

            Dbgl("World initialization complete. Performing post-load routines.");
            HandleCategories();
            CachePieceTables();
        }

        private System.Collections.IEnumerator WaitForConfigSync()
        {
            Dbgl("Waiting for config sync to complete...");

            // Use reflection to check for the private 'InitialSyncDone' property
            var initialSyncDoneProperty = AccessTools.Property(configSync.GetType(), "InitialSyncDone");
            if (initialSyncDoneProperty == null)
            {
                Dbgl("Error: Unable to find InitialSyncDone property.");
                yield break;
            }

            // Wait until the config sync is complete
            yield return new WaitUntil(() =>
            {
                bool syncDone = (bool)initialSyncDoneProperty.GetValue(configSync);
                return syncDone;
            });
            ParseExceptions();
        }



        private void AddCategoryConfig(string category, string description, bool defaultValue)
        {
            CategoryConfigs[category] = configSync.AddConfigEntry(
                Config.Bind("Categories", $"{category}RequiresResources", defaultValue, description)
            );
        }
  

        private void ParseExceptions()
        {
            Dbgl("Parsing exceptions...");
            ExceptionPieces.Clear();
            if (string.IsNullOrWhiteSpace(Exceptions.Value))
            {
                Dbgl("No exception pieces specified in config.");
            }

            var pieces = Exceptions.Value.Split(',');
            foreach (string piece in pieces)
            {
                string trimmedPiece = piece.Trim();
                if (!string.IsNullOrEmpty(trimmedPiece))
                {
                    ExceptionPieces.Add(trimmedPiece);
                    Dbgl($"Exception added: {trimmedPiece}");
                }
            }
            Dbgl($"Final list of exception pieces: {string.Join(", ", ExceptionPieces)}");
        } 

        private void CachePieceTables()
        {
            CachedPieceTables.Clear();
            PieceToTableMap.Clear();

            foreach (var pieceTable in Resources.FindObjectsOfTypeAll<PieceTable>())
            {
                if (!CachedPieceTables.ContainsKey(pieceTable.name))
                {
                    CachedPieceTables[pieceTable.name] = pieceTable;
                    Dbgl($"Cached PieceTable: {pieceTable.name}");

                    foreach (var pieceObject in pieceTable.m_pieces)
                    {
                        if (pieceObject != null && !PieceToTableMap.ContainsKey(pieceObject))
                        {
                            PieceToTableMap[pieceObject] = pieceTable.name;
                            Dbgl($" - Piece: {pieceObject.name} belongs to PieceTable: {pieceTable.name}");
                        }
                    }
                }
            }

            if (CachedPieceTables.Count == 0)
            {
                Dbgl("No piece tables were found to cache.");
            }
        }


        private void HandleCategories()
        {
            Dbgl("Scanning game for available categories...");

            HashSet<string> blacklist = new HashSet<string>
            {
                "Misc",
                "Crafting",
                "Furniture",
                "BuildingWorkbench",
                "BuildingStonecutter",
                "All",
                "Meads",
                "Feasts",
                "Food"
            };

            HashSet<string> processedCategories = new HashSet<string>();
            HashSet<string> detectedCategories = new HashSet<string>();

            foreach (var pieceTable in Resources.FindObjectsOfTypeAll<PieceTable>())
            {
                foreach (var obj in pieceTable.m_pieces)
                {
                    if (obj != null)
                    {
                        Piece piece = obj.GetComponent<Piece>();
                        if (piece == null) continue;

                        string category = piece.m_category.ToString();

                        if (processedCategories.Contains(category))
                        {
                            continue; // Skip already processed categories
                        }

                        processedCategories.Add(category);

                        if (blacklist.Contains(category))
                        {
                            Dbgl($"Skipping blacklisted category: {category}");
                            continue;
                        }

                        detectedCategories.Add(category);
                        Dbgl($"Detected modded category: {category}");

                        // Dynamically create or update configuration for this category
                        if (!CategoryConfigs.ContainsKey(category))
                        {
                            LocalizationAsset locAsset = new LocalizationAsset();
                            string localizedCategory = locAsset.GetLocalizedString($"$category_{category}") ?? category;

                            Dbgl($"Adding new config entry for category: {localizedCategory}");
                            
                            CategoryConfigs[category] = configSync.AddConfigEntry(
                                Config.Bind(
                                    "Categories",
                                    $"{category}RequiresResources",
                                    true,
                                    new ConfigDescription($"Require resources for modded category: {localizedCategory}")
                                )
                            );
                        }
                        else
                        {
                            Dbgl($"Category '{category}' already exists in the configuration.");
                        }
                    }
                }
            }

            if (detectedCategories.Count == 0)
            {
                Dbgl("No additional modded categories detected.");
            }
            else
            {
                Dbgl($"Detected and added {detectedCategories.Count} new modded categories.");
            }
        }


        private bool ShouldRequireResources(Piece piece)
        {
            if (LastPiece == piece.name)
            {
                return LastChecked;
            }

            if (piece == null)
            {
                Dbgl("Piece is null in ShouldRequireResources check.");
                LastPiece = piece.name;
                LastChecked = true;
                return true; // Default to requiring resources if piece is null
            }

            string rawName = piece.name.Replace("(Clone)", "").Trim().ToLowerInvariant();

            // Check exceptions by raw internal name
            if (ExceptionPieces.Contains(rawName))
            {
                Dbgl($"Piece '{piece.name}' is in the exception list. Skipping resource requirement.");
                LastPiece = piece.name;
                LastChecked = false;
                return false;
            }

            // Check if the piece belongs to the cultivator
            if (PieceToTableMap.TryGetValue(piece.gameObject, out string tableName))
            {
                if (tableName == "_CultivatorPieceTable")
                {
                    if (CategoryConfigs.TryGetValue("Cultivator", out SyncedConfigEntry<bool> cultivatorConfig))
                    {
                        Dbgl($"Piece '{piece.name}' belongs to the cultivator. Requires resources: {cultivatorConfig.Value}");
                        LastPiece = piece.name;
                        LastChecked = cultivatorConfig.Value;
                        return cultivatorConfig.Value;
                    }
                }
                // Check if the piece belongs to the hoe
                else if (tableName == "_HoePieceTable")
                {
                    if (CategoryConfigs.TryGetValue("Hoe", out SyncedConfigEntry<bool> hoeConfig))
                    {
                        Dbgl($"Piece '{piece.name}' belongs to the hoe. Requires resources: {hoeConfig.Value}");
                        LastPiece = piece.name;
                        LastChecked = hoeConfig.Value;
                        return hoeConfig.Value;
                    }
                }
            }

            // Check if the piece belongs to a valid category
            string category = piece.m_category.ToString();
            if (CategoryConfigs.TryGetValue(category, out SyncedConfigEntry<bool> config))
            {
                Dbgl($"Piece '{piece.name}' in category '{category}' requires resources: {config.Value}");
                LastPiece = piece.name;
                LastChecked = config.Value;
                return config.Value;
            }

            Dbgl($"Piece '{piece.name}' in category '{category}' has no specific configuration. Defaulting to require resources.");
            LastPiece = piece.name;
            LastChecked = true;
            return true;
        }


        [HarmonyPatch(typeof(Player), "ConsumeResources")]
        [HarmonyPatch(new[] { typeof(Piece.Requirement[]), typeof(int), typeof(int), typeof(int) })]
        public static class ConsumeResourcesPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier, Player __instance)
            {
                if (BuildResourcesMod.Instance == null) return true;

                // Attempt to locate the currently selected piece from the requirements or context
                foreach (var requirement in requirements)
                {
                    if (requirement.m_resItem == null) continue;

                    // Check the Piece associated with these requirements
                    Piece currentPiece = __instance.GetSelectedPiece(); 
                    if (currentPiece != null && !BuildResourcesMod.Instance.ShouldRequireResources(currentPiece))
                    {
                        Dbgl($"Skipping resource consumption for piece '{currentPiece.m_name}(Token Name)' in category '{currentPiece.m_category}' (mod setting).");
                        return false; // Skip the default resource consumption logic
                    }
                }

                // Default behavior if no bypass conditions are met
                return true;
            }
        }


        [HarmonyPatch(typeof(Player), "HaveRequirements")]
        [HarmonyPatch(new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        public static class HaveRequirementsPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Piece piece, Player.RequirementMode mode, ref bool __result, Player __instance)
            {
                if (BuildResourcesMod.Instance == null) return true;

                // Reflectively access m_knownStations as a Dictionary<string, int>
                var knownStationsField = AccessTools.Field(typeof(Player), "m_knownStations");
                var knownStations = (Dictionary<string, int>)knownStationsField.GetValue(__instance);

                // Crafting station check
                if (piece.m_craftingStation)
                {
                    if (mode == Player.RequirementMode.IsKnown || mode == Player.RequirementMode.CanAlmostBuild)
                    {
                        if (!knownStations.ContainsKey(piece.m_craftingStation.m_name))
                        {
                            __result = false;
                            return false; // Block placement if the station is unknown
                        }
                    }
                    else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position)
                            && !ZoneSystem.instance.GetGlobalKey("NoWorkbench"))
                    {
                        __result = false;
                        return false; // Block placement if the station is out of range
                    }
                }

                // DLC check
                if (piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
                {
                    __result = false;
                    return false; // Block placement if the required DLC is not installed
                }
                //FreeBuildKey check
                if (mode != Player.RequirementMode.IsKnown && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
                {
                    __result = true;
                    return false;
                }

                // Check if the piece requires resources
                if  (mode != Player.RequirementMode.IsKnown && !BuildResourcesMod.Instance.ShouldRequireResources(piece))
                {
                    __result = true;
                    return false; // Simulate having the requirements
                }

                // Fall back to the original method's logic for resource checks
                return true;
            }
        }


        [HarmonyPatch(typeof(Piece), "DropResources")]
        public static class DropResourcesPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Piece __instance)
            {
                if (BuildResourcesMod.Instance == null) return true;

                // Use ShouldRequireResources to determine if resources should be dropped
                if (!BuildResourcesMod.Instance.ShouldRequireResources(__instance))
                {
                    Dbgl($"Skipping resource drop for piece '{__instance.name}' (Mod Setting).");
                    return false; // Prevent resource drop
                }

                // Default behavior (resources are dropped)
                Dbgl($"Dropping resources for piece '{__instance.name}'.");
                return true;
            }
        }


        [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
        public static class SetupPieceInfoPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Piece piece, Hud __instance)
            {
                if (piece == null || BuildResourcesMod.Instance == null) return;

                // Iterate over all resource slots in the UI
                for (int j = 0; j < piece.m_resources.Length; j++)
                {
                    Piece.Requirement req = piece.m_resources[j];

                    if (req.m_resItem != null && !BuildResourcesMod.Instance.ShouldRequireResources(piece))
                    {
                        // Find the resource amount text element
                        GameObject resourceSlot = __instance.m_requirementItems[j];
                        TMP_Text component3 = resourceSlot.transform.Find("res_amount")?.GetComponent<TMP_Text>();
                        
                        if (component3 != null)
                        {
                            component3.color = Color.white; // Always display resource amounts in white
                        }
                    }
                }
            }
        }
    }
}
