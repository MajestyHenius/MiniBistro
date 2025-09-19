using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class NPCAnimationCreator : EditorWindow
{
    private string spritesFolderPath = "Assets/Sprites/NPCs/";
    private string animationsFolderPath = "Assets/Animations/NPCs/";

    [MenuItem("Tools/NPC Animation Creator")]
    public static void ShowWindow()
    {
        GetWindow<NPCAnimationCreator>("NPC Animation Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("NPC������������", EditorStyles.boldLabel);

        spritesFolderPath = EditorGUILayout.TextField("Sprite�ļ�·��", spritesFolderPath);
        animationsFolderPath = EditorGUILayout.TextField("��������·��", animationsFolderPath);

        if (GUILayout.Button("����NPC����"))
        {
            CreateAllNPCAnimations();
        }
    }

    void CreateAllNPCAnimations()
    {
        // ��ȡ����Sprite�ļ�
        string[] spriteFiles = Directory.GetFiles(spritesFolderPath, "*.png", SearchOption.AllDirectories);

        // ��NPC����
        var npcGroups = spriteFiles
            .Select(path => new {
                Path = path,
                FileName = Path.GetFileNameWithoutExtension(path)
            })
            .Where(file => file.FileName.Contains("_idle") || file.FileName.Contains("_walk"))
            .GroupBy(file =>
            {
                string fileName = file.FileName;
                if (fileName.Contains("_idle"))
                    return fileName.Replace("_idle", "");
                else
                    return fileName.Replace("_walk", "");
            });

        foreach (var npcGroup in npcGroups)
        {
            string npcName = npcGroup.Key;
            CreateNPCAnimations(npcName, npcGroup.Select(g => g.Path).ToArray());
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("����NPC�����������!");
    }

    void CreateNPCAnimations(string npcName, string[] spritePaths)
    {
        // ����idle��walk Sprite
        string idlePath = spritePaths.FirstOrDefault(p => p.Contains("_idle"));
        string walkPath = spritePaths.FirstOrDefault(p => p.Contains("_walk"));

        if (string.IsNullOrEmpty(idlePath) || string.IsNullOrEmpty(walkPath))
        {
            Debug.LogWarning($"NPC {npcName} ȱ��idle��walk sprite");
            return;
        }

        // ��������Sprite�������ֺ�׺����
        Sprite[] idleSprites = AssetDatabase.LoadAllAssetsAtPath(idlePath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                // ��ȡ���ֺ�׺��������
                Match match = Regex.Match(s.name, @"_(\d+)$");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            })
            .ToArray();

        Sprite[] walkSprites = AssetDatabase.LoadAllAssetsAtPath(walkPath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                // ��ȡ���ֺ�׺��������
                Match match = Regex.Match(s.name, @"_(\d+)$");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            })
            .ToArray();

        // ��֤Sprite����
        if (idleSprites.Length < 24 || walkSprites.Length < 24)
        {
            Debug.LogWarning($"NPC {npcName} ��Sprite��������24��");
            return;
        }

        // ����8������Ķ��� (4��idle + 4��walk)
        // ֡˳��Ϊ: [R R R R R R U U U U U U L L L L L L D D D D D D]
        CreateDirectionAnimation(npcName, "Idle_Up", idleSprites, 6, 11);    // �Ϸ���: ����6-11
        CreateDirectionAnimation(npcName, "Idle_Left", idleSprites, 12, 17); // ����: ����12-17
        CreateDirectionAnimation(npcName, "Idle_Down", idleSprites, 18, 23); // �·���: ����18-23

        CreateDirectionAnimation(npcName, "Walk_Up", walkSprites, 6, 11);    // �Ϸ���: ����6-11
        CreateDirectionAnimation(npcName, "Walk_Left", walkSprites, 12, 17); // ����: ����12-17
        CreateDirectionAnimation(npcName, "Walk_Down", walkSprites, 18, 23); // �·���: ����18-23

        // ����Ҫ�ҷ�����Ϊ����ʹ������ľ���
    }

    void CreateDirectionAnimation(string npcName, string direction, Sprite[] sourceSprites, int startIndex, int endIndex)
    {
        if (sourceSprites == null || sourceSprites.Length <= endIndex)
        {
            Debug.LogWarning($"�޷�Ϊ{npcName}����{direction}����: Sprite��������");
            return;
        }

        // ������������
        AnimationClip clip = new AnimationClip();
        clip.name = $"{npcName}_{direction}";
        clip.frameRate = 10; // ���ú��ʵ�֡��

        // ������������
        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite",
            path = "" // ��·����ʾ������ӵ��Animator��GameObject����
        };

        // �����ؼ�֡
        int frameCount = endIndex - startIndex + 1;
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = sourceSprites[startIndex + i]
            };
        }

        // ���ö�������
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        // ���ö���Ϊѭ��
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // ȷ��Ŀ¼����
        string npcFolder = Path.Combine(animationsFolderPath, npcName);
        if (!Directory.Exists(npcFolder))
            Directory.CreateDirectory(npcFolder);

        // ���涯��
        string assetPath = Path.Combine(npcFolder, $"{clip.name}.anim");
        AssetDatabase.CreateAsset(clip, assetPath);

        Debug.Log($"�Ѵ�������: {assetPath}");
    }
}