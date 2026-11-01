#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Rebana las hojas de sprites de nado de Mateo (Nivel 2) en cuadros individuales.
/// Ya no se ejecuta solo al abrir Unity: la escena del río la arma SetupNivel2Editor a partir del Nivel 1.
/// </summary>
public static class RiverPlayerSetup
{
    [MenuItem("Sombras/Rebanar Sprites de Nado de Mateo (Nivel 2)")]
    public static void SetupRiverPlayer()
    {
        string spriteSheetB = "Assets/Sprites/Level2_River/mateo_swim_sheet_B.png";
        string spriteSheetA = "Assets/Sprites/Level2_River/mateo_swim_sheet_A.png";

        if (File.Exists(spriteSheetB))
        {
            SliceSwimSpriteSheetB(spriteSheetB);
        }

        if (File.Exists(spriteSheetA))
        {
            SliceSwimSpriteSheetA(spriteSheetA);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[RiverPlayerSetup] ¡Spritesheets de nado rebanados individualmente!");
    }

    private static void SliceSwimSpriteSheetB(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 126; // Cabeza del mismo tamaño que el Mateo del Nivel 1 (gorra de 88 px = 0.70 u)
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;

        // 30 sprites individuales (5 filas x 6 columnas)
        SpriteMetaData[] metas = new SpriteMetaData[]
        {
            // Fila 0: Nado hacia arriba (espaldas / norte)
            new SpriteMetaData { name = "mateo_swim_up_0", rect = new Rect(86, 604, 197, 198), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_up_1", rect = new Rect(407, 604, 209, 199), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_up_2", rect = new Rect(715, 604, 216, 201), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_up_3", rect = new Rect(1050, 604, 192, 199), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_up_4", rect = new Rect(1366, 604, 211, 200), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_up_5", rect = new Rect(1674, 604, 201, 200), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },

            // Fila 1: Nado lateral perfil 1
            new SpriteMetaData { name = "mateo_swim_side1_0", rect = new Rect(34, 471, 280, 128), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side1_1", rect = new Rect(356, 464, 286, 135), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side1_2", rect = new Rect(676, 471, 283, 128), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side1_3", rect = new Rect(1000, 459, 272, 140), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side1_4", rect = new Rect(1317, 459, 282, 140), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side1_5", rect = new Rect(1638, 468, 274, 131), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },

            // Fila 2: Nado lateral perfil 2
            new SpriteMetaData { name = "mateo_swim_side2_0", rect = new Rect(37, 289, 280, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side2_1", rect = new Rect(367, 289, 278, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side2_2", rect = new Rect(688, 289, 280, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side2_3", rect = new Rect(1009, 289, 270, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side2_4", rect = new Rect(1324, 289, 276, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_side2_5", rect = new Rect(1650, 289, 271, 165), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },

            // Fila 3: Nado hacia abajo (frente / sur)
            new SpriteMetaData { name = "mateo_swim_down_0", rect = new Rect(34, 175, 259, 104), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_down_1", rect = new Rect(399, 164, 201, 115), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_down_2", rect = new Rect(741, 175, 189, 104), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_down_3", rect = new Rect(1026, 174, 216, 105), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_down_4", rect = new Rect(1342, 167, 228, 112), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_down_5", rect = new Rect(1662, 164, 230, 115), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },

            // Fila 4: Flotando boca arriba / buceo
            new SpriteMetaData { name = "mateo_swim_float_0", rect = new Rect(57, 7, 221, 152), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_float_1", rect = new Rect(371, 14, 242, 145), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_float_2", rect = new Rect(716, 13, 204, 146), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_float_3", rect = new Rect(1013, 12, 242, 147), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_float_4", rect = new Rect(1345, 9, 242, 150), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_swim_float_5", rect = new Rect(1683, 8, 216, 151), alignment = 0, pivot = new Vector2(0.5f, 0.5f) }
        };

#pragma warning disable CS0618
        importer.spritesheet = metas;
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void SliceSwimSpriteSheetA(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 253; // Cabeza del mismo tamaño que el Mateo del Nivel 1 (gorra de 177 px = 0.70 u)
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;

        // 6 sprites de cuerpo completo (de cabeza a pies con la misma altura Y y base)
        SpriteMetaData[] metas = new SpriteMetaData[]
        {
            new SpriteMetaData { name = "mateo_tread_0", rect = new Rect(21, 130, 340, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_tread_1", rect = new Rect(415, 130, 270, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_tread_2", rect = new Rect(730, 130, 362, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_tread_3", rect = new Rect(1136, 130, 261, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_tread_4", rect = new Rect(1445, 130, 362, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) },
            new SpriteMetaData { name = "mateo_tread_5", rect = new Rect(1858, 130, 254, 420), alignment = 0, pivot = new Vector2(0.5f, 0.5f) }
        };

#pragma warning disable CS0618
        importer.spritesheet = metas;
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}
#endif
