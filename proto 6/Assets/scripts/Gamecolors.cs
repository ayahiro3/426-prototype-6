using UnityEngine;

// The one place the palette lives. Colors chosen to stay distinct for most colorblind players.
public static class GameColors
{
    public static readonly Color[] Palette =
    {
        new Color(0.20f, 0.45f, 0.95f), // blue
        new Color(1.00f, 0.55f, 0.10f), // orange
        new Color(1.00f, 0.85f, 0.15f), // yellow
        new Color(0.90f, 0.20f, 0.75f), // magenta
    };

    public static int Count => Palette.Length;

    public static Color Get(int index) => Palette[((index % Count) + Count) % Count];

    public static int RandomExcept(int exclude)
    {
        int i = Random.Range(0, Count - 1);
        return i >= exclude ? i + 1 : i;
    }
}