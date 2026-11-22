using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>Deforms the icon after UI rebuilds without injecting Unity's abstract mesh-effect class.</summary>
internal sealed class AttunementItemWarp : IDisposable
{
    private readonly Image image;
    private readonly Mesh mesh;
    private readonly UIVertex[] corners = new UIVertex[4];
    private readonly Canvas.WillRenderCanvases render;
    private float strength;
    private float phase;

    public AttunementItemWarp(Image image)
    {
        this.image = image;
        mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        // Capture Unity's own sprite geometry, preserving aspect ratio and atlas UVs.
        image.SetVerticesDirty();
        image.Rebuild(CanvasUpdate.PreRender);
        var vertices = new VertexHelper(image.canvasRenderer.GetMesh());
        try
        {
            if (vertices.currentVertCount != 4)
                throw new InvalidOperationException("Attunement requires a simple item icon quad.");
            for (int i = 0; i < corners.Length; i++) vertices.PopulateUIVertex(ref corners[i], i);
        }
        catch
        {
            UnityEngine.Object.Destroy(mesh);
            throw;
        }
        finally { vertices.Dispose(); }
        render = (Canvas.WillRenderCanvases)(Action)Render;
        Canvas.willRenderCanvases += render;
    }

    public void SetDistortion(float amount, float time)
    {
        strength = amount;
        phase = time;
    }

    private void Render()
    {
        if (image == null || !image.isActiveAndEnabled) return;
        var vertices = new VertexHelper();
        try
        {
            ModifyMesh(vertices);
            vertices.FillMesh(mesh);
            image.canvasRenderer.SetMesh(mesh);
        }
        finally { vertices.Dispose(); }
    }

    private void ModifyMesh(VertexHelper vertices)
    {
        float width = Mathf.Abs(corners[2].position.x - corners[0].position.x);
        const int bands = 20;
        for (int band = 0; band < bands; band++)
        {
            for (int side = 0; side < 4; side++)
            {
                bool right = side >= 2;
                bool top = side == 1 || side == 2;
                float t = (band + (top ? 1f : 0f)) / bands;
                UIVertex bottom = corners[right ? 3 : 0];
                UIVertex upper = corners[right ? 2 : 1];
                UIVertex vertex = bottom;
                vertex.color = image.color;
                vertex.position = Vector3.Lerp(bottom.position, upper.position, t);
                vertex.uv0 = Vector4.Lerp(bottom.uv0, upper.uv0, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                vertex.position.x += Mathf.Sin(t * Mathf.PI * 2.6f + phase) * width * strength * 0.19f * envelope;
                vertex.position.y += Mathf.Sin(t * Mathf.PI * 3.1f - phase * 0.7f) * width * strength * 0.035f * envelope;
                vertices.AddVert(vertex);
            }
            int index = band * 4;
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index + 2, index + 3, index);
        }
    }

    public void Dispose()
    {
        Canvas.willRenderCanvases -= render;
        if (image != null) image.SetVerticesDirty();
        UnityEngine.Object.Destroy(mesh);
    }
}
