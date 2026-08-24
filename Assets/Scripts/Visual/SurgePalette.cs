using UnityEngine;

[CreateAssetMenu(fileName = "SurgePalette", menuName = "Surge/Visual/Palette")]
public class SurgePalette : ScriptableObject
{
    [Header("Node Colors")]
    public Color neonBlue = new(0.12f, 0.76f, 1.00f, 1f);
    public Color neonPink = new(1.00f, 0.20f, 0.70f, 1f);
    public Color neonGreen = new(0.20f, 1.00f, 0.45f, 1f);
    public Color neonYellow = new(1.00f, 0.87f, 0.15f, 1f);
    public Color neonPurple = new(0.64f, 0.35f, 1.00f, 1f);

    [Header("Environment")]
    public Color boardBackdrop = new(0.05f, 0.07f, 0.11f, 1f);
    public Color hudPrimary = new(0.70f, 0.93f, 1f, 1f);
    public Color hudAccent = new(1.00f, 0.38f, 0.82f, 1f);
}
