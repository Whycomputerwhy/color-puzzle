using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>
    /// Persistent, per-level playtest telemetry inspired by the supplied USC TelemetryManager.
    /// Records gameplay events in memory and appends readable session blocks to one text file.
    /// </summary>
    public sealed class TelemetryManager : MonoBehaviour
    {
        // CENTRALIZED TELEMETRY PARAMETERS
        [Header("Collection")]
        [SerializeField] private string menuSceneName = "ColorPuzzleStartMenu";
        [SerializeField] private string campaignSceneName = "ColorPuzzleOceanCampaign";
        [SerializeField] private Key manualSnapshotKey = Key.F9;
        [SerializeField] private bool writeIncompleteSessions = true;

        [Header("Text Output")]
        [SerializeField] private string outputDirectoryName = "Telemetry";
        [SerializeField] private string outputFileName = "ColorPuzzle-Telemetry.txt";
        [SerializeField] private bool logOutputPath = true;

        [Header("Standalone Build Output")]
        [Tooltip("Standalone desktop builds save logs here by default. The Editor continues to use Unity's persistent data folder.")]
        [SerializeField] private bool saveToDesktopInStandaloneBuilds = true;
        [SerializeField] private string desktopOutputDirectoryName = "ColorPuzzleTelemetry";

        private static TelemetryManager instance;
        private const string LosAngelesTimeZoneId = "America/Los_Angeles";

        private string runId;
        private DateTime runStartUtc;
        private string sessionOutputFileName;
        private bool wroteFileHeader;
        private string levelName;
        private DateTime levelStartUtc;
        private double levelStartTime;
        private double portalUnlockTime;
        private int magnifierUseCount;
        private int failedAttemptCount;
        private int objectPickupCount;
        private int objectDropCount;
        private bool sessionActive;
        private bool portalUnlocked;
        private bool stageOnePearlPuzzleRun;
        private int campaignPuzzleCompletionOrder;

        public static string OutputFilePath => instance != null
            ? instance.GetOutputFilePath()
            : Path.Combine(Application.persistentDataPath, "Telemetry", "ColorPuzzle-Telemetry.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            GameObject managerObject = new GameObject("TelemetryManager");
            instance = managerObject.AddComponent<TelemetryManager>();
            DontDestroyOnLoad(managerObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            runId = Guid.NewGuid().ToString("N");
            runStartUtc = DateTime.UtcNow;
            sessionOutputFileName = BuildSessionFileName();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            instance = null;
        }

        private void Update()
        {
            if (!sessionActive || Keyboard.current == null || manualSnapshotKey == Key.None) return;
            if (Keyboard.current[manualSnapshotKey].wasPressedThisFrame)
                WriteSession("manual snapshot", false);
        }

        private void OnApplicationQuit()
        {
            if (sessionActive && writeIncompleteSessions)
                WriteSession("application quit", true);
        }

        public static void RecordMagnifierUse()
        {
            if (instance != null && instance.sessionActive)
                instance.magnifierUseCount++;
        }

        public static void RecordObjectPickup()
        {
            if (instance != null && instance.sessionActive)
                instance.objectPickupCount++;
        }

        public static void RecordObjectDrop()
        {
            if (instance != null && instance.sessionActive)
                instance.objectDropCount++;
        }

        /// <summary>
        /// Placeholder hook for level-specific puzzle logic. Call this once whenever the
        /// player makes an attempt that the current stage considers unsuccessful.
        /// </summary>
        public static void RecordFailedAttempt()
        {
            if (instance != null && instance.sessionActive)
                instance.failedAttemptCount++;
        }

        public static void BeginCampaignStage(int stageNumber)
        {
            if (instance == null) return;
            if (instance.sessionActive)
                instance.WriteSession("stage changed before completion", true);
            instance.BeginSession($"Ocean Stage {stageNumber}");
        }

        /// <summary>
        /// Starts the telemetry window for the puzzle the player actually begins.
        /// This lets the two optional middle puzzles be solved in either order.
        /// </summary>
        public static void BeginCampaignPuzzle(int stageNumber)
        {
            if (instance == null) return;
            string puzzleName = $"Ocean Puzzle {stageNumber}";
            if (instance.sessionActive && instance.levelName == puzzleName) return;
            if (instance.sessionActive)
                instance.WriteSession("switched to another puzzle before completion", true);
            instance.BeginSession(puzzleName);
        }

        /// <summary>
        /// Stage 1's evaluation window begins only when the player earns the magnifier.
        /// The earlier stage-one arrival record is intentionally discarded.
        /// </summary>
        public static void BeginStageOnePearlPuzzle()
        {
            if (instance == null) return;
            instance.sessionActive = false;
            instance.BeginSession("Ocean Stage 1 — Pearl Puzzle", true);
        }

        /// <summary>
        /// Supports the valid no-magnifier route through Puzzle 1. If the player
        /// already collected the magnifier, its more specific telemetry window is
        /// kept instead.
        /// </summary>
        public static void BeginStageOnePuzzleWithoutMagnifierIfNeeded()
        {
            if (instance == null || instance.stageOnePearlPuzzleRun) return;
            const string stageOneName = "Ocean Puzzle 1";
            if (instance.sessionActive && instance.levelName == stageOneName) return;
            if (instance.sessionActive)
                instance.WriteSession("started Puzzle 1 without magnifier pickup", true);
            instance.BeginSession(stageOneName);
        }

        public static void CompleteStageOnePearlPuzzle()
        {
            if (instance == null || !instance.sessionActive) return;
            if (instance.stageOnePearlPuzzleRun)
            {
                instance.WriteSession("pearl puzzle completed", true);
                return;
            }
            if (instance.levelName == "Ocean Puzzle 1")
                instance.WriteSession("pearl puzzle completed without magnifier pickup", true);
        }

        public static void CompleteCampaignStage(int stageNumber)
        {
            if (instance != null && instance.sessionActive)
                instance.WriteSession($"stage {stageNumber} door entered", true);
        }

        /// <summary>Ends a campaign-stage record at a puzzle's actual solve event.</summary>
        public static void CompleteCampaignPuzzle(int stageNumber)
        {
            if (instance != null && instance.sessionActive)
            {
                instance.campaignPuzzleCompletionOrder++;
                instance.WriteSession(
                    $"stage {stageNumber} puzzle completed (campaign puzzle order #{instance.campaignPuzzleCompletionOrder})",
                    true);
            }
        }

        public static void RecordPortalUnlocked()
        {
            if (instance == null || !instance.sessionActive || instance.portalUnlocked) return;
            instance.portalUnlocked = true;
            instance.portalUnlockTime = Time.realtimeSinceStartupAsDouble;
        }

        public static void RecordPortalEntered()
        {
            if (instance == null || !instance.sessionActive || !instance.portalUnlocked) return;
            instance.WriteSession("portal entered", true);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name == menuSceneName) return;
            // OceanCampaignManager begins one record per physical room instead.
            if (scene.name == campaignSceneName) return;
            BeginSession(scene.name);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (!sessionActive || (scene.name != levelName && scene.name != campaignSceneName)) return;
            if (writeIncompleteSessions)
                WriteSession("level exited before portal entry", true);
            else
                sessionActive = false;
        }

        private void BeginSession(string loadedLevelName, bool isStageOnePearlPuzzle = false)
        {
            if (sessionActive && writeIncompleteSessions)
                WriteSession("new level loaded", true);

            levelName = loadedLevelName;
            levelStartUtc = DateTime.UtcNow;
            levelStartTime = Time.realtimeSinceStartupAsDouble;
            portalUnlockTime = 0d;
            magnifierUseCount = 0;
            failedAttemptCount = 0;
            objectPickupCount = 0;
            objectDropCount = 0;
            portalUnlocked = false;
            stageOnePearlPuzzleRun = isStageOnePearlPuzzle;
            sessionActive = true;
        }

        private void WriteSession(string reason, bool endSession)
        {
            if (!sessionActive) return;

            double now = Time.realtimeSinceStartupAsDouble;
            StringBuilder text = new StringBuilder(768);
            if (!wroteFileHeader)
            {
                text.AppendLine("================================================================");
                text.AppendLine("COLOR PUZZLE — PLAYTEST TELEMETRY");
                text.AppendLine($"Play session: {runId}");
                text.AppendLine($"Started (Los Angeles): {FormatLosAngelesTime(runStartUtc)}");
                text.AppendLine("================================================================");
                text.AppendLine();
                wroteFileHeader = true;
            }

            bool completed = reason.Contains("door entered") || reason.Contains("puzzle completed");
            text.AppendLine("STAGE RECORD");
            text.AppendLine($"  Recorded (Los Angeles): {FormatLosAngelesTime(DateTime.UtcNow)}");
            text.AppendLine($"  Stage: {levelName}");
            text.AppendLine($"  Window: {(stageOnePearlPuzzleRun ? "Magnifier pickup → pearl puzzle completion" : "Stage/session")}");
            text.AppendLine($"  Outcome: {(completed ? "Completed" : "In progress / incomplete")}");
            text.AppendLine($"  Saved because: {reason}");
            text.AppendLine();
            text.AppendLine("  METRICS");
            text.AppendLine($"    Time in window: {FormatSeconds(now - levelStartTime)} seconds");
            text.AppendLine($"    Magnifier uses: {magnifierUseCount}");
            text.AppendLine($"    Object pickups: {objectPickupCount}");
            text.AppendLine($"    Object drops / placements: {objectDropCount}");
            text.AppendLine($"    Failed puzzle combinations: {failedAttemptCount}");
            text.AppendLine("----------------------------------------------------------------");
            text.AppendLine();

            string path = GetOutputFilePath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, text.ToString());
                if (logOutputPath) Debug.Log($"Telemetry written to: {path}", this);
            }
            catch (Exception exception)
            {
                string fallbackPath = GetPersistentOutputFilePath();
                if (string.Equals(path, fallbackPath, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"Telemetry could not write to '{path}': {exception.Message}", this);
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath));
                        File.AppendAllText(fallbackPath, text.ToString());
                        Debug.LogWarning(
                            $"Telemetry could not access the Desktop folder ({exception.Message}). " +
                            $"Saved to Unity data instead: {fallbackPath}", this);
                    }
                    catch (Exception fallbackException)
                    {
                        Debug.LogError(
                            $"Telemetry could not write to '{path}' or fallback '{fallbackPath}': " +
                            fallbackException.Message,
                            this);
                    }
                }
            }

            if (endSession)
            {
                sessionActive = false;
                stageOnePearlPuzzleRun = false;
            }
        }

        private string GetOutputFilePath()
        {
            string directory = GetOutputDirectoryPath();
            string safeFileName = string.IsNullOrWhiteSpace(sessionOutputFileName)
                ? BuildSessionFileName()
                : sessionOutputFileName;
            return Path.Combine(directory, safeFileName);
        }

        private string GetPersistentOutputFilePath()
        {
            string safeFileName = string.IsNullOrWhiteSpace(sessionOutputFileName)
                ? BuildSessionFileName()
                : sessionOutputFileName;
            return Path.Combine(GetOutputDirectoryPath(true), safeFileName);
        }

        private string GetOutputDirectoryPath(bool forcePersistentDataPath = false)
        {
            if (!forcePersistentDataPath && saveToDesktopInStandaloneBuilds &&
                !Application.isEditor && IsDesktopPlatform())
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrWhiteSpace(desktopPath))
                {
                    string safeDesktopDirectory = string.IsNullOrWhiteSpace(desktopOutputDirectoryName)
                        ? "ColorPuzzleTelemetry"
                        : Path.GetFileName(desktopOutputDirectoryName);
                    return Path.Combine(desktopPath, safeDesktopDirectory);
                }
            }

            string safeDirectory = string.IsNullOrWhiteSpace(outputDirectoryName)
                ? "Telemetry"
                : outputDirectoryName;
            return Path.Combine(Application.persistentDataPath, safeDirectory);
        }

        private static bool IsDesktopPlatform()
        {
            RuntimePlatform platform = Application.platform;
            return platform == RuntimePlatform.OSXPlayer ||
                   platform == RuntimePlatform.WindowsPlayer ||
                   platform == RuntimePlatform.LinuxPlayer;
        }

        private string BuildSessionFileName()
        {
            string configuredName = string.IsNullOrWhiteSpace(outputFileName)
                ? "ColorPuzzle-Telemetry.txt"
                : Path.GetFileName(outputFileName);
            string baseName = Path.GetFileNameWithoutExtension(configuredName);
            string extension = Path.GetExtension(configuredName);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "ColorPuzzle-Telemetry";
            if (string.IsNullOrWhiteSpace(extension)) extension = ".txt";
            string idFragment = string.IsNullOrEmpty(runId) ? "pending" : runId.Substring(0, 8);
            DateTimeOffset localStart = ToLosAngelesTime(runStartUtc);
            return $"{baseName}-{localStart:yyyy-MM-dd_HH-mm-ss}-{idFragment}{extension}";
        }

        private static DateTimeOffset ToLosAngelesTime(DateTime utcTime)
        {
            DateTimeOffset utc = new DateTimeOffset(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc));
            try
            {
                return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, LosAngelesTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows uses the alternate ID; retain a usable local timestamp on
                // unusual platforms rather than failing telemetry output.
                try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, "Pacific Standard Time"); }
                catch (TimeZoneNotFoundException) { return utc.ToLocalTime(); }
            }
        }

        private static string FormatLosAngelesTime(DateTime utcTime) =>
            ToLosAngelesTime(utcTime).ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

        private static string FormatSeconds(double seconds)
        {
            return seconds < 0d
                ? "N/A"
                : seconds.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
