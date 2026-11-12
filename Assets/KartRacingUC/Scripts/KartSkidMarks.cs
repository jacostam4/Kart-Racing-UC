using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class KartSkidMarks
{
    private const int Capacity = 1200;
    private readonly KartMovement kart;
    private readonly GameObject surface;
    private readonly Material material;
    private readonly Mesh mesh;
    private readonly Vector3[] vertices = new Vector3[Capacity * 4];
    private readonly Vector3[] normals = new Vector3[Capacity * 4];
    private readonly int[] indices = new int[Capacity * 6];
    private readonly Vector3[] previous = new Vector3[2];
    private readonly bool[] hasPrevious = new bool[2];
    private int nextSegment;
    private int count;

    public KartSkidMarks(KartMovement kart)
    {
        this.kart = kart;
        surface = new GameObject("Tire Marks", typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(surface, kart.gameObject.scene);
        mesh = new Mesh { name = "Tire Marks" };
        mesh.MarkDynamic();
        surface.GetComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(kart.RoadMaterial);
        material.SetTexture("_BaseMap", null);
        material.SetColor("_BaseColor", new Color(0.065f, 0.064f, 0.064f));
        material.SetFloat("_Smoothness", 0.03f);
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void Break()
    {
        hasPrevious[0] = false;
        hasPrevious[1] = false;
    }

    public void Draw()
    {
        bool changed = false;
        for (int side = 0; side < 2; side++)
        {
            if (!kart.TryGetRoadContact(kart.RearWheel(side).position, out var hit))
            {
                hasPrevious[side] = false;
                continue;
            }

            Vector3 point = hit.point + hit.normal * 0.012f;
            float distance = Vector3.Distance(point, previous[side]);
            if (hasPrevious[side] && distance < 0.09f)
                continue;

            if (hasPrevious[side] && distance < 0.75f)
            {
                AddSegment(previous[side], point, hit.normal);
                changed = true;
            }

            previous[side] = point;
            hasPrevious[side] = true;
        }

        if (!changed)
            return;

        int vertexCount = count * 4;
        mesh.SetVertices(vertices, 0, vertexCount);
        mesh.SetNormals(normals, 0, vertexCount);
        mesh.SetIndices(indices, 0, count * 6, MeshTopology.Triangles, 0);
    }

    private void AddSegment(Vector3 start, Vector3 end, Vector3 normal)
    {
        Vector3 width = Vector3.Cross(normal, end - start).normalized * 0.075f;
        int vertex = nextSegment * 4;
        vertices[vertex] = start - width;
        vertices[vertex + 1] = start + width;
        vertices[vertex + 2] = end - width;
        vertices[vertex + 3] = end + width;
        for (int i = 0; i < 4; i++)
            normals[vertex + i] = normal;

        int index = nextSegment * 6;
        indices[index] = vertex;
        indices[index + 1] = vertex + 2;
        indices[index + 2] = vertex + 1;
        indices[index + 3] = vertex + 1;
        indices[index + 4] = vertex + 2;
        indices[index + 5] = vertex + 3;
        nextSegment = (nextSegment + 1) % Capacity;
        count = Mathf.Min(count + 1, Capacity);
    }

    public void Dispose()
    {
        Object.Destroy(mesh);
        Object.Destroy(material);
        Object.Destroy(surface);
    }
}

