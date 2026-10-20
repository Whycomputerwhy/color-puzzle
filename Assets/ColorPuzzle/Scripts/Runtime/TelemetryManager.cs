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

        private static TelemetryManager instance;

        private string runId;
        private string levelName;
        private DateTime levelStartUtc;
        private double levelStartTime;
        private double portalUnlockTime;
        private int magnifierUseCount;
        private int failedAttemptCount;
        private int objectPickupCount;
        private bool sessionActive;
        private bool portalUnlocked;

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

        public static void CompleteCampaignStage(int stageNumber)
        {
            if (instance != null && instance.sessionActive)
                instance.WriteSession($"stage {stageNumber} door entered", true);
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

        private void BeginSession(string loadedLevelName)
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
            portalUnlocked = false;
            sessionActive = true;
        }

        private void WriteSession(string reason, bool endSession)
        {
            if (!sessionActive) return;

            double now = Time.realtimeSinceStartupAsDouble;
            StringBuilder text = new StringBuilder(512);
            text.AppendLine("============================================================");
            text.AppendLine($"Record UTC: {DateTime.UtcNow:O}");
            text.AppendLine($"Run ID: {runId}");
            text.AppendLine($"Level: {levelName}");
            text.AppendLine($"Level start UTC: {levelStartUtc:O}");
            text.AppendLine($"Write reason: {reason}");
            text.AppendLine($"Stage completed: {(reason.Contains("door entered") ? "yes" : "no")}");
            text.AppendLine($"Time to complete stage / observed stage time (seconds): {FormatSeconds(now - levelStartTime)}");
            text.AppendLine($"Magnifier uses: {magnifierUseCount}");
            text.AppendLine($"Failed attempts: {failedAttemptCount}");
            text.AppendLine($"Objects picked up: {objectPickupCount}");
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
                Debug.LogError($"Telemetry could not write to '{path}': {exception.Message}", this);
            }

            if (endSession) sessionActive = false;
        }

        private string GetOutputFilePath()
        {
            string safeDirectory = string.IsNullOrWhiteSpace(outputDirectoryName)
                ? "Telemetry"
                : outputDirectoryName;
            string safeFileName = string.IsNullOrWhiteSpace(outputFileName)
                ? "ColorPuzzle-Telemetry.txt"
                : outputFileName;
            return Path.Combine(Application.persistentDataPath, safeDirectory, safeFileName);
        }

        private static string FormatSeconds(double seconds)
        {
            return seconds < 0d
                ? "N/A"
                : seconds.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
