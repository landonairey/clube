namespace Clube.Core
{
    /// <summary>Where a surface vertex sits on a crossed cube edge (V3).</summary>
    public enum EdgePlacement
    {
        /// <summary>Linearly interpolated to where the density crosses the iso level.</summary>
        Interpolated,

        /// <summary>Always the edge midpoint, ignoring the densities (blocky look).</summary>
        Midpoint,
    }

    /// <summary>How surface vertices are shared, which decides the normals (V4).</summary>
    public enum Shading
    {
        /// <summary>Every triangle gets its own vertices, so each face has a single normal.</summary>
        Flat,

        /// <summary>Triangles on the same grid edge share a vertex, so normals are averaged.</summary>
        Smooth,
    }

    /// <summary>How terrain materials show on the mesh (M15), a mesh variant picked once per build (A6).</summary>
    public enum MaterialDisplay
    {
        /// <summary>No materials: the mesh carries positions and normals only, drawn with the view's own material.</summary>
        None,

        /// <summary>Each triangle takes one material; vertices split where materials meet, so nothing bleeds.</summary>
        HardSeams,

        /// <summary>Each vertex keeps its own material and the shader blends them across triangles (M13).</summary>
        Blended,

        /// <summary>Hard seams in a flat colour per material, to check where materials are at a glance.</summary>
        DebugColours,
    }

    /// <summary>Which implementation builds the chunk mesh (K12). Both give the same mesh.</summary>
    public enum MesherBackend
    {
        /// <summary>The C# loop (<see cref="ChunkMesher"/>), with strategy objects (A6) and step recording (A11).</summary>
        Managed,

        /// <summary>A Burst-compiled job over native arrays (<see cref="BurstChunkMesher"/>); no step recording.</summary>
        Burst,
    }
}
