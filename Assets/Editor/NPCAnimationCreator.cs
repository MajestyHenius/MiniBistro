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
        GUILayout.Label("NPC动画创建工具", EditorStyles.boldLabel);

        spritesFolderPath = EditorGUILayout.TextField("Sprite文件路径", spritesFolderPath);
        animationsFolderPath = EditorGUILayout.TextField("动画保存路径", animationsFolderPath);

        if (GUILayout.Button("创建NPC动画"))
        {
            CreateAllNPCAnimations();
        }
    }

    void CreateAllNPCAnimations()
    {
        // 获取所有Sprite文件
        string[] spriteFiles = Directory.GetFiles(spritesFolderPath, "*.png", SearchOption.AllDirectories);

        // 按NPC分组
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
        Debug.Log("所有NPC动画创建完成!");
    }

    void CreateNPCAnimations(string npcName, string[] spritePaths)
    {
        // 分离idle和walk Sprite
        string idlePath = spritePaths.FirstOrDefault(p => p.Contains("_idle"));
        string walkPath = spritePaths.FirstOrDefault(p => p.Contains("_walk"));

        if (string.IsNullOrEmpty(idlePath) || string.IsNullOrEmpty(walkPath))
        {
            Debug.LogWarning($"NPC {npcName} 缺少idle或walk sprite");
            return;
        }

        // 加载所有Sprite并按数字后缀排序
        Sprite[] idleSprites = AssetDatabase.LoadAllAssetsAtPath(idlePath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                // 提取数字后缀进行排序
                Match match = Regex.Match(s.name, @"_(\d+)$");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            })
            .ToArray();

        Sprite[] walkSprites = AssetDatabase.LoadAllAssetsAtPath(walkPath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                // 提取数字后缀进行排序
                Match match = Regex.Match(s.name, @"_(\d+)$");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            })
            .ToArray();

        // 验证Sprite数量
        if (idleSprites.Length < 24 || walkSprites.Length < 24)
        {
            Debug.LogWarning($"NPC {npcName} 的Sprite数量不足24个");
            return;
        }

        // 创建8个方向的动画 (4个idle + 4个walk)
        // 帧顺序为: [R R R R R R U U U U U U L L L L L L D D D D D D]
        CreateDirectionAnimation(npcName, "Idle_Up", idleSprites, 6, 11);    // 上方向: 索引6-11
        CreateDirectionAnimation(npcName, "Idle_Left", idleSprites, 12, 17); // 左方向: 索引12-17
        CreateDirectionAnimation(npcName, "Idle_Down", idleSprites, 18, 23); // 下方向: 索引18-23

        CreateDirectionAnimation(npcName, "Walk_Up", walkSprites, 6, 11);    // 上方向: 索引6-11
        CreateDirectionAnimation(npcName, "Walk_Left", walkSprites, 12, 17); // 左方向: 索引12-17
        CreateDirectionAnimation(npcName, "Walk_Down", walkSprites, 18, 23); // 下方向: 索引18-23

        // 不需要右方向，因为可以使用左方向的镜像
    }

    void CreateDirectionAnimation(string npcName, string direction, Sprite[] sourceSprites, int startIndex, int endIndex)
    {
        if (sourceSprites == null || sourceSprites.Length <= endIndex)
        {
            Debug.LogWarning($"无法为{npcName}创建{direction}动画: Sprite数量不足");
            return;
        }

        // 创建动画剪辑
        AnimationClip clip = new AnimationClip();
        clip.name = $"{npcName}_{direction}";
        clip.frameRate = 10; // 设置合适的帧率

        // 创建动画曲线
        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite",
            path = "" // 空路径表示作用于拥有Animator的GameObject本身
        };

        // 创建关键帧
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

        // 设置动画曲线
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        // 设置动画为循环
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // 确保目录存在
        string npcFolder = Path.Combine(animationsFolderPath, npcName);
        if (!Directory.Exists(npcFolder))
            Directory.CreateDirectory(npcFolder);

        // 保存动画
        string assetPath = Path.Combine(npcFolder, $"{clip.name}.anim");
        AssetDatabase.CreateAsset(clip, assetPath);

        Debug.Log($"已创建动画: {assetPath}");
    }
}