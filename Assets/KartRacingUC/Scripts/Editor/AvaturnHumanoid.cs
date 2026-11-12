using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class AvaturnHumanoid
{
    [MenuItem("Tools/Kart Racing UC/Create Avaturn Humanoid")]
    private static void Create()
    {
        GameObject model = Selection.activeGameObject;
        if (model == null || EditorUtility.IsPersistent(model) || EditorApplication.isPlaying ||
            model.transform.Find("Hips") == null)
        {
            Debug.LogError("Select the Avaturn scene object directly above Hips in Edit Mode.");
            return;
        }

        var mapping = new (string human, string bone)[]
        {
            ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Spine1"),
            ("UpperChest", "Spine2"), ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "LeftShoulder"), ("RightShoulder", "RightShoulder"),
            ("LeftUpperArm", "LeftArm"), ("RightUpperArm", "RightArm"),
            ("LeftLowerArm", "LeftForeArm"), ("RightLowerArm", "RightForeArm"),
            ("LeftHand", "LeftHand"), ("RightHand", "RightHand"),
            ("LeftUpperLeg", "LeftUpLeg"), ("RightUpperLeg", "RightUpLeg"),
            ("LeftLowerLeg", "LeftLeg"), ("RightLowerLeg", "RightLeg"),
            ("LeftFoot", "LeftFoot"), ("RightFoot", "RightFoot"),
            ("LeftToes", "LeftToeBase"), ("RightToes", "RightToeBase")
        };

        Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
        foreach (var entry in mapping)
        {
            if (transforms.Count(bone => bone.name == entry.bone) != 1)
            {
                Debug.LogError($"Expected exactly one bone named {entry.bone}.", model);
                return;
            }
        }

        var description = new HumanDescription
        {
            human = mapping.Select(entry => new HumanBone
            {
                humanName = entry.human,
                boneName = entry.bone,
                limit = new HumanLimit { useDefaultValues = true }
            }).ToArray(),
            skeleton = transforms.Select(bone => new SkeletonBone
            {
                name = bone.name,
                position = bone == model.transform ? Vector3.zero : bone.localPosition,
                rotation = bone == model.transform ? Quaternion.identity : bone.localRotation,
                scale = bone == model.transform ? Vector3.one : bone.localScale
            }).ToArray(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f
        };

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(model, description);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            if (avatar != null)
                Object.DestroyImmediate(avatar);
            Debug.LogError("Unity could not build a valid Humanoid Avatar from this pose.", model);
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Humanoid Avatar", model.name + " Humanoid", "asset",
            "Choose where to save the Humanoid Avatar.", "Assets/KartRacingUC/Animations");
        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(avatar);
            return;
        }

        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(avatar, path);
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(model);

        Undo.RecordObject(animator, "Assign Avaturn Humanoid Avatar");
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        Debug.Log($"Humanoid Avatar assigned: {path}", model);
    }
}
