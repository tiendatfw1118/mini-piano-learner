using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class HitWindow { public int perfect = 250, great = 500, good = 1000; }

[System.Serializable]
public class RemoteConfigData
{
    public int baseBpm = 100;
    public int bpmStep = 10;
    public int speedUpCombo = 4;
    public int speedDownMissStreak = 3;
    public int minBpm = 60, maxBpm = 200;
    public HitWindow hitWindowMs = new HitWindow();
    public int inputOffsetMs = 100;
    public int audioLatencyMs = 0;
}

public static class RemoteConfigLoader
{
    const string FileName = "RemoteConfig.json";
    
    // CDN Configuration - Update these with your actual CDN details
    [System.Serializable]
    public class CDNConfig
    {
        public string baseUrl = "https://your-cdn-domain.com/game-config/";
        public string configEndpoint = "RemoteConfig.json";
        public int timeoutSeconds = 10;
        public bool enableFallback = true;
        public bool enableCaching = true;
        public int cacheExpiryHours = 24;
    }
    
    private static CDNConfig cdnConfig = new CDNConfig();
    private static RemoteConfigData cachedConfig;
    private static System.DateTime lastCacheTime;
    private static bool cdnConfigured = false;
    
    /// <summary>
    /// Load remote config from CDN with fallback to local files
    /// </summary>
    public static async Task<RemoteConfigData> LoadAsync()
    {
        
        // Try to load from CDN first
        var remoteConfig = await LoadFromCDN();
        if (remoteConfig != null)
        {
            // Cache the config locally for offline use
            if (cdnConfig.enableCaching)
            {
                await CacheConfigLocally(remoteConfig);
            }
            return remoteConfig;
        }
        
        // Fallback to local files if CDN fails
        if (cdnConfig.enableFallback)
        {
            Debug.LogWarning("[RemoteConfig] CDN failed, falling back to local config");
            var localConfig = await LoadFromLocal();
            if (localConfig != null)
            {
                return localConfig;
            }
        }
        
        // Return default config if everything fails
        Debug.LogError("[RemoteConfig] All loading methods failed, using default config");
        var defaultConfig = new RemoteConfigData();
        return defaultConfig;
    }
    
    /// <summary>
    /// Load config from CDN
    /// </summary>
    private static async Task<RemoteConfigData> LoadFromCDN()
    {
        try
        {
            string cdnUrl = cdnConfig.baseUrl + cdnConfig.configEndpoint;
            // Loading from CDN
            
            using (var request = UnityWebRequest.Get(cdnUrl))
            {
                request.timeout = cdnConfig.timeoutSeconds;
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonText = request.downloadHandler.text;
                    // Successfully loaded from CDN
                    
                    var config = JsonUtility.FromJson<RemoteConfigData>(jsonText);
                    if (config != null)
                    {
                        cachedConfig = config;
                        lastCacheTime = System.DateTime.Now;
                        return config;
                    }
                    else
                    {
                        Debug.LogError("[RemoteConfig] Failed to parse JSON from CDN");
                    }
                }
                else
                {
                    Debug.LogError($"[RemoteConfig] CDN request failed: {request.error}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RemoteConfig] Exception loading from CDN: {e.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Load config from local files (fallback)
    /// </summary>
    private static async Task<RemoteConfigData> LoadFromLocal()
    {
        // Try persistent data path first (user-modified config)
        string persistentPath = Path.Combine(Application.persistentDataPath, FileName);
        if (File.Exists(persistentPath))
        {
            try
            {
                string jsonText = File.ReadAllText(persistentPath);
                var config = JsonUtility.FromJson<RemoteConfigData>(jsonText);
                if (config != null)
                {
                    // Loaded from persistent data path
                    return config;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RemoteConfig] Error reading persistent config: {e.Message}");
            }
        }
        
        // Try streaming assets (default config)
        string streamingPath = Path.Combine(Application.streamingAssetsPath, FileName);
        if (File.Exists(streamingPath))
        {
            try
            {
                string jsonText = File.ReadAllText(streamingPath);
                var config = JsonUtility.FromJson<RemoteConfigData>(jsonText);
                if (config != null)
                {
                    // Loaded from streaming assets
                    return config;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RemoteConfig] Error reading streaming config: {e.Message}");
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Cache config locally for offline use
    /// </summary>
    private static async Task CacheConfigLocally(RemoteConfigData config)
    {
        try
        {
            string cachePath = Path.Combine(Application.persistentDataPath, "cached_" + FileName);
            string jsonText = JsonUtility.ToJson(config, true);
            await File.WriteAllTextAsync(cachePath, jsonText);
            // Config cached locally
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RemoteConfig] Error caching config: {e.Message}");
        }
    }
    
    /// <summary>
    /// Check if cached config is still valid
    /// </summary>
    public static bool IsCacheValid()
    {
        if (cachedConfig == null) return false;
        
        var timeSinceCache = System.DateTime.Now - lastCacheTime;
        return timeSinceCache.TotalHours < cdnConfig.cacheExpiryHours;
    }
    
    /// <summary>
    /// Get cached config if valid
    /// </summary>
    public static RemoteConfigData GetCachedConfig()
    {
        return IsCacheValid() ? cachedConfig : null;
    }
    
    /// <summary>
    /// Configure CDN settings
    /// </summary>
    public static void ConfigureCDN(string baseUrl, string configEndpoint = "RemoteConfig.json", int timeoutSeconds = 10)
    {
        cdnConfig.baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
        cdnConfig.configEndpoint = configEndpoint;
        cdnConfig.timeoutSeconds = timeoutSeconds;
        cdnConfigured = true;
    }
    
    /// <summary>
    /// Check if CDN has been configured
    /// </summary>
    public static bool IsCDNConfigured()
    {
        return cdnConfigured;
    }
    
    /// <summary>
    /// Configure CDN for testing with a public JSON hosting service
    /// </summary>
    public static void ConfigureTestCDN()
    {
        // Using JSONBin.io as a test CDN (you can replace with your own)
        // First, upload your RemoteConfig.json to a public JSON hosting service
        ConfigureCDN("https://api.jsonbin.io/v3/b", "your-bin-id", 10);
    }
    
    /// <summary>
    /// Configure CDN for local development (requires local HTTP server)
    /// </summary>
    public static void ConfigureLocalCDN(int port = 8080)
    {
        ConfigureCDN($"http://localhost:{port}", "RemoteConfig.json", 5);
    }
    
    /// <summary>
    /// Force reload from CDN (ignore cache)
    /// </summary>
    public static async Task<RemoteConfigData> ForceReloadFromCDN()
    {
        cachedConfig = null;
        lastCacheTime = System.DateTime.MinValue;
        return await LoadFromCDN();
    }
}
