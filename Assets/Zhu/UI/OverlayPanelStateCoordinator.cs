using UnityEngine;

public static class OverlayPanelStateCoordinator
{
    private static bool isCharacterPanelOpen;
    private static bool isRunePanelOpen;
    private static bool pauseApplied;
    private static float previousTimeScale = 1f;

    public static bool IsCharacterPanelOpen => isCharacterPanelOpen;
    public static bool IsRunePanelOpen => isRunePanelOpen;
    public static bool ShouldPauseEnemies => isCharacterPanelOpen || isRunePanelOpen;

    public static void SetCharacterPanelOpen(bool isOpen)
    {
        isCharacterPanelOpen = isOpen;
        ApplyPauseState();
    }

    public static void SetRunePanelOpen(bool isOpen)
    {
        isRunePanelOpen = isOpen;
        ApplyPauseState();
    }

    private static void ApplyPauseState()
    {
        if (ShouldPauseEnemies)
        {
            if (pauseApplied)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pauseApplied = true;
            return;
        }

        if (!pauseApplied)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        pauseApplied = false;
    }
}
