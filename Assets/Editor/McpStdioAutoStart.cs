using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class McpStdioAutoStart
{
    private static bool started;

    static McpStdioAutoStart()
    {
        EditorApplication.delayCall += Start;
    }

    private static void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        try
        {
            EditorConfigurationCache.Instance.SetUseHttpTransport(false);
            StdioBridgeHost.StartAutoConnect();
            Debug.Log($"McpStdioAutoStart: stdio bridge started on port {StdioBridgeHost.GetCurrentPort()}.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"McpStdioAutoStart: failed to start stdio bridge: {ex}");
        }
    }
}
