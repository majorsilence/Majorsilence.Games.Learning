using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Isometric;

namespace Majorsilence.Games.Core.Rendering;

/// <summary>
/// Merges GameObjects (and, for IsometricTilemap, its individual tiles) into a
/// single per-frame painter's-algorithm sort by screen-Y, so moving sprites can
/// correctly interleave in front of/behind individual isometric tiles instead of
/// only being globally in front of or behind an entire tilemap.
/// </summary>
public static class RenderQueue
{
    public static void RenderSorted(IEnumerable<GameObject> gameObjects)
    {
        var items = new List<(float SortY, int ZIndex, Action Render)>();

        foreach (var obj in gameObjects)
        {
            if (obj is IsometricTilemap tilemap)
            {
                foreach (var (sortY, render) in tilemap.EnumerateRenderItems())
                    items.Add((sortY, obj.ZIndex, render));
            }
            else
            {
                items.Add((obj.SortY, obj.ZIndex, obj.Render));
            }
        }

        items.Sort((a, b) => a.SortY != b.SortY
            ? a.SortY.CompareTo(b.SortY)
            : a.ZIndex.CompareTo(b.ZIndex));

        foreach (var item in items) item.Render();
    }
}
