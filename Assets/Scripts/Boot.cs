using UnityEngine;
using SpeedItUp.Events;
using SpeedItUp.Input;

/// <summary>
/// Boot – initializes core systems, loads configuration/chart, and starts the game loop.
/// Responsibilities:
/// - Wire up Conductor, TempoController, NoteSpawner, JudgeController, and audio.
/// - Select mini-game mode and chart, then start after a short countdown.
/// - Provide RestartGame() for resetting all systems cleanly.
/// </summary>
public class Boot : MonoBehaviour
{
    [Header("Core Components")]
    public Conductor conductor;
    public TempoController tempoController;
    public NoteSpawner noteSpawner;
    public JudgeController judge;
    public ScaleNoteAudio noteAudio;

    [Header("Mini-Game Settings")]
    public MiniGameType selectedMiniGame = MiniGameType.TempoIncreasing;
    
    [Header("Track Selection")]
    public string tempoGameChart = "delay_test.json";
    public string stemGameChart = "stem_test.json";
    public AudioSource backgroundStem; // Background audio for stem mini-game
    
    [Header("Settings")]
    public bool useGuidePlayback = true;
    public bool takeBpmFromRemoteConfig = true;
    public float startDelaySeconds = 3.0f; // Delay before notes start coming down

    async void Start()
    {
        Application.targetFrameRate = 60;

        // Initialize the event bus first
        var eventBus = GameEventBus.Instance;
        
        // Initialize BPM compensation system
        InitializeBPMCompensationSystem();

        // Wait for CDN setup to complete first
        await WaitForCDNSetup();

        // Load configuration and chart based on selected mini-game
        var cfg = await RemoteConfigLoader.LoadAsync();
        tempoController.Setup(cfg);

        // Select chart based on mini-game type
        string selectedChart = GetSelectedChart();
        var chart = await ChartLoader.LoadAsync(selectedChart);
        if (chart == null) 
        { 
            Debug.LogError("[Boot] Chart load failed: " + selectedChart); 
            return; 
        }

        // Setup conductor (but don't start yet)
        conductor.firstBeatOffsetSec = chart.firstBeatOffsetSec;
        
        // Apply BPM from remote config or chart
        float finalBpm = takeBpmFromRemoteConfig ? cfg.baseBpm : chart.bpm;
        conductor.bpm = finalBpm;
        

        // Setup note spawner with chart data (but don't start spawning yet)
        noteSpawner.guidePlayback = useGuidePlayback;
        noteSpawner.SetupAndBegin(chart);
        
        // Initialize selected mini-game
        InitializeMiniGame();

        // Find or create components
        if (!judge) judge = FindFirstObjectByType<JudgeController>();
        if (!noteAudio) noteAudio = FindFirstObjectByType<ScaleNoteAudio>();

        // Start countdown and then begin the game
        StartCoroutine(CountdownAndStart());

        // Setup judge controller
        if (judge)
        {
            judge.cfg = cfg;
            judge.conductor = conductor;
            judge.tempo = tempoController;
            judge.spawner = noteSpawner;
            judge.noteAudio = noteAudio;
        }

        // Connect input to judge controller (legacy compatibility)
        var input = FindFirstObjectByType<TapInput>();
        if (input != null && judge != null)
        {
            // REMOVED: OnTapDegreeDsp - not needed since all notes are hold notes
            // Connected TapInput to JudgeController
        }

        // Setup note audio
        if (noteAudio)
        {
            var f = noteSpawner.GetType().GetField("noteAudio");
            if (f != null) f.SetValue(noteSpawner, noteAudio);
        }

        // Create improved input manager
        var inputManager = FindFirstObjectByType<ImprovedInputManager>();
        if (inputManager == null)
        {
            var inputGO = new GameObject("ImprovedInputManager");
            inputManager = inputGO.AddComponent<ImprovedInputManager>();
        }

        // Create input command manager
        var commandManager = FindFirstObjectByType<InputCommandManager>();
        if (commandManager == null)
        {
            var commandGO = new GameObject("InputCommandManager");
            commandManager = commandGO.AddComponent<InputCommandManager>();
        }

        // Ensure we have an audio listener
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            var cam = new GameObject("Main Camera (Auto)").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.gameObject.AddComponent<AudioListener>();
        }
        
        // Create restart button
        CreateRestartButton();

