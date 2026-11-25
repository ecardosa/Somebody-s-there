using UnityEngine;
using UnityEditor;

public class ShiftTerrainHeight : EditorWindow
{
    private float offsetMeters = 50f;

    [MenuItem("Tools/Subir Heightmap del Terreno")]
    public static void ShowWindow()
    {
        GetWindow<ShiftTerrainHeight>("Shift Terrain");
    }

    private void OnGUI()
    {
        offsetMeters = EditorGUILayout.FloatField("Metros a subir:", offsetMeters);

        if (GUILayout.Button("Aplicar al Terreno seleccionado"))
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Selecciona tu objeto Terrain en la jerarquía primero.", "OK");
                return;
            }

            TerrainData data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Shift Terrain Height");
            Undo.RecordObject(terrain.transform, "Shift Terrain Position");

            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            float normalizedOffset = offsetMeters / data.size.y;

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    heights[x, y] = Mathf.Clamp01(heights[x, y] + normalizedOffset);
                }
            }

            data.SetHeights(0, 0, heights);
            terrain.transform.position -= new Vector3(0, offsetMeters, 0);
        }
    }
}