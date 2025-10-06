using UnityEngine;

/// <summary>
/// Simple setup script to configure CDN for remote config loading
/// Attach this to a GameObject in your scene for easy CDN configuration
/// </summary>
public class CDNSetup : MonoBehaviour
{
    [Header("CDN Configuration")]
    [Tooltip("Your CDN base URL (e.g., https://your-cdn.com/game-config/) - Use placeholder for public repo")]
    public string cdnBaseUrl = "https://your-cdn-domain.com/game-config/";
    
    [Tooltip("Config file endpoint (e.g., RemoteConfig.json)")]
    public string configEndpoint = "RemoteConfig.json";
    
    [Tooltip("Request timeout in seconds")]
    public int timeoutSeconds = 10;
    
    [Header("Advanced Settings")]
    [Tooltip("Enable fallback to local files if CDN fails")]
    public bool enableFallback = true;
    
    [Tooltip("Enable local caching of remote config")]
    public bool enableCaching = true;
    
    [Tooltip("Cache expiry time in hours")]
    public int cacheExpiryHours = 24;
    
    [Header("Auto Setup")]
    [Tooltip("Automatically configure CDN on Start")]
    public bool autoSetupOnStart = true;
    
    void Awake()
    {
        if (autoSetupOnStart)
        {
            SetupCDN();
        }
    }
    
    /// <summary>
    /// Configure the CDN with current settings
    /// </summary>
    [ContextMenu("Setup CDN")]
    public void SetupCDN()
    {
        if (string.IsNullOrEmpty(cdnBaseUrl) || cdnBaseUrl.Contains("your-cdn-domain"))
        {
            return;
        }
        
        RemoteConfigLoader.ConfigureCDN(cdnBaseUrl, configEndpoint, timeoutSeconds);
    }
    
    /// <summary>
    /// Test the CDN connection
    /// </summary>
    [ContextMenu("Test CDN Connection")]
    public async void TestCDNConnection()
    {
        
        try
        {
            var config = await RemoteConfigLoader.ForceReloadFromCDN();
            
            if (config != null)
            {
            }
            else
            {
                // CDN test failed
            }
        }
        catch (System.Exception e)
        {
            // CDN test exception
        }
    }
    
    /// <summary>
    /// Quick setup for common CDN providers
    /// </summary>
    [ContextMenu("Setup for AWS CloudFront")]
    public void SetupForAWSCloudFront()
    {
        cdnBaseUrl = "https://your-cloudfront-domain.cloudfront.net/game-config/";
        configEndpoint = "RemoteConfig.json";
        timeoutSeconds = 10;
        SetupCDN();
    }
    
    [ContextMenu("Setup for Azure CDN")]
    public void SetupForAzureCDN()
    {
        cdnBaseUrl = "https://your-azure-cdn.azureedge.net/game-config/";
        configEndpoint = "RemoteConfig.json";
        timeoutSeconds = 10;
        SetupCDN();
    }
    
    [ContextMenu("Setup for Cloudflare")]
    public void SetupForCloudflare()
    {
        cdnBaseUrl = "https://your-domain.com/cdn/game-config/";
        configEndpoint = "RemoteConfig.json";
        timeoutSeconds = 10;
        SetupCDN();
    }
}