        // Initialization complete with new architecture
    }

    void OnDestroy()
    {
        // Disconnect input from judge controller
        var input = FindFirstObjectByType<TapInput>();
        var judge = FindFirstObjectByType<JudgeController>();
        if (input != null && judge != null)
        {
            // REMOVED: OnTapDegreeDsp - not needed since all notes are hold notes
        }
        
        // Cleanup is handled by individual components
        // Shutting down
    }

    private System.Collections.IEnumerator CountdownAndStart()
    {
        var feedbackText = judge?.feedbackText;
        if (!feedbackText) yield break;

        if(selectedMiniGame == MiniGameType.TempoIncreasing)
        {
            // Countdown from 3 to 1
            for (int i = 3; i >= 1; i--)
            {
                feedbackText.text = $"Starting in {i}...";
                yield return new WaitForSeconds(1f);
            }
        }
        
        // Show "GO!" when the game starts
        feedbackText.text = "GO!";
        
        // NOW start the conductor (note spawner is already set up)
        conductor.StartConductor();
        
        yield return new WaitForSeconds(0.5f);

        // Clear the feedback text
        feedbackText.text = "";
    }

    /// <summary>
    /// Wait for CDN setup to complete before loading config
    /// </summary>
    private async System.Threading.Tasks.Task WaitForCDNSetup()
    {
        // Wait a frame to ensure all Awake() methods have been called
        await System.Threading.Tasks.Task.Yield();
        
        // Check if CDNSetup component exists and wait for it to configure
        var cdnSetup = FindFirstObjectByType<CDNSetup>();
        if (cdnSetup != null)
        {
            
            // Wait for CDN to be configured (with timeout)
            int maxWaitTime = 1000; // 1 second max wait
            int waitTime = 0;
            while (!RemoteConfigLoader.IsCDNConfigured() && waitTime < maxWaitTime)
            {
                await System.Threading.Tasks.Task.Delay(10);
                waitTime += 10;
            }
            
            if (RemoteConfigLoader.IsCDNConfigured())
            {
            }
            else
            {
                Debug.LogWarning("[Boot] CDN setup timeout, proceeding with default config");
            }
        }
        else
        {
        }
    }
    
    /// <summary>
    /// Initialize the BPM change handling system
    /// </summary>
    private void InitializeBPMCompensationSystem()
    {
        // Create simple BPM handler if it doesn't exist
        var handler = FindFirstObjectByType<SimpleBPMHandler>();
        if (handler == null)
        {
            var handlerGO = new GameObject("SimpleBPMHandler");
            handlerGO.AddComponent<SimpleBPMHandler>();
        }
    }
    
    /// <summary>
    /// Restart the game - reset all components and start over
    /// </summary>
    public void RestartGame()
    {
        // Stop the conductor
        conductor.StopConductor();
        
        // Clear all live notes
        noteSpawner.ClearAllNotes();
        
        // Reset tempo controller
        tempoController.Reset();
        
        // Reset judge controller
        if (judge)
        {
            judge.Reset();
        }
        
        // Reset conductor
        conductor.Reset();
        
        // Reset stem controller if it exists
        var stemController = FindFirstObjectByType<StemController>();
        if (stemController != null)
        {
            stemController.Reset();
        }
        
        // Restart the game
        StartCoroutine(CountdownAndStart());
    }
    
    /// <summary>
    /// Create the restart button in the top left corner
    /// </summary>
    private void CreateRestartButton()
    {
        // Check if restart button already exists
        if (FindFirstObjectByType<RestartButton>() != null)
        {
            return; // Already exists
        }
        
        // Create restart button GameObject
        var restartGO = new GameObject("RestartButton");
        var restartButton = restartGO.AddComponent<RestartButton>();
        restartButton.boot = this;
    }
    
    /// <summary>
    /// Get the selected chart file based on mini-game type
    /// </summary>
    private string GetSelectedChart()
    {
        switch (selectedMiniGame)
        {
            case MiniGameType.TempoIncreasing:
                return tempoGameChart;
            case MiniGameType.BackgroundStem:
                return stemGameChart;
            default:
                return tempoGameChart; // Default fallback
        }
    }
    
    /// <summary>
    /// Initialize the selected mini-game
    /// </summary>
    private void InitializeMiniGame()
    {
        switch (selectedMiniGame)
        {
            case MiniGameType.TempoIncreasing:
                InitializeTempoGame();
                break;
            case MiniGameType.BackgroundStem:
                InitializeStemGame();
                break;
        }
    }
    
    /// <summary>
    /// Initialize tempo increasing mini-game
    /// </summary>
    private void InitializeTempoGame()
    {
        // Tempo game is already initialized by default
        // The TempoController handles BPM changes based on performance
        // Tempo Increasing Mini-Game initialized
    }
    
    /// <summary>
    /// Initialize background stem mini-game
    /// </summary>
    private void InitializeStemGame()
    {
        // Create or find StemController
        var stemController = FindFirstObjectByType<StemController>();
        if (stemController == null)
        {
            var stemGO = new GameObject("StemController");
            stemController = stemGO.AddComponent<StemController>();
            // Created new StemController GameObject
        }
        else
        {
            // Found existing StemController
        }
        
        // Set the background stem audio source
        if (backgroundStem != null)
        {
            stemController.SetBackgroundStem(backgroundStem);
            // Assigned background stem audio source to StemController
        }
        else
        {
            Debug.LogWarning("[Boot] No background stem audio source assigned for stem mini-game!");
        }
        
        // Force the StemController to subscribe to events immediately
        if (stemController != null)
        {
            // StemController is ready and should be subscribed to events
        }
        
        // Background Stem Mini-Game initialized
    }
    
    /// <summary>
    /// Test CDN configuration for development
    /// </summary>
    [ContextMenu("Test CDN Configuration")]
    public void TestCDNConfiguration()
    {
        // Configure a test CDN URL (replace with your actual CDN)
        RemoteConfigLoader.ConfigureCDN("https://your-actual-cdn.com/game-config/", "RemoteConfig.json", 10);
    }
    
    /// <summary>
    /// Configure local CDN for development
    /// </summary>
    [ContextMenu("Configure Local CDN")]
    public void ConfigureLocalCDN()
    {
        RemoteConfigLoader.ConfigureLocalCDN(8080);
    }
}
